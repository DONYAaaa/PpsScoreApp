using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Data;
using PpsScoreApp.Domain;

namespace PpsScoreApp.Services;

/// <summary>Замечания проверяющего к таблице преподавателя за период.</summary>
public class RemarkService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public RemarkService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    /// <summary>Все замечания преподавателя за период (и к строкам, и общие), старые — первыми.</summary>
    public async Task<List<Remark>> ListAsync(int teacherId, string year, int semester)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Remarks
            .Where(r => r.TeacherId == teacherId && r.AcademicYear == year && r.Semester == semester)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToListAsync();
    }

    public async Task<Remark> AddAsync(int teacherId, string year, int semester, int? workTypeId,
        string text, string? author)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var r = new Remark
        {
            TeacherId = teacherId,
            AcademicYear = year,
            Semester = semester,
            WorkTypeId = workTypeId,
            Text = text.Trim(),
            AuthorLogin = author,
            CreatedAt = DateTime.UtcNow
        };
        db.Remarks.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    public async Task SetResolvedAsync(int id, bool resolved, string? by)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var r = await db.Remarks.FindAsync(id);
        if (r == null) return;
        r.IsResolved = resolved;
        r.ResolvedAt = resolved ? DateTime.UtcNow : null;
        r.ResolvedBy = resolved ? by : null;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var r = await db.Remarks.FindAsync(id);
        if (r == null) return;
        db.Remarks.Remove(r);
        await db.SaveChangesAsync();
    }

    /// <summary>Число открытых замечаний по преподавателям за период (только у кого они есть).</summary>
    public async Task<Dictionary<int, int>> OpenCountsAsync(string year, int semester)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Remarks
            .Where(r => r.AcademicYear == year && r.Semester == semester && !r.IsResolved)
            .GroupBy(r => r.TeacherId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
