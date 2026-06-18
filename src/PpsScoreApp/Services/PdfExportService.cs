using PpsScoreApp.Domain;
using PpsScoreApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PpsScoreApp.Services;

public class PdfExportService
{
    public byte[] Build(ReportResult report)
    {
        var doc = Document.Create(container =>
        {
            foreach (var t in report.Teachers)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(s => s.FontSize(10).FontFamily("Times New Roman"));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("Индивидуальные показатели работы преподавателя")
                            .Bold().FontSize(13);
                        col.Item().AlignCenter().Text(report.PeriodTitle).FontSize(10);
                        col.Item().PaddingTop(6).Text($"{t.TeacherName} — кафедра «{t.DepartmentName}»")
                            .Bold().FontSize(11);
                    });

                    page.Content().PaddingVertical(8).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);   // №
                                columns.RelativeColumn(5);    // вид работы
                                columns.RelativeColumn(3);    // пояснение
                                columns.ConstantColumn(45);   // баллы
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("№");
                                header.Cell().Element(HeaderCell).Text("Вид работы");
                                header.Cell().Element(HeaderCell).Text("Пояснение");
                                header.Cell().Element(HeaderCell).Text("Баллы");
                            });

                            foreach (var s in t.Sections)
                            {
                                table.Cell().ColumnSpan(4).Background("#EAF0F8").Padding(3)
                                    .Text(s.Section.FullTitle()).Bold().Italic();

                                foreach (var line in s.Lines)
                                {
                                    table.Cell().Element(BodyCell).Text(line.Number);
                                    var title = line.Title;
                                    if (!string.IsNullOrWhiteSpace(line.Detail)) title += $" ({line.Detail})";
                                    table.Cell().Element(BodyCell).Text(title);
                                    table.Cell().Element(BodyCell).Text(line.Comment ?? "");
                                    table.Cell().Element(BodyCell).AlignRight().Text(line.Points.ToString("0.##"));
                                }

                                table.Cell().ColumnSpan(3).Padding(3).AlignRight()
                                    .Text(s.LimitApplied
                                        ? $"Итого по разделу (лимит 50; до лимита {s.RawSum:0.##}):"
                                        : "Итого по разделу:").SemiBold();
                                table.Cell().Padding(3).AlignRight().Text(s.CappedSum.ToString("0.##")).SemiBold();
                            }

                            table.Cell().ColumnSpan(3).Background("#FFF6DC").Padding(4).AlignRight()
                                .Text("ИТОГО (лимит 100, не менее 0):").Bold();
                            table.Cell().Background("#FFF6DC").Padding(4).AlignRight()
                                .Text(t.Total.ToString("0.##")).Bold();
                        });

                        col.Item().PaddingTop(30).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"Зав. кафедрой «{t.DepartmentShort ?? t.DepartmentName}»");
                                c.Item().PaddingTop(18)
                                    .Text("_______________________ / " + (t.HeadName ?? "_______________") + " /");
                            });
                            row.ConstantItem(140).Border(1).BorderColor("#BBBBBB").Height(90)
                                .AlignCenter().AlignMiddle().Text("М.П.").FontColor("#999999");
                        });
                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("Сформировано: " + report.GeneratedAt.ToString("dd.MM.yyyy") + "   стр. ");
                        x.CurrentPageNumber();
                    });
                });
            }
        });

        return doc.GeneratePdf();

        // стили ячеек (паттерн из доков QuestPDF — без явных типов библиотеки)
        static IContainer HeaderCell(IContainer c) =>
            c.Background("#F2F2F2").BorderBottom(1).BorderColor("#CCCCCC").Padding(3)
             .DefaultTextStyle(x => x.Bold());

        static IContainer BodyCell(IContainer c) =>
            c.BorderBottom(1).BorderColor("#DDDDDD").Padding(3);
    }
}