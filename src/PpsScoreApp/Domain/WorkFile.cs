using System.ComponentModel.DataAnnotations;

namespace PpsScoreApp.Domain;

/// <summary>Файл-приложение к внесённой работе. Записей может быть несколько на одну WorkEntry.</summary>
public class WorkFile
{
    public int Id { get; set; }

    public int WorkEntryId { get; set; }
    public WorkEntry? WorkEntry { get; set; }

    [Required, MaxLength(260)]
    public string StoredFileName { get; set; } = string.Empty;

    [MaxLength(260)]
    public string? OriginalFileName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
