using ClosedXML.Excel;
using PpsScoreApp.Domain;
using PpsScoreApp.Models;

namespace PpsScoreApp.Services;

/// <summary>
/// Формирует Excel в виде исходной формы ИП (альбомная, многостраничная):
/// матрица «виды работ × преподаватели», подытоги по разделам, «Итого», подпись зав. кафедрой.
/// Отдельный лист на каждую кафедру. Шапка повторяется на каждой печатной странице.
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
            RenderSheet(ws, report, teachers, g.Key.DepartmentName, g.Key.HeadName);
        }

        if (groups.Count == 0)
            wb.Worksheets.Add("Отчёт").Cell(1, 1).Value = "Нет данных для отчёта.";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void RenderSheet(IXLWorksheet ws, ReportResult report, List<TeacherReport> teachers,
        string deptName, string? headName)
    {
        int lastCol = Math.Max(FirstTeacherCol + teachers.Count - 1, FirstTeacherCol);
        int r = 1;

        // заголовок (только на 1-й странице)
        ws.Cell(r, 1).Value = $"Индивидуальные показатели работы преподавателей за {report.Semester} семестр {report.AcademicYear} учебного года";
        MergeStyled(ws, r, 1, r, lastCol, bold: true, size: 13, center: true, wrap: true);
        ws.Row(r).Height = 34;
        r++;
        ws.Cell(r, 1).Value = $"Кафедра «{deptName}»";
        MergeStyled(ws, r, 1, r, lastCol, bold: false, center: true, wrap: true);
        ws.Row(r).Height = 30;
        r += 2;

        // двухстрочная шапка: №/Виды работ/Баллы (на 2 строки) + «Фамилия И.О...» над именами
        int headTop = r;
        int headBottom = r + 1;

        ws.Cell(headTop, 1).Value = "№";
        ws.Range(headTop, 1, headBottom, 1).Merge();
        ws.Cell(headTop, 2).Value = "Виды работ";
        ws.Range(headTop, 2, headBottom, 2).Merge();
        ws.Cell(headTop, 3).Value = "Баллы";
        ws.Range(headTop, 3, headBottom, 3).Merge();

        if (teachers.Count > 0)
        {
            ws.Cell(headTop, FirstTeacherCol).Value = "Фамилия И.О. штатного преподавателя";
            ws.Range(headTop, FirstTeacherCol, headTop, lastCol).Merge();
            for (int i = 0; i < teachers.Count; i++)
            {
                var cell = ws.Cell(headBottom, FirstTeacherCol + i);
                cell.Value = teachers[i].TeacherName;
                cell.Style.Alignment.TextRotation = 90;   // имена вертикально
            }
            ws.Row(headBottom).Height = 130;
        }

        var hr = ws.Range(headTop, 1, headBottom, lastCol);
        hr.Style.Alignment.WrapText = true;
        hr.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        hr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        r = headBottom + 1;

        // объединения ячейки «№» по группам подпунктов — применяются после вывода всех строк
        var numberMerges = new List<(int from, int to)>();

        // разделы и виды работ
        foreach (WorkSection sec in Enum.GetValues<WorkSection>())
        {
            var types = report.Catalog.Where(w => w.Section == sec).OrderBy(w => w.DisplayOrder).ToList();
            if (types.Count == 0) continue;

            ws.Cell(r, 1).Value = sec.FullTitle();
            ws.Range(r, 1, r, 3).Merge();
            for (int i = 0; i < teachers.Count; i++)
                ws.Cell(r, FirstTeacherCol + i).Value = teachers[i].SectionCappedFor(sec);
            var sr = ws.Range(r, 1, r, lastCol);
            sr.Style.Font.Bold = true;
            sr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            r++;

            for (int k = 0; k < types.Count; k++)
            {
                var w = types[k];

                ws.Cell(r, 1).Value = w.ShowNumber ? w.Number : "";
                ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 2).Value = w.Title;
                ws.Cell(r, 2).Style.Alignment.WrapText = true;
                if (w.Level > 0) ws.Cell(r, 2).Style.Alignment.Indent = w.Level;
                ws.Cell(r, 3).Value = RefPoints(w);
                ws.Cell(r, 3).Style.Alignment.WrapText = true;
                ws.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // по строке-заголовку группы баллы не вносятся — ячейки преподавателей пустые
                if (!w.IsHeader)
                {
                    for (int i = 0; i < teachers.Count; i++)
                    {
                        if (teachers[i].PointsByWorkType.TryGetValue(w.Id, out var pts) && pts != 0)
                        {
                            var c = ws.Cell(r, FirstTeacherCol + i);
                            c.Value = pts;
                            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                    }
                }

                var rr = ws.Range(r, 1, r, lastCol);
                rr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rr.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // как в бланке: номер пункта объединяется по группе подпунктов — но только там,
                // где у подпунктов нет собственных номеров (у 4.4.1–4.4.4 они есть, там merge не нужен)
                if (w.Level == 0)
                {
                    int subs = 0;
                    while (k + subs + 1 < types.Count && types[k + subs + 1].Level > 0) subs++;
                    if (subs > 0 && types.Skip(k + 1).Take(subs).All(x => !x.ShowNumber))
                        numberMerges.Add((r, r + subs));
                }

                r++;
            }
        }

        foreach (var (from, to) in numberMerges)
        {
            var numCell = ws.Range(from, 1, to, 1);
            numCell.Merge();
            numCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            numCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            numCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // строка ИТОГО
        ws.Cell(r, 1).Value = "Итого";
        ws.Range(r, 1, r, 3).Merge();
        for (int i = 0; i < teachers.Count; i++)
            ws.Cell(r, FirstTeacherCol + i).Value = teachers[i].Total;
        var ir = ws.Range(r, 1, r, lastCol);
        ir.Style.Font.Bold = true;
        ir.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ir.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ir.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        int totalRow = r;
        r += 2;

        // подпись зав. кафедрой
        // в подписи — полное название кафедры, как в бланке; аббревиатура идёт только в имя листа
        ws.Cell(r, 1).Value = $"Зав. кафедрой «{deptName}» ____________________ {headName}";
        ws.Range(r, 1, r, lastCol).Merge();
        ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        int lastRow = r;

        // ширины
        ws.Column(1).Width = 5;
        ws.Column(2).Width = 62;
        ws.Column(3).Width = 12;
        for (int i = 0; i < teachers.Count; i++)
            ws.Column(FirstTeacherCol + i).Width = 5;

        // на экране — закрепление шапки и первых столбцов
        ws.SheetView.FreezeRows(headBottom);
        ws.SheetView.FreezeColumns(3);

        // печать: альбом, A4, вписать по ширине в 1 страницу, повтор шапки, подпись внизу
        var ps = ws.PageSetup;
        ps.PageOrientation = XLPageOrientation.Landscape;
        ps.PaperSize = XLPaperSize.A4Paper;
        ps.FitToPages(1, 0);                       // 1 страница в ширину, по высоте — сколько нужно
        ps.SetRowsToRepeatAtTop(headTop, headBottom);
        ps.CenterHorizontally = true;
        ps.Margins.Top = 0.5; ps.Margins.Bottom = 0.5;
        ps.Margins.Left = 0.4; ps.Margins.Right = 0.4;
        ps.Margins.Header = 0.2; ps.Margins.Footer = 0.2;
        ps.Header.Right.AddText("Стр. ");
        ps.Header.Right.AddText(XLHFPredefinedText.PageNumber);
        ps.PrintAreas.Add(1, 1, lastRow, lastCol);
    }

    private static void MergeStyled(IXLWorksheet ws, int r1, int c1, int r2, int c2,
        bool bold = false, int size = 11, bool center = false, bool wrap = false)
    {
        var rng = ws.Range(r1, c1, r2, c2);
        rng.Merge();
        rng.Style.Font.Bold = bold;
        rng.Style.Font.FontSize = size;
        if (center) rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        if (wrap) rng.Style.Alignment.WrapText = true;
        rng.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static string RefPoints(WorkType w) => w.InputKind switch
    {
        ScoreInputKind.Fixed => w.FixedPoints?.ToString("0.##") ?? "",
        ScoreInputKind.Choice => string.Join("/", w.Options.OrderBy(o => o.DisplayOrder).Select(o => o.Points.ToString("0.##"))),
        ScoreInputKind.Quantity => $"{w.UnitPoints?.ToString("0.##")} балл за 1 практику",
        ScoreInputKind.Manual => "",
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