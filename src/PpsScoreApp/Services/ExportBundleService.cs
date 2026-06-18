using System.IO.Compression;
using PpsScoreApp.Models;

namespace PpsScoreApp.Services;

/// <summary>
/// Собирает выгрузку в один .zip: общий Excel-отчёт (форма-матрица)
/// и по .docx с пояснениями на каждого преподавателя.
/// </summary>
public class ExportBundleService
{
    private readonly ExcelExportService _excel;
    private readonly WordExportService _word;

    public ExportBundleService(ExcelExportService excel, WordExportService word)
    {
        _excel = excel;
        _word = word;
    }

    public byte[] Build(ReportResult report)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var period = $"{report.AcademicYear.Replace('/', '-')}_сем{report.Semester}";

            // общий отчёт
            WriteEntry(zip, $"Отчёт_ИП_{period}.xlsx", _excel.Build(report));

            // пояснения по преподавателям
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in report.Teachers)
            {
                var safe = Sanitize(t.TeacherName);
                var name = safe; int k = 1;
                while (!used.Add(name)) name = $"{safe}_{++k}";
                WriteEntry(zip, $"Пояснения/Пояснения_{name}.docx", _word.Build(t, report));
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

    private static string Sanitize(string raw)
    {
        var cleaned = raw.Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
            cleaned = cleaned.Replace(ch, '_');
        cleaned = cleaned.Replace(' ', '_');
        return string.IsNullOrWhiteSpace(cleaned) ? "Преподаватель" : cleaned;
    }
}
