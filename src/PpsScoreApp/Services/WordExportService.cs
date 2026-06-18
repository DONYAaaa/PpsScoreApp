using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PpsScoreApp.Domain;
using PpsScoreApp.Models;

namespace PpsScoreApp.Services;

/// <summary>
/// Формирует отдельный .docx по преподавателю: пояснения к показателям по пунктам.
/// </summary>
public class WordExportService
{
    public byte[] Build(TeacherReport teacher, ReportResult report)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            body.AppendChild(Para("Пояснения к индивидуальным показателям работы преподавателя",
                bold: true, size: 28, just: JustificationValues.Center));
            body.AppendChild(Para(teacher.TeacherName + " — кафедра «" + teacher.DepartmentName + "»",
                bold: true, size: 26, just: JustificationValues.Center));
            body.AppendChild(Para(report.PeriodTitle, size: 24, just: JustificationValues.Center));
            body.AppendChild(Para("", size: 12));

            if (!teacher.HasEntries)
            {
                body.AppendChild(Para("За выбранный период работы не вносились.", italic: true));
            }
            else
            {
                foreach (var s in teacher.Sections)
                {
                    body.AppendChild(Para(s.Section.FullTitle(), bold: true, size: 26));

                    foreach (var line in s.Lines)
                    {
                        var head = $"п. {line.Number} — {line.Title}";
                        if (!string.IsNullOrWhiteSpace(line.Detail)) head += $" ({line.Detail})";
                        head += $" — {line.Points:0.##} б.";
                        body.AppendChild(Para(head, bold: true, indent: 200));

                        var comment = string.IsNullOrWhiteSpace(line.Comment) ? "—" : line.Comment!.Trim();
                        body.AppendChild(Para("Пояснение: " + comment, indent: 460));
                    }

                    var sub = s.LimitApplied
                        ? $"Итого по разделу (с учётом лимита 50; до лимита {s.RawSum:0.##}): {s.CappedSum:0.##}"
                        : $"Итого по разделу: {s.CappedSum:0.##}";
                    body.AppendChild(Para(sub, bold: true, just: JustificationValues.Right));
                    body.AppendChild(Para("", size: 8));
                }

                body.AppendChild(Para($"ИТОГО (лимит 100, не менее 0): {teacher.Total:0.##}",
                    bold: true, size: 26, just: JustificationValues.Right));
            }

            body.AppendChild(Para("", size: 16));
            body.AppendChild(Para($"Зав. кафедрой «{teacher.DepartmentShort ?? teacher.DepartmentName}» " +
                                  "____________________ " + (teacher.HeadName ?? "")));
            body.AppendChild(Para("М.П.", size: 24));
        }

        return ms.ToArray();
    }

    /// <summary>Создаёт абзац с единым форматированием рана (Times New Roman).</summary>
    private static Paragraph Para(string text, bool bold = false, bool italic = false,
        int size = 24, JustificationValues? just = null, int indent = 0)
    {
        var rPr = new RunProperties();
        rPr.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" });
        if (bold) rPr.Append(new Bold());
        if (italic) rPr.Append(new Italic());
        rPr.Append(new FontSize { Val = size.ToString() });          // в полупунктах: 24 = 12pt

        var run = new Run();
        run.Append(rPr);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var pPr = new ParagraphProperties();
        if (indent > 0) pPr.Append(new Indentation { Left = indent.ToString() });   // в твипах
        if (just.HasValue) pPr.Append(new Justification { Val = just.Value });

        var p = new Paragraph();
        p.Append(pPr);
        p.Append(run);
        return p;
    }
}
