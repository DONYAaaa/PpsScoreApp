using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Domain;

namespace PpsScoreApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<WorkType> WorkTypes => Set<WorkType>();
    public DbSet<ScoreOption> ScoreOptions => Set<ScoreOption>();
    public DbSet<WorkEntry> WorkEntries => Set<WorkEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Teacher>()
            .HasOne(t => t.Department)
            .WithMany(d => d.Teachers)
            .HasForeignKey(t => t.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<WorkType>()
            .HasMany(w => w.Options)
            .WithOne(o => o.WorkType!)
            .HasForeignKey(o => o.WorkTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<WorkEntry>()
            .HasOne(e => e.Teacher)
            .WithMany(t => t.Entries)
            .HasForeignKey(e => e.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<WorkEntry>()
            .HasOne(e => e.WorkType)
            .WithMany()
            .HasForeignKey(e => e.WorkTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<WorkEntry>()
            .HasOne(e => e.ScoreOption)
            .WithMany()
            .HasForeignKey(e => e.ScoreOptionId)
            .OnDelete(DeleteBehavior.SetNull);

        // decimal precision
        foreach (var prop in new[] { "FixedPoints", "UnitPoints" })
            b.Entity<WorkType>().Property(prop).HasPrecision(6, 2);
        b.Entity<ScoreOption>().Property(o => o.Points).HasPrecision(6, 2);
        b.Entity<WorkEntry>().Property(e => e.Points).HasPrecision(6, 2);

        b.Entity<Department>().HasIndex(d => d.Name).IsUnique();
        b.Entity<WorkEntry>().HasIndex(e => new { e.TeacherId, e.AcademicYear, e.Semester });
    }
}
