using LecturerPayrollApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LecturerPayrollApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<MonthlyClaim> MonthlyClaims { get; set; }
        public DbSet<Approval> Approvals { get; set; }
        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure relationships
            modelBuilder.Entity<MonthlyClaim>()
                .HasOne(mc => mc.Lecturer)
                .WithMany(u => u.Claims)
                .HasForeignKey(mc => mc.LecturerId);

            modelBuilder.Entity<Approval>()
                .HasOne(a => a.Claim)
                .WithMany(mc => mc.Approvals)
                .HasForeignKey(a => a.ClaimId);

            modelBuilder.Entity<Approval>()
                .HasOne(a => a.Approver)
                .WithMany(u => u.Approvals)
                .HasForeignKey(a => a.ApproverId);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.Claim)
                .WithMany(mc => mc.Documents)
                .HasForeignKey(d => d.ClaimId);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.UploadedBy)
                .WithMany(u => u.Documents)
                .HasForeignKey(d => d.UploadedById);
        }
    }
}