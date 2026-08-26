using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Data;
using PpsScoreApp.Models;

namespace PpsScoreApp.Services;

/// <summary>
/// Собирает выгрузку в один .zip: общий Excel-отчёт (форма-матрица) и на каждого
/// преподавателя — папку с пояснениями (.docx) и подпапкой «Приложения» с прикреплёнными файлами.
/// </summary>
public class ExportBundleService
{
    private readonly ExcelExportService _excel;
    private readonly WordExportService _word;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly FileStorageService _storage;

    public ExportBundleService(ExcelExportService excel, WordExportService word,
        IDbContextFactory<AppDbContext> factory, FileStorageService storage)
    {
        _excel = excel;
        _word = word;
        _factory = factory;
        _storage = storage;
    }

    public async Task<byte[]> BuildAsync(ReportResult report)
    {
        // вложения выбранных преподавателей за период
        await using var db = await _factory.CreateDbContextAsync();
        var teacherIds = report.Teachers.Select(t => t.TeacherId).ToList();
        var attachments = await db.WorkFiles
            .Where(f => teacherIds.Contains(f.WorkEntry!.TeacherId)
                        && f.WorkEntry.AcademicYear == report.AcademicYear
                        && f.WorkEntry.Semester == report.Semester)
            .OrderBy(f => f.WorkEntry!.WorkType!.Section)
            .ThenBy(f => f.WorkEntry!.WorkType!.DisplayOrder)
            .ThenBy(f => f.Id)
            .Select(f => new
            {
                f.WorkEntry!.TeacherId,
                f.StoredFileName,
                f.OriginalFileName,
                Section = f.WorkEntry.WorkType!.Section,
                Number = f.WorkEntry.WorkType.Number
            })
            .ToListAsync();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var period = $"{report.AcademicYear.Replace('/', '-')}_сем{report.Semester}";

            // общий отчёт
            WriteEntry(zip, $"Отчёт_ИП_{period}.xlsx", _excel.Build(report));

            var usedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in report.Teachers)
            {
                var baseName = SanitizeName(t.TeacherName);
                var folder = baseName; int k = 1;
                while (!usedFolders.Add(folder)) folder = $"{baseName}_{++k}";

                // пояснения преподавателя
                WriteEntry(zip, $"{folder}/Пояснения_{baseName}.docx", _word.Build(t, report));

                // приложенные файлы: имя по инструкции — «Номер пункта_Порядковый номер.*»
                var usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var indexByPoint = new Dictionary<string, int>();
                foreach (var a in attachments.Where(a => a.TeacherId == t.TeacherId))
                {
                    var bytes = await _storage.ReadAsync(a.StoredFileName!);
                    if (bytes == null) continue;

                    var point = $"{(int)a.Section}.{a.Number}";
                    indexByPoint[point] = indexByPoint.GetValueOrDefault(point) + 1;

                    var fileName = SanitizeFileName(
                        WorkFileNaming.Build(point, indexByPoint[point], a.OriginalFileName ?? a.StoredFileName));
                    var name = fileName; int n = 1;
                    while (!usedFiles.Add(name)) name = AppendSuffix(fileName, ++n);

                    WriteEntry(zip, $"{folder}/Приложения/{name}", bytes);
                }
            }
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] data)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(data, 0, data.Length);
    }

    private static string SanitizeName(string raw)
    {
        var cleaned = (raw ?? "").Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
            cleaned = cleaned.Replace(ch, '_');
        cleaned = cleaned.Replace(' ', '_');
        return string.IsNullOrWhiteSpace(cleaned) ? "Преподаватель" : cleaned;
    }

    private static string SanitizeFileName(string raw)
    {
        var cleaned = (raw ?? "").Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
            cleaned = cleaned.Replace(ch, '_');
        return string.IsNullOrWhiteSpace(cleaned) ? "файл" : cleaned;
    }

    private static string AppendSuffix(string fileName, int n)
    {
        var ext = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{stem}_{n}{ext}";
    }
}
