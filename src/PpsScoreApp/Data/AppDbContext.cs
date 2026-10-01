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
    public DbSet<WorkFile> WorkFiles => Set<WorkFile>();
    public DbSet<CompletionStatus> Completions => Set<CompletionStatus>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Remark> Remarks => Set<Remark>();

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

        b.Entity<WorkEntry>()
            .HasMany(e => e.WorkFiles)
            .WithOne(f => f.WorkEntry!)
            .HasForeignKey(f => f.WorkEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CompletionStatus>()
            .HasOne(c => c.Teacher)
            .WithMany()
            .HasForeignKey(c => c.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        // Привязка учётной записи к преподавателю. Удаление преподавателя не удаляет
        // учётку — она просто остаётся без привязки (админ переназначит).
        b.Entity<AppUser>()
            .HasOne(u => u.Teacher)
            .WithMany()
            .HasForeignKey(u => u.TeacherId)
            .OnDelete(DeleteBehavior.SetNull);

        // Замечания проверяющего: удаляются вместе с преподавателем.
        b.Entity<Remark>()
            .HasOne(r => r.Teacher)
            .WithMany()
            .HasForeignKey(r => r.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Remark>()
            .HasOne(r => r.WorkType)
            .WithMany()
            .HasForeignKey(r => r.WorkTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // decimal precision
        foreach (var prop in new[] { "FixedPoints", "UnitPoints" })
            b.Entity<WorkType>().Property(prop).HasPrecision(6, 2);
        b.Entity<ScoreOption>().Property(o => o.Points).HasPrecision(6, 2);
        b.Entity<WorkEntry>().Property(e => e.Points).HasPrecision(6, 2);

        b.Entity<Department>().HasIndex(d => d.Name).IsUnique();
        // один вид работы у преподавателя за период — ровно одна запись.
        // Уникальность нужна против гонки в автосохранении: два одновременных
        // вызова могли вставить два WorkEntry на один WorkTypeId, и тогда таблица
        // ввода показывала первую запись, а отчёт — сумму всех.
        b.Entity<WorkEntry>()
            .HasIndex(e => new { e.TeacherId, e.AcademicYear, e.Semester, e.WorkTypeId })
            .IsUnique();
        b.Entity<AppUser>().HasIndex(u => u.Login).IsUnique();
        // один преподаватель — не более одной учётной записи
        b.Entity<AppUser>().HasIndex(u => u.TeacherId).IsUnique()
            .HasFilter("[TeacherId] IS NOT NULL");
        b.Entity<CompletionStatus>().HasIndex(c => new { c.TeacherId, c.AcademicYear, c.Semester }).IsUnique();
        b.Entity<Remark>().HasIndex(r => new { r.TeacherId, r.AcademicYear, r.Semester });
    }
}
