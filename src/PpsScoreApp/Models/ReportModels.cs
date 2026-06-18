using PpsScoreApp.Domain;

namespace PpsScoreApp.Models;

/// <summary>Параметры формирования отчёта.</summary>
public class ReportRequest
{
    public string AcademicYear { get; set; } = string.Empty;
    public int Semester { get; set; }
    public List<int> TeacherIds { get; set; } = new();
}

/// <summary>Строка отчёта по конкретной внесённой работе.</summary>
public class ReportEntryLine
{
    public string Number { get; set; } = string.Empty;     // напр. 1.3
    public string Title { get; set; } = string.Empty;
    public decimal Points { get; set; }
    public string? Detail { get; set; }                    // выбранный вариант / количество
    public string? Comment { get; set; }
}

/// <summary>Раздел в отчёте по преподавателю (с суммами и лимитом).</summary>
public class ReportSectionBlock
{
    public WorkSection Section { get; set; }
    public List<ReportEntryLine> Lines { get; set; } = new();
    public decimal RawSum { get; set; }
    public decimal CappedSum { get; set; }
    public bool LimitApplied => CappedSum != RawSum;
}

/// <summary>Отчёт по одному преподавателю.</summary>
public class TeacherReport
{
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string? DepartmentShort { get; set; }
    public string? HeadName { get; set; }

    public List<ReportSectionBlock> Sections { get; set; } = new();

    public decimal RawTotal { get; set; }
    public decimal Total { get; set; }

    /// <summary>Суммарные баллы по каждому виду работы (WorkTypeId -> сумма). Для матричного Excel.</summary>
    public Dictionary<int, decimal> PointsByWorkType { get; set; } = new();

    /// <summary>Подытог по разделу с учётом лимита 50 (для строки-раздела в матрице).</summary>
    public decimal SectionCappedFor(WorkSection s) =>
        Sections.FirstOrDefault(b => b.Section == s)?.CappedSum ?? 0m;

    /// <summary>Есть ли вообще внесённые работы (хотя бы одна строка).</summary>
    public bool HasEntries => Sections.Any(b => b.Lines.Count > 0);
}

/// <summary>Готовый отчёт.</summary>
public class ReportResult
{
    public string AcademicYear { get; set; } = string.Empty;
    public int Semester { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public List<TeacherReport> Teachers { get; set; } = new();

    /// <summary>Полный каталог видов работ (по разделам), для строк матрицы Excel.</summary>
    public List<WorkType> Catalog { get; set; } = new();

    public string PeriodTitle => $"{AcademicYear} учебный год, {Semester} семестр";
}
