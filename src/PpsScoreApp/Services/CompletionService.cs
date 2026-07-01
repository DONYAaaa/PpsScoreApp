using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Data;
using PpsScoreApp.Domain;

namespace PpsScoreApp.Services;

public class CompletionService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CompletionService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<bool> IsCompletedAsync(int teacherId, string year, int semester)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Completions.FirstOrDefaultAsync(x =>
            x.TeacherId == teacherId && x.AcademicYear == year && x.Semester == semester);
        return c?.IsCompleted ?? false;
    }

    public async Task SetAsync(int teacherId, string year, int semester, bool completed, string? by)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Completions.FirstOrDefaultAsync(x =>
            x.TeacherId == teacherId && x.AcademicYear == year && x.Semester == semester);
        if (c == null)
        {
            c = new CompletionStatus { TeacherId = teacherId, AcademicYear = year, Semester = semester };
            db.Completions.Add(c);
        }
        c.IsCompleted = completed;
        c.CompletedAt = completed ? DateTime.UtcNow : null;
        c.CompletedBy = completed ? by : null;
        await db.SaveChangesAsync();
    }

    /// <summary>Id преподавателей, отмеченных «Заполнено» за период.</summary>
    public async Task<HashSet<int>> CompletedIdsAsync(string year, int semester)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var ids = await db.Completions
            .Where(x => x.AcademicYear == year && x.Semester == semester && x.IsCompleted)
            .Select(x => x.TeacherId).ToListAsync();
        return ids.ToHashSet();
    }

    /// <summary>Id преподавателей, у которых есть хотя бы одна запись за период (заполнение начато).</summary>
    public async Task<HashSet<int>> StartedIdsAsync(string year, int semester)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var ids = await db.WorkEntries
            .Where(e => e.AcademicYear == year && e.Semester == semester)
            .Select(e => e.TeacherId).Distinct().ToListAsync();
        return ids.ToHashSet();
    }
}
