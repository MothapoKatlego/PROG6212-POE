using LecturerPayrollApp.Data;
using LecturerPayrollApp.Models;
using LecturerPayrollApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LecturerPayrollApp.Controllers
{
    public class ClaimsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IAutoVerificationService _verificationService;

        public ClaimsController(AppDbContext context, IWebHostEnvironment environment, IAutoVerificationService verificationService)
        {
            _context = context;
            _environment = environment;
            _verificationService = verificationService;
        }

        // GET: Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            var userId = int.Parse(userIdStr);

            ViewBag.UserRole = role;
            ViewBag.UserName = HttpContext.Session.GetString("FirstName");

            if (role == "Lecturer")
            {
                var myClaims = await _context.MonthlyClaims
                    .Where(c => c.LecturerId == userId)
                    .OrderByDescending(c => c.SubmittedDate)
                    .Take(5)
                    .ToListAsync();
                return View("Dashboard", myClaims);
            }
            else if (role == "Coordinator" || role == "Manager")
            {
                var claims = await _context.MonthlyClaims
                    .Include(c => c.Lecturer)
                    .Where(c => c.Status == ClaimStatus.Submitted)
                    .OrderByDescending(c => c.SubmittedDate)
                    .Take(10)
                    .ToListAsync();
                return View("Dashboard", claims);
            }

            return RedirectToAction("Login", "Account");
        }

        // GET: Submit claim for Lecturers
        public IActionResult Submit()
        {
            if (HttpContext.Session.GetString("Role") != "Lecturer")
                return RedirectToAction("Login", "Account");

            return View(new SubmitClaimViewModel());
        }

        // POST: Submit claim for Lecturers with 160-hour verification and document upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(SubmitClaimViewModel model)
        {
            if (HttpContext.Session.GetString("Role") != "Lecturer")
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                try
                {
                    var userIdStr = HttpContext.Session.GetString("UserId");
                    if (string.IsNullOrEmpty(userIdStr))
                        return RedirectToAction("Login", "Account");

                    // Auto-calculate the amount
                    model.CalculateAmount();

                    var claim = new MonthlyClaim
                    {
                        LecturerId = int.Parse(userIdStr),
                        ClaimMonth = model.ClaimMonth,
                        HoursWorked = model.HoursWorked,
                        HourlyRate = model.HourlyRate,
                        TotalAmount = model.TotalAmount,
                        Description = model.Description,
                        Status = ClaimStatus.Submitted,
                        SubmittedDate = DateTime.Now
                    };

                    // APPLY 160-HOUR AUTO VERIFICATION
                    var verificationResult = _verificationService.VerifyClaim(claim);
                    claim.ApplyAutoVerification();

                    // Set status based on verification
                    if (verificationResult.HasErrors || claim.Exceeds160Hours())
                    {
                        claim.Status = ClaimStatus.Submitted; // Keep as submitted for manual review
                    }

                    claim.CalculateTotalAmount();

                    _context.MonthlyClaims.Add(claim);
                    await _context.SaveChangesAsync();

                    // Handle document uploads if any
                    if (model.Documents != null && model.Documents.Any())
                    {
                        await UploadDocumentsAsync(claim.Id, model.Documents, model.DocumentDescriptions);
                    }

                    // Show appropriate message
                    if (claim.Exceeds160Hours())
                    {
                        TempData["Warning"] = $"Claim submitted but flagged for review: {claim.HoursWorked} hours exceeds 160-hour limit.";
                    }
                    else if (verificationResult.HasWarnings)
                    {
                        TempData["Warning"] = "Claim submitted but has warnings that require review.";
                    }
                    else
                    {
                        TempData["Success"] = "Claim submitted successfully and passed all policy checks!";
                    }

                    // Add document count to success message if documents were uploaded
                    if (model.Documents != null && model.Documents.Any(d => d.Length > 0))
                    {
                        var uploadedCount = model.Documents.Count(d => d.Length > 0);
                        if (uploadedCount > 0)
                        {
                            TempData["Success"] += $" {uploadedCount} document(s) uploaded.";
                        }
                    }

                    return RedirectToAction("Dashboard");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "An error occurred while submitting the claim.";
                }
            }

            return View(model);
        }

        // Helper method to upload documents
        private async Task UploadDocumentsAsync(int claimId, List<IFormFile> documents, List<string> descriptions)
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "documents");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            for (int i = 0; i < documents.Count; i++)
            {
                var file = documents[i];
                if (file != null && file.Length > 0)
                {
                    try
                    {
                        // Validate file size (max 10MB)
                        if (file.Length > 10 * 1024 * 1024)
                        {
                            continue; // Skip this file
                        }

                        // Validate file type
                        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".txt" };
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            continue; // Skip this file
                        }

                        // Generate unique filename
                        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                        var filePath = Path.Combine(uploadsPath, fileName);

                        // Save the file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Get description (if provided)
                        var description = descriptions != null && i < descriptions.Count ? descriptions[i] : null;

                        // Create document record
                        var document = new Document
                        {
                            ClaimId = claimId,
                            FileName = file.FileName,
                            Description = description,
                            FilePath = $"/uploads/documents/{fileName}",
                            FileType = fileExtension,
                            FileSize = file.Length,
                            UploadedDate = DateTime.Now
                        };

                        _context.Documents.Add(document);
                    }
                    catch (Exception)
                    {
                        // Log error but continue with other files
                        continue;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        // GET: MyClaims - Lecturer's view of their own claims
        public async Task<IActionResult> MyClaims()
        {
            if (HttpContext.Session.GetString("Role") != "Lecturer")
                return RedirectToAction("Login", "Account");

            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdStr);
            var claims = await _context.MonthlyClaims
                .Include(c => c.Documents)
                .Include(c => c.Approvals)
                .Where(c => c.LecturerId == userId)
                .OrderByDescending(c => c.SubmittedDate)
                .ToListAsync();

            return View(claims);
        }

        // GET: Claims/ReviewClaims - Coordinator view with 160-hour policy
        public async Task<IActionResult> ReviewClaims()
        {
            if (HttpContext.Session.GetString("Role") != "Coordinator")
                return RedirectToAction("Login", "Account");

            var claims = await _context.MonthlyClaims
                .Include(c => c.Lecturer)
                .Include(c => c.Documents)
                .Where(c => c.Status == ClaimStatus.Submitted)
                .OrderByDescending(c => c.SubmittedDate)
                .ToListAsync();

            return View(claims);
        }

        // POST: Claims/ApproveAsCoordinator
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAsCoordinator(int claimId, bool isApproved, string comments = "")
        {
            if (HttpContext.Session.GetString("Role") != "Coordinator")
                return RedirectToAction("Login", "Account");

            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                    return RedirectToAction("Login", "Account");

                var claim = await _context.MonthlyClaims
                    .Include(c => c.Lecturer)
                    .FirstOrDefaultAsync(c => c.Id == claimId);

                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(ReviewClaims));
                }

                // FEATURE 3: CHECK 160-HOUR POLICY BEFORE APPROVAL
                var verificationResult = _verificationService.VerifyClaim(claim);
                bool exceeds160Hours = claim.Exceeds160Hours();

                if (isApproved)
                {
                    // If claim exceeds 160 hours, show warning but allow override
                    if (exceeds160Hours)
                    {
                        // Add special note about policy override
                        comments += $" [POLICY OVERRIDE: Claim exceeds 160-hour limit ({claim.HoursWorked} hours)]";
                        TempData["Warning"] = $"Claim #{claim.Id} approved with policy override - {claim.HoursWorked} hours exceeds 160-hour limit.";
                    }
                    else if (verificationResult.HasWarnings)
                    {
                        comments += $" [Reviewed with warnings: {string.Join(", ", verificationResult.Warnings)}]";
                        TempData["Success"] = $"Claim #{claim.Id} approved with warnings.";
                    }
                    else
                    {
                        TempData["Success"] = $"Claim #{claim.Id} approved successfully.";
                    }

                    claim.Status = ClaimStatus.Approved;
                }
                else
                {
                    // Rejection - include policy violation reason if applicable
                    if (exceeds160Hours)
                    {
                        comments += $" [Rejected: Exceeds 160-hour policy limit]";
                    }
                    claim.Status = ClaimStatus.Rejected;
                    TempData["Success"] = $"Claim #{claim.Id} has been rejected.";
                }

                // Create approval record
                var approval = new Approval
                {
                    ClaimId = claimId,
                    ApproverId = int.Parse(userIdStr),
                    ApproverRole = "Coordinator",
                    IsApproved = isApproved,
                    Comments = comments,
                    ApprovalDate = DateTime.Now
                };

                _context.Approvals.Add(approval);
                _context.MonthlyClaims.Update(claim);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(ReviewClaims));
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while processing the claim.";
                return RedirectToAction(nameof(ReviewClaims));
            }
        }

        // GET: Approve Claims (Manager)
        public async Task<IActionResult> ApproveClaims()
        {
            if (HttpContext.Session.GetString("Role") != "Manager")
                return RedirectToAction("Login", "Account");

            var claims = await _context.MonthlyClaims
                .Include(c => c.Lecturer)
                .Include(c => c.Documents)
                .Include(c => c.Approvals)
                .Where(c => c.Status == ClaimStatus.Submitted)
                .OrderByDescending(c => c.SubmittedDate)
                .ToListAsync();

            return View(claims);
        }

        // POST: ApproveAsManager
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAsManager(int claimId, bool isApproved, string comments = "")
        {
            if (HttpContext.Session.GetString("Role") != "Manager")
                return RedirectToAction("Login", "Account");

            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var approval = new Approval
            {
                ClaimId = claimId,
                ApproverId = int.Parse(userIdStr),
                ApproverRole = "Manager",
                IsApproved = isApproved,
                Comments = comments,
                ApprovalDate = DateTime.Now
            };

            _context.Approvals.Add(approval);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Claim {(isApproved ? "approved" : "rejected")} successfully!";
            return RedirectToAction("ApproveClaims");
        }

        // GET: View Claim Details
        public async Task<IActionResult> Details(int id)
        {
            var claim = await _context.MonthlyClaims
                .Include(c => c.Lecturer)
                .Include(c => c.Documents)
                .Include(c => c.Approvals)
                .ThenInclude(a => a.Approver)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();

            // Check permissions
            var userRole = HttpContext.Session.GetString("Role");
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (userRole == "Lecturer")
            {
                if (string.IsNullOrEmpty(userIdStr) || claim.LecturerId != int.Parse(userIdStr))
                {
                    TempData["Error"] = "Access denied.";
                    return RedirectToAction("MyClaims");
                }
            }

            return View(claim);
        }

        // GET: Upload Document
        public async Task<IActionResult> UploadDocument(int? claimId)
        {
            if (HttpContext.Session.GetString("Role") != "Lecturer")
                return RedirectToAction("Login", "Account");

            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdStr);

            // If claimId is provided, verify the claim belongs to the user
            if (claimId.HasValue)
            {
                var claim = await _context.MonthlyClaims
                    .FirstOrDefaultAsync(c => c.Id == claimId && c.LecturerId == userId);

                if (claim == null)
                {
                    TempData["Error"] = "Claim not found or access denied.";
                    return RedirectToAction("MyClaims");
                }

                var viewModel = new UploadDocumentViewModel
                {
                    ClaimId = claim.Id,
                    Claim = claim
                };

                return View(viewModel);
            }

            // If no claimId, show form without pre-selected claim
            return View(new UploadDocumentViewModel());
        }

        // POST: Upload Document
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument(UploadDocumentViewModel model)
        {
            if (HttpContext.Session.GetString("Role") != "Lecturer")
                return RedirectToAction("Login", "Account");

            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdStr);

            // Verify the claim belongs to the user
            var claim = await _context.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == model.ClaimId && c.LecturerId == userId);

            if (claim == null)
            {
                TempData["Error"] = "Claim not found or access denied.";
                return RedirectToAction("MyClaims");
            }

            if (ModelState.IsValid && model.File != null && model.File.Length > 0)
            {
                try
                {
                    // Validate file size (max 10MB)
                    if (model.File.Length > 10 * 1024 * 1024)
                    {
                        ModelState.AddModelError("File", "File size cannot exceed 10MB.");
                        model.Claim = claim;
                        return View(model);
                    }

                    // Validate file type
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".txt" };
                    var fileExtension = Path.GetExtension(model.File.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("File", "Only PDF, Word, Image, and Text files are allowed.");
                        model.Claim = claim;
                        return View(model);
                    }

                    // Create uploads directory if it doesn't exist
                    var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "documents");
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                    }

                    // Generate unique filename
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.File.FileName)}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    // Save the file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.File.CopyToAsync(stream);
                    }

                    // Create document record
                    var document = new Document
                    {
                        ClaimId = model.ClaimId,
                        FileName = model.File.FileName,
                        Description = model.Description,
                        FilePath = $"/uploads/documents/{fileName}",
                        FileType = fileExtension,
                        FileSize = model.File.Length,
                        UploadedDate = DateTime.Now
                    };

                    _context.Documents.Add(document);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Document '{model.File.FileName}' uploaded successfully!";
                    return RedirectToAction("MyClaims");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error uploading file: {ex.Message}";
                    model.Claim = claim;
                    return View(model);
                }
            }

            model.Claim = claim;
            return View(model);
        }

        // GET: View/Download Document
        public async Task<IActionResult> ViewDocument(int id)
        {
            var document = await _context.Documents
                .Include(d => d.Claim)
                .ThenInclude(c => c.Lecturer)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                TempData["Error"] = "Document not found.";
                return RedirectToAction("MyClaims");
            }

            // Check permissions
            var userRole = HttpContext.Session.GetString("Role");
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdStr);

            bool hasAccess = false;

            switch (userRole)
            {
                case "Lecturer":
                    hasAccess = document.Claim?.LecturerId == userId;
                    break;
                case "Coordinator":
                case "Manager":
                case "HR":
                    hasAccess = true; // These roles can view all documents
                    break;
                default:
                    hasAccess = false;
                    break;
            }

            if (!hasAccess)
            {
                TempData["Error"] = "Access denied.";
                return RedirectToAction("Dashboard");
            }

            var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found on server.";
                return RedirectToAction("MyClaims");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = GetContentType(document.FileType);

            return File(fileBytes, contentType, document.FileName);
        }

        // GET: Documents for a claim (for managers and coordinators)
        public async Task<IActionResult> ClaimDocuments(int claimId)
        {
            var claim = await _context.MonthlyClaims
                .Include(c => c.Lecturer)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null)
            {
                TempData["Error"] = "Claim not found.";
                return RedirectToAction("Dashboard");
            }

            // Check if user has permission to view these documents
            var userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Coordinator" && userRole != "Manager" && userRole != "HR")
            {
                TempData["Error"] = "Access denied.";
                return RedirectToAction("Dashboard");
            }

            ViewBag.Claim = claim;
            return View(claim.Documents.ToList());
        }

        // Helper method to get content type
        private string GetContentType(string fileExtension)
        {
            return fileExtension.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }

    // ViewModel for claim submission
    // ViewModel for claim submission
    public class SubmitClaimViewModel
    {
        [Required]
        [Display(Name = "Claim Month")]
        public DateTime ClaimMonth { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Hours Worked")]
        [Range(0.1, 176, ErrorMessage = "Hours must be between 0.1 and 176")]
        public decimal HoursWorked { get; set; }

        [Required]
        [Display(Name = "Hourly Rate")]
        [Range(15, 200, ErrorMessage = "Hourly rate must be between $15 and $200")]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        public string? Description { get; set; }

        // Document upload fields
        [Display(Name = "Supporting Documents")]
        public List<IFormFile>? Documents { get; set; }

        [Display(Name = "Document Descriptions")]
        public List<string>? DocumentDescriptions { get; set; }

        public void CalculateAmount()
        {
            TotalAmount = HoursWorked * HourlyRate;
        }
    }

    // ViewModel for document upload
    public class UploadDocumentViewModel
    {
        [Required]
        public int ClaimId { get; set; }

        [Required]
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please select a file to upload")]
        [Display(Name = "Select File")]
        public IFormFile? File { get; set; }

        // For display
        public MonthlyClaim? Claim { get; set; }
    }
}