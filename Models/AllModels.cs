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
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty; // Lecturer, Coordinator, Manager

        // Navigation properties
        public ICollection<MonthlyClaim> Claims { get; set; } = new List<MonthlyClaim>();
        public ICollection<Approval> Approvals { get; set; } = new List<Approval>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }

    public class MonthlyClaim
    {
        public int Id { get; set; }

        [Required]
        public int LecturerId { get; set; }
        public User? Lecturer { get; set; }

        [Required]
        public DateTime ClaimMonth { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;

        public DateTime SubmittedDate { get; set; } = DateTime.Now;

        public string Description { get; set; } = string.Empty;

        // Navigation properties
        public ICollection<Approval> Approvals { get; set; } = new List<Approval>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }

    public class Approval
    {
        public int Id { get; set; }

        [Required]
        public int ClaimId { get; set; }
        public MonthlyClaim? Claim { get; set; }

        [Required]
        public int ApproverId { get; set; }
        public User? Approver { get; set; }

        [Required]
        public string ApproverRole { get; set; } = string.Empty; // Coordinator, Manager

        [Required]
        public bool IsApproved { get; set; }

        public string Comments { get; set; } = string.Empty;

        public DateTime ApprovalDate { get; set; } = DateTime.Now;
    }

    public class Document
    {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public int? ClaimId { get; set; }
        public MonthlyClaim? Claim { get; set; }

        public int UploadedById { get; set; }
        public User? UploadedBy { get; set; }

        public DateTime UploadDate { get; set; } = DateTime.Now;
    }

    // View Models for forms
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class SubmitClaimViewModel
    {
        [Required]
        [Display(Name = "Claim Month")]
        public DateTime ClaimMonth { get; set; } = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        [Required]
        [Display(Name = "Total Amount")]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Supporting Document")]
        public IFormFile? Document { get; set; }
    }
}