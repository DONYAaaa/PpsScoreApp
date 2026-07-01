using System.ComponentModel.DataAnnotations;

namespace PpsScoreApp.Domain;

/// <summary>
/// Внесённая работа: конкретный вид работы, выполненный преподавателем в заданном периоде.
/// </summary>
public class WorkEntry
{
    public int Id { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int WorkTypeId { get; set; }
    public WorkType? WorkType { get; set; }

    /// <summary>Учебный год в формате «2025/26».</summary>
    [Required, MaxLength(9)]
    public string AcademicYear { get; set; } = string.Empty;

    /// <summary>Семестр: 1 или 2.</summary>
    public int Semester { get; set; }

    /// <summary>Итоговые баллы за эту запись (с учётом выбранного варианта / количества).</summary>
    public decimal Points { get; set; }

    /// <summary>Выбранный вариант (для Choice), если применимо.</summary>
    public int? ScoreOptionId { get; set; }
    public ScoreOption? ScoreOption { get; set; }

    /// <summary>Количество (для Quantity), напр. число практик.</summary>
    public int? Quantity { get; set; }

    /// <summary>Опциональный комментарий-пояснение.</summary>
    [MaxLength(2000)]
    public string? Comment { get; set; }

    /// <summary>Имя сохранённого файла на диске (относительно папки загрузок).</summary>
    [MaxLength(260)]
    public string? StoredFileName { get; set; }

    /// <summary>Исходное имя файла (для отображения/скачивания).</summary>
    [MaxLength(260)]
    public string? OriginalFileName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Прикреплённые файлы (несколько на один вид работы).</summary>
    public List<WorkFile> WorkFiles { get; set; } = new();

    public string Period => $"{AcademicYear}, {Semester} сем.";
    public bool HasFile => !string.IsNullOrEmpty(StoredFileName);
}
