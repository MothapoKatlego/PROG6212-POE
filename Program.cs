using LecturerPayrollApp.Data;
using LecturerPayrollApp.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=payroll.db"));
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Seed data - DELETE AND RECREATE DATABASE
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Delete existing database
    db.Database.EnsureDeleted();

    // Create new database with updated schema
    db.Database.EnsureCreated();

    if (!db.Users.Any())
    {
        var lecturer1 = new User { Username = "lecturer1", Password = "password", FirstName = "John", LastName = "Smith", Role = "Lecturer" };
        var lecturer2 = new User { Username = "lecturer2", Password = "password", FirstName = "Sarah", LastName = "Johnson", Role = "Lecturer" };
        var coordinator = new User { Username = "coordinator1", Password = "password", FirstName = "Mike", LastName = "Brown", Role = "Coordinator" };
        var manager = new User { Username = "manager1", Password = "password", FirstName = "Lisa", LastName = "Davis", Role = "Manager" };

        db.Users.AddRange(lecturer1, lecturer2, coordinator, manager);
        db.SaveChanges();

        // Create sample claims WITH Description field
        var claims = new[]
        {
            new MonthlyClaim {
                LecturerId = lecturer1.Id,
                ClaimMonth = new DateTime(2024, 1, 1),
                TotalAmount = 2500,
                Status = ClaimStatus.Submitted,
                SubmittedDate = DateTime.Now.AddDays(-5),
                Description = "January teaching hours"
            },
            new MonthlyClaim {
                LecturerId = lecturer2.Id,
                ClaimMonth = new DateTime(2024, 1, 1),
                TotalAmount = 3200,
                Status = ClaimStatus.Submitted,
                SubmittedDate = DateTime.Now.AddDays(-3),
                Description = "January contract work"
            },
            new MonthlyClaim {
                LecturerId = lecturer1.Id,
                ClaimMonth = new DateTime(2024, 2, 1),
                TotalAmount = 2800,
                Status = ClaimStatus.Submitted,
                SubmittedDate = DateTime.Now.AddDays(-1),
                Description = "February teaching hours"
            }
        };

        db.MonthlyClaims.AddRange(claims);
        db.SaveChanges();

        // Add one approval for testing
        var approval = new Approval
        {
            ClaimId = claims[0].Id,
            ApproverId = coordinator.Id,
            ApproverRole = "Coordinator",
            IsApproved = true,
            Comments = "Approved by coordinator",
            ApprovalDate = DateTime.Now.AddDays(-2)
        };
        db.Approvals.Add(approval);
        db.SaveChanges();
    }
}

app.Run();