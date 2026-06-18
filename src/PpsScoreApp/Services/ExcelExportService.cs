using ClosedXML.Excel;
using PpsScoreApp.Domain;
using PpsScoreApp.Models;

namespace PpsScoreApp.Services;

/// <summary>
/// Формирует Excel в виде исходной формы ИП: матрица «виды работ × преподаватели».
/// Строки — весь каталог видов работ по разделам, столбцы — выбранные преподаватели,
/// в ячейках — набранные баллы. Отдельный лист на каждую кафедру.
/// </summary>
public class ExcelExportService
{
    private const int FirstTeacherCol = 4; // A=№, B=Виды работ, C=Баллы, D…=преподаватели

    public byte[] Build(ReportResult report)
    {
        using var wb = new XLWorkbook();

        var groups = report.Teachers
            .GroupBy(t => new { t.DepartmentName, t.DepartmentShort })
            .ToList();

        int idx = 0;
        foreach (var g in groups)
        {
            idx++;
            var teachers = g.OrderBy(t => t.TeacherName).ToList();
            var ws = wb.Worksheets.Add(SheetName(g.Key.DepartmentShort ?? g.Key.DepartmentName, idx, wb));
            ws.Style.Font.FontName = "Times New Roman";
            ws.Style.Font.FontSize = 11;
            RenderSheet(ws, report, teachers, g.Key.DepartmentName);
        }

        if (groups.Count == 0)
            wb.Worksheets.Add("Отчёт").Cell(1, 1).Value = "Нет данных для отчёта.";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void RenderSheet(IXLWorksheet ws, ReportResult report, List<TeacherReport> teachers, string deptName)
    {
        int lastCol = Math.Max(FirstTeacherCol + teachers.Count - 1, 4);
        int r = 1;

        // заголовок
        ws.Cell(r, 1).Value = $"Индивидуальные показатели работы преподавателей, {report.Semester} семестр {report.AcademicYear} учебного года";
        var tr = ws.Range(r, 1, r, lastCol); tr.Merge();
        tr.Style.Font.Bold = true; tr.Style.Font.FontSize = 13;
        tr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        r++;

        ws.Cell(r, 1).Value = $"Кафедра «{deptName}»";
        var kr = ws.Range(r, 1, r, lastCol); kr.Merge();
        kr.Style.Font.Bold = true;
        kr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        r += 2;

        // шапка таблицы
        int headerRow = r;
        ws.Cell(r, 1).Value = "№";
        ws.Cell(r, 2).Value = "Виды работ";
        ws.Cell(r, 3).Value = "Баллы";
        for (int i = 0; i < teachers.Count; i++)
            ws.Cell(r, FirstTeacherCol + i).Value = teachers[i].TeacherName;
        var hr = ws.Range(headerRow, 1, headerRow, lastCol);
        hr.Style.Font.Bold = true;
        hr.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF7");
        hr.Style.Alignment.WrapText = true;
        hr.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        hr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        r++;

        // разделы и виды работ
        foreach (WorkSection sec in Enum.GetValues<WorkSection>())
        {
            var types = report.Catalog.Where(w => w.Section == sec).OrderBy(w => w.DisplayOrder).ToList();
            if (types.Count == 0) continue;

            // строка-раздел с подытогами по столбцам
            ws.Cell(r, 1).Value = sec.FullTitle();
            ws.Range(r, 1, r, 3).Merge();
            for (int i = 0; i < teachers.Count; i++)
                ws.Cell(r, FirstTeacherCol + i).Value = teachers[i].SectionCappedFor(sec);
            var sr = ws.Range(r, 1, r, lastCol);
            sr.Style.Font.Bold = true;
            sr.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
            sr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            r++;

            foreach (var w in types)
            {
                ws.Cell(r, 1).Value = w.Number;
                ws.Cell(r, 2).Value = w.Title;
                ws.Cell(r, 2).Style.Alignment.WrapText = true;
                ws.Cell(r, 3).Value = RefPoints(w);
                ws.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                for (int i = 0; i < teachers.Count; i++)
                {
                    if (teachers[i].PointsByWorkType.TryGetValue(w.Id, out var pts) && pts != 0)
                    {
                        ws.Cell(r, FirstTeacherCol + i).Value = pts;
                        ws.Cell(r, FirstTeacherCol + i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }
                ws.Range(r, 1, r, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(r, 1, r, lastCol).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                r++;
            }
        }

        // строка ИТОГО
        ws.Cell(r, 1).Value = "Итого";
        ws.Range(r, 1, r, 3).Merge();
        for (int i = 0; i < teachers.Count; i++)
        {
            ws.Cell(r, FirstTeacherCol + i).Value = teachers[i].Total;
            ws.Cell(r, FirstTeacherCol + i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        var ir = ws.Range(r, 1, r, lastCol);
        ir.Style.Font.Bold = true;
        ir.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
        ir.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ir.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // ширины и закрепление
        ws.Column(1).Width = 6;
        ws.Column(2).Width = 70;
        ws.Column(3).Width = 16;
        for (int i = 0; i < teachers.Count; i++)
            ws.Column(FirstTeacherCol + i).Width = 16;

        ws.SheetView.FreezeRows(headerRow);
        ws.SheetView.FreezeColumns(3);
    }

    private static string RefPoints(WorkType w) => w.InputKind switch
    {
        ScoreInputKind.Fixed => w.FixedPoints?.ToString("0.##") ?? "",
        ScoreInputKind.Choice => string.Join("/", w.Options.OrderBy(o => o.DisplayOrder).Select(o => o.Points.ToString("0.##"))),
        ScoreInputKind.Quantity => $"{w.UnitPoints?.ToString("0.##")} за 1 {w.UnitName}",
        ScoreInputKind.Manual => "по решению зав. каф.",
        _ => ""
    };

    private static string SheetName(string raw, int idx, XLWorkbook wb)
    {
        var s = new string(raw.Where(ch => !"\\/?*[]:".Contains(ch)).ToArray()).Trim();
        if (s.Length > 28) s = s.Substring(0, 28);
        if (string.IsNullOrWhiteSpace(s)) s = $"Каф {idx}";
        var name = s; int k = 1;
        while (wb.Worksheets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = $"{s}_{++k}";
        return name;
    }
}
