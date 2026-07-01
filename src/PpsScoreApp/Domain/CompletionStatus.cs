using System.ComponentModel.DataAnnotations;

namespace PpsScoreApp.Domain;

/// <summary>Отметка «Заполнено» для преподавателя за конкретный период.</summary>
public class CompletionStatus
{
    public int Id { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    [Required, MaxLength(9)]
    public string AcademicYear { get; set; } = string.Empty;

    public int Semester { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(100)]
    public string? CompletedBy { get; set; }
}
