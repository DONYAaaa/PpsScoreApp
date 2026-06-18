using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Data;
using PpsScoreApp.Domain;
using PpsScoreApp.Models;

namespace PpsScoreApp.Services;

public class ReportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ReportService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<ReportResult> BuildAsync(ReportRequest req)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var teachers = await db.Teachers
            .Include(t => t.Department)
            .Where(t => req.TeacherIds.Contains(t.Id))
            .OrderBy(t => t.Department!.Name).ThenBy(t => t.FullName)
            .ToListAsync();

        var entries = await db.WorkEntries
            .Include(e => e.WorkType)
            .Include(e => e.ScoreOption)
            .Where(e => req.TeacherIds.Contains(e.TeacherId)
                        && e.AcademicYear == req.AcademicYear
                        && e.Semester == req.Semester)
            .ToListAsync();

        var catalog = await db.WorkTypes
            .Include(w => w.Options)
            .OrderBy(w => w.Section).ThenBy(w => w.DisplayOrder)
            .ToListAsync();

        var result = new ReportResult
        {
            AcademicYear = req.AcademicYear,
            Semester = req.Semester,
            Catalog = catalog
        };

        foreach (var t in teachers)
        {
            var tr = new TeacherReport
            {
                TeacherId = t.Id,
                TeacherName = t.FullName,
                DepartmentName = t.Department?.Name ?? "",
                DepartmentShort = t.Department?.ShortName,
                HeadName = t.Department?.HeadName,
            };

            var byTeacher = entries.Where(e => e.TeacherId == t.Id).ToList();

            // матрица: суммарные баллы по виду работы
            tr.PointsByWorkType = byTeacher
                .GroupBy(e => e.WorkTypeId)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Points));

            foreach (WorkSection section in Enum.GetValues<WorkSection>())
            {
                var sEntries = byTeacher
                    .Where(e => e.WorkType!.Section == section)
                    .OrderBy(e => e.WorkType!.DisplayOrder)
                    .ToList();

                if (sEntries.Count == 0) continue;

                var block = new ReportSectionBlock { Section = section };
                foreach (var e in sEntries)
                {
                    block.Lines.Add(new ReportEntryLine
                    {
                        Number = e.WorkType!.FullNumber,
                        Title = e.WorkType.Title,
                        Points = e.Points,
                        Detail = BuildDetail(e),
                        Comment = e.Comment,
                    });
                }
                block.RawSum = sEntries.Sum(e => e.Points);
                block.CappedSum = ScoringRules.CapSection(block.RawSum);
                tr.Sections.Add(block);
            }

            tr.RawTotal = tr.Sections.Sum(s => s.CappedSum);
            tr.Total = ScoringRules.CapTotal(tr.RawTotal);
            result.Teachers.Add(tr);
        }

        return result;
    }

    private static string? BuildDetail(WorkEntry e)
    {
        if (e.ScoreOption != null) return e.ScoreOption.Label;
        if (e.Quantity.HasValue) return $"{e.Quantity} × {e.WorkType?.UnitName ?? "ед."}";
        return null;
    }
}
