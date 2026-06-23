using ClosedXML.Excel;
using PpsScoreApp.Domain;
using PpsScoreApp.Models;

namespace PpsScoreApp.Services;

/// <summary>
/// Формирует Excel в виде исходной формы ИП: матрица «виды работ × преподаватели».
/// Альбомная A4, повторяющаяся шапка, подпись зав. кафедрой внизу.
/// Отдельный лист на каждую кафедру.
/// </summary>
public class ExcelExportService
{
    private const int FirstTeacherCol = 4; // A=№, B=Виды работ, C=Баллы, D…=преподаватели

    public byte[] Build(ReportResult report)
    {
        using var wb = new XLWorkbook();

        var groups = report.Teachers
            .GroupBy(t => new { t.DepartmentName, t.DepartmentShort, t.HeadName })
            .ToList();

        int idx = 0;
        foreach (var g in groups)
        {
            idx++;
            var teachers = g.OrderBy(t => t.TeacherName).ToList();
            var ws = wb.Worksheets.Add(SheetName(g.Key.DepartmentShort ?? g.Key.DepartmentName, idx, wb));
            ws.Style.Font.FontName = "Times New Roman";
            ws.Style.Font.FontSize = 11;
            RenderSheet(ws, report, teachers, g.Key.DepartmentName, g.Key.DepartmentShort, g.Key.HeadName);
        }

        if (groups.Count == 0)
            wb.Worksheets.Add("Отчёт").Cell(1, 1).Value = "Нет данных для отчёта.";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void RenderSheet(IXLWorksheet ws, ReportResult report, List<TeacherReport> teachers,
        string deptName, string? deptShort, string? headName)
    {
        int lastCol = Math.Max(FirstTeacherCol + teachers.Count - 1, FirstTeacherCol);
        int r = 1;

        // заголовок (только на 1-й странице)
        ws.Cell(r, 1).Value = $"Индивидуальные показатели работы преподавателя за {report.Semester} семестр {report.AcademicYear} учебного года";
        Merge(ws, r, 1, r, lastCol, bold: true, size: 13, center: true);
        r++;

        ws.Cell(r, 1).Value = $"Кафедра «{deptName}»";
        Merge(ws, r, 1, r, lastCol, bold: true, center: true);
        r += 2;

        // ---- двухстрочная шапка ----
        int captionRow = r;
        int namesRow = r + 1;

        ws.Cell(captionRow, 1).Value = "№";
        ws.Range(captionRow, 1, namesRow, 1).Merge();
        ws.Cell(captionRow, 2).Value = "Виды работ";
        ws.Range(captionRow, 2, namesRow, 2).Merge();
        ws.Cell(captionRow, 3).Value = "Баллы";
        ws.Range(captionRow, 3, namesRow, 3).Merge();

        ws.Cell(captionRow, FirstTeacherCol).Value = "Фамилия И.О. штатного преподавателя";
        ws.Range(captionRow, FirstTeacherCol, captionRow, lastCol).Merge();
        for (int i = 0; i < teachers.Count; i++)
            ws.Cell(namesRow, FirstTeacherCol + i).Value = teachers[i].TeacherName;

        var hr = ws.Range(captionRow, 1, namesRow, lastCol);
        hr.Style.Font.Bold = true;
        hr.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF7");
        hr.Style.Alignment.WrapText = true;
        hr.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        hr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        r = namesRow + 1;

        // ---- разделы и виды работ ----
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
            sr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            r++;

            foreach (var w in types)
            {
                ws.Cell(r, 1).Value = w.Number;
                ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 2).Value = w.Title;
                ws.Cell(r, 2).Style.Alignment.WrapText = true;
                ws.Cell(r, 3).Value = RefPoints(w);
                ws.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                for (int i = 0; i < teachers.Count; i++)
                {
                    if (teachers[i].PointsByWorkType.TryGetValue(w.Id, out var pts) && pts != 0)
                        ws.Cell(r, FirstTeacherCol + i).Value = pts;
                    ws.Cell(r, FirstTeacherCol + i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                var row = ws.Range(r, 1, r, lastCol);
                row.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                row.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                row.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                r++;
            }
        }

        // ---- строка ИТОГО ----
        ws.Cell(r, 1).Value = "Итого";
        ws.Range(r, 1, r, 3).Merge();
        for (int i = 0; i < teachers.Count; i++)
            ws.Cell(r, FirstTeacherCol + i).Value = teachers[i].Total;
        var ir = ws.Range(r, 1, r, lastCol);
        ir.Style.Font.Bold = true;
        ir.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
        ir.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ir.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ir.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        r += 2;

        // ---- подпись зав. кафедрой ----
        ws.Cell(r, 1).Value = $"Зав. кафедрой «{deptShort ?? deptName}» ____________________ {headName}";
        ws.Range(r, 1, r, lastCol).Merge();
        ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        // ---- ширины колонок ----
        ws.Column(1).Width = 5;
        ws.Column(2).Width = 55;
        ws.Column(3).Width = 9;
        for (int i = 0; i < teachers.Count; i++)
            ws.Column(FirstTeacherCol + i).Width = 11;

        // ---- печать: альбомная A4, по ширине 1 страница, повтор шапки ----
        var ps = ws.PageSetup;
        ps.PageOrientation = XLPageOrientation.Landscape;
        ps.PaperSize = XLPaperSize.A4Paper;
        ps.PagesWide = 1;
        ps.PagesTall = 0;            // по высоте — сколько нужно
        ps.Margins.Top = 0.5;
        ps.Margins.Bottom = 0.5;
        ps.Margins.Left = 0.4;
        ps.Margins.Right = 0.4;
        ps.CenterHorizontally = true;
        ps.SetRowsToRepeatAtTop(captionRow, namesRow);

        ws.SheetView.FreezeRows(namesRow);
        ws.SheetView.FreezeColumns(3);
    }

    private static void Merge(IXLWorksheet ws, int r1, int c1, int r2, int c2,
        bool bold = false, int size = 0, bool center = false)
    {
        var rng = ws.Range(r1, c1, r2, c2);
        rng.Merge();
        if (bold) rng.Style.Font.Bold = true;
        if (size > 0) rng.Style.Font.FontSize = size;
        if (center) rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
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