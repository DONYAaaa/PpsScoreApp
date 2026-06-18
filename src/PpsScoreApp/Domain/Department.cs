using System.ComponentModel.DataAnnotations;

namespace PpsScoreApp.Domain;

/// <summary>Кафедра.</summary>
public class Department
{
    public int Id { get; set; }

    /// <summary>Полное название, напр. «Мировая экономика и международный бизнес».</summary>
    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Аббревиатура, напр. «МЭиМБ».</summary>
    [MaxLength(80)]
    public string? ShortName { get; set; }

    /// <summary>ФИО заведующего кафедрой в формате «Фамилия И. О.» (для подписи в отчёте).</summary>
    [MaxLength(200)]
    public string? HeadName { get; set; }

    public List<Teacher> Teachers { get; set; } = new();

    public string DisplayShort => string.IsNullOrWhiteSpace(ShortName) ? Name : ShortName!;
}
