using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LecturerPayrollApp.Models
{
    public enum ClaimStatus
    {
        Draft,
        Submitted,
        Approved,
        Rejected,
        Completed,
        UnderReview
    }

    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty; // Lecturer, Coordinator, Manager, HR

        public string? Department { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<MonthlyClaim> MonthlyClaims { get; set; } = new List<MonthlyClaim>();
        public ICollection<Approval> Approvals { get; set; } = new List<Approval>();

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";
    }


    public class MonthlyClaim
    {
        public int Id { get; set; }

        [Required]
        public int LecturerId { get; set; }
        public User? Lecturer { get; set; }  // Changed from Lecturer? to User?

        [Required]
        [Display(Name = "Claim Month")]
        public DateTime ClaimMonth { get; set; }

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

        public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;
        public DateTime SubmittedDate { get; set; } = DateTime.Now;

        // Automated policy check fields
        public bool IsAutoFlagged { get; set; }
        public string? AutoVerificationNotes { get; set; }
        public DateTime? AutoVerifiedDate { get; set; }

        // Navigation properties
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<Approval> Approvals { get; set; } = new List<Approval>();

        // Method to calculate total amount
        public void CalculateTotalAmount()
        {
            TotalAmount = HoursWorked * HourlyRate;
        }

        // 160-HOUR POLICY CHECK METHODS
        public bool Exceeds160Hours()
        {
            return HoursWorked > 160.0m;
        }

        public string GetHoursPolicyStatus()
        {
            if (HoursWorked > 160.0m)
                return "EXCEEDS_LIMIT";
            else if (HoursWorked > 140.0m)
                return "WARNING";
            else
                return "WITHIN_LIMIT";
        }

        public string? GetPolicyViolationDescription()
        {
            var violations = new List<string>();

            if (Exceeds160Hours())
                violations.Add($"HOURS_EXCEEDED: {HoursWorked} hours (Max: 160)");

            if (HourlyRate < 15.0m || HourlyRate > 200.0m)
                violations.Add($"RATE_OUT_OF_RANGE: ${HourlyRate} (Allowed: $15-$200)");

            return violations.Any() ? string.Join(" | ", violations) : null;
        }

        public void ApplyAutoVerification()
        {
            AutoVerifiedDate = DateTime.Now;
            var violations = GetPolicyViolationDescription();

            if (!string.IsNullOrEmpty(violations))
            {
                IsAutoFlagged = true;
                AutoVerificationNotes = $"AUTO-VERIFICATION: {violations}";
            }
            else
            {
                IsAutoFlagged = false;
                AutoVerificationNotes = "AUTO-VERIFICATION: Passed all policy checks";
            }
        }

    }


    public class Approval
    {
        public int Id { get; set; }

        [Required]
        public int ClaimId { get; set; }
        public MonthlyClaim? Claim { get; set; }

        [Required]
        public int ApproverId { get; set; }
        public User? Approver { get; set; }  // Changed from Lecturer? to User?

        [Required]
        public string ApproverRole { get; set; } = string.Empty; // "Coordinator" or "Manager"

        [Required]
        public bool IsApproved { get; set; }

        public string? Comments { get; set; }

        public DateTime ApprovalDate { get; set; } = DateTime.Now;
    }


public class Lecturer
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        public string? Department { get; set; }

        [Display(Name = "Hourly Rate")]
        [Range(15, 200, ErrorMessage = "Hourly rate must be between $15 and $200")]
        public decimal HourlyRate { get; set; } = 50.0m; // Default rate

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<MonthlyClaim> MonthlyClaims { get; set; } = new List<MonthlyClaim>();

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";
    }

        public class Document
        {
            public int Id { get; set; }

            [Required]
            public int ClaimId { get; set; }
            public MonthlyClaim? Claim { get; set; }

            [Required]
            [Display(Name = "File Name")]
            public string FileName { get; set; } = string.Empty;

            [Display(Name = "Description")]
            public string? Description { get; set; }

            [Display(Name = "File Path")]
            public string FilePath { get; set; } = string.Empty;

            [Display(Name = "Upload Date")]
            public DateTime UploadedDate { get; set; } = DateTime.Now; // FIXED: Use UploadedDate

            [Display(Name = "File Type")]
            public string FileType { get; set; } = string.Empty;

            [Display(Name = "File Size")]
            public long FileSize { get; set; }

            [NotMapped]
            [Display(Name = "Upload File")]
            public IFormFile? File { get; set; }
        }
    


    // View Models for forms
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public class SubmitClaimViewModel
    {
        [Required(ErrorMessage = "Claim month is required")]
        [Display(Name = "Claim Month")]
        public DateTime ClaimMonth { get; set; } = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        [Required(ErrorMessage = "Hours worked is required")]
        [Range(0.1, 200, ErrorMessage = "Hours must be between 0.1 and 200")]
        [Display(Name = "Hours Worked")]
        public decimal HoursWorked { get; set; }

        [Required(ErrorMessage = "Hourly rate is required")]
        [Range(15, 500, ErrorMessage = "Hourly rate must be between $15 and $500")]
        [Display(Name = "Hourly Rate")]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Calculated Amount")]
        public decimal CalculatedAmount { get; set; }

        [Required(ErrorMessage = "Total amount is required")]
        [Display(Name = "Total Amount")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [Display(Name = "Work Description")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Supporting Document")]
        public IFormFile? Document { get; set; }

        public void CalculateAmount()
        {
            CalculatedAmount = HoursWorked * HourlyRate;
            TotalAmount = CalculatedAmount;
        }
    }

    // NEW VIEW MODELS ADDED BELOW

    public class VerifiedClaimViewModel
    {
        public MonthlyClaim Claim { get; set; } = new MonthlyClaim();
        public VerificationResult VerificationResult { get; set; } = new VerificationResult();
        public bool CanAutoApprove => VerificationResult?.IsApproved == true;
    }

    public class VerificationResult
    {
        public bool IsApproved { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Issues { get; set; } = new List<string>();
        public string Summary => Issues?.Any() == true ? string.Join("; ", Issues) : "No issues found";
    }

    public class PayrollSummaryViewModel
    {
        public string LecturerName { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public decimal TotalAmount { get; set; }
        public int NumberOfClaims { get; set; }
    }

    public class ClaimReviewViewModel
    {
        public int ClaimId { get; set; }
        public bool IsApproved { get; set; }
        public string Comments { get; set; } = string.Empty;
        public bool AutoRecommendation { get; set; }
    }

    public class DashboardViewModel
    {
        public List<MonthlyClaim> RecentClaims { get; set; } = new List<MonthlyClaim>();
        public int PendingApprovals { get; set; }
        public decimal MonthlyTotal { get; set; }
        public string UserRole { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }

    public class DocumentUploadViewModel
    {
        [Required]
        public int ClaimId { get; set; }

        [Required]
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; } = "Supporting";

        [Required]
        [Display(Name = "Select File")]
        public IFormFile File { get; set; } = null!;

        [Display(Name = "Description")]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;
    }

    public class UserProfileViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Department")]
        public string Department { get; set; } = string.Empty;
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class HRDashboardViewModel
    {
        public int TotalApprovedClaims { get; set; }
        public int TotalPendingClaims { get; set; }
        public int TotalLecturers { get; set; }
        public decimal TotalAmountThisMonth { get; set; }
    }

    public class ReportFilterViewModel
    {
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Lecturer")]
        public int? LecturerId { get; set; }

        [Display(Name = "Status")]
        public ClaimStatus? Status { get; set; }

        [Display(Name = "Department")]
        public string? Department { get; set; }

        [Display(Name = "Report Format")]
        public string Format { get; set; } = "PDF";
    }

    public class ReportRequestViewModel
    {
        [Required]
        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
        public int Month { get; set; } = DateTime.Now.Month;

        [Required]
        [Range(2020, 2030, ErrorMessage = "Year must be between 2020 and 2030")]
        public int Year { get; set; } = DateTime.Now.Year;

        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "Payment";

        [Display(Name = "Include Details")]
        public bool IncludeDetails { get; set; } = true;

        [Display(Name = "Export Format")]
        public string ExportFormat { get; set; } = "PDF";
    }

    public class UserManagementViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Department")]
        public string Department { get; set; } = string.Empty;

        [Display(Name = "Max Monthly Hours")]
        [Range(1, 200, ErrorMessage = "Monthly hours must be between 1 and 200")]
        public decimal MaxMonthlyHours { get; set; } = 160m;

        [Display(Name = "Contract Hourly Rate")]
        [Range(0, 500, ErrorMessage = "Hourly rate must be between 0 and 500")]
        public decimal ContractHourlyRate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public class ReportViewModel
    {
        [Required]
        [Range(2020, 2030, ErrorMessage = "Please enter a valid year")]
        public int Year { get; set; } = DateTime.Now.Year;

        [Required]
        [Range(1, 12, ErrorMessage = "Please enter a valid month (1-12)")]
        public int Month { get; set; } = DateTime.Now.Month;
    }

    // Add this class definition (place it after the SubmitClaimViewModel class)
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

