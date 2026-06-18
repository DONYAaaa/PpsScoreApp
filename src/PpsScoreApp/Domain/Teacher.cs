using System.ComponentModel.DataAnnotations;

namespace PpsScoreApp.Domain;

/// <summary>Преподаватель (ППС).</summary>
public class Teacher
{
    public int Id { get; set; }

    /// <summary>ФИО в формате «Фамилия И.О.».</summary>
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Кафедра, к которой принадлежит преподаватель.</summary>
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    public List<WorkEntry> Entries { get; set; } = new();
}
