using System.ComponentModel.DataAnnotations;

namespace PpsScoreApp.Domain;

/// <summary>
/// Замечание проверяющего к таблице преподавателя за период.
/// Привязано к строке (виду работы), а не к WorkEntry: строка может быть пустой
/// («не заполнен п. 2.4»), а очистка строки удаляет WorkEntry, но не замечание.
/// WorkTypeId = null — общее замечание ко всей таблице.
/// </summary>
public class Remark
{
    public int Id { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    [Required, MaxLength(9)]
    public string AcademicYear { get; set; } = string.Empty;

    public int Semester { get; set; }

    public int? WorkTypeId { get; set; }
    public WorkType? WorkType { get; set; }

    [Required, MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? AuthorLogin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Отмечено «Исправлено» (преподавателем или проверяющим).</summary>
    public bool IsResolved { get; set; }

    public DateTime? ResolvedAt { get; set; }

    [MaxLength(100)]
    public string? ResolvedBy { get; set; }
}
