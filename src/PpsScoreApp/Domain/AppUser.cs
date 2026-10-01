using System.ComponentModel.DataAnnotations;

namespace PpsScoreApp.Domain;

/// <summary>Учётная запись пользователя системы.</summary>
public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Login { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>Хеш пароля (PBKDF2, формат «итерации.соль.хеш»).</summary>
    [Required, MaxLength(400)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    /// <summary>
    /// Проверяющий: видит показатели всех преподавателей (только чтение) и пишет замечания.
    /// Совместим с IsAdmin и с привязкой к преподавателю (свою таблицу правит как обычно).
    /// </summary>
    public bool IsReviewer { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Преподаватель, показатели которого правит эта учётная запись.
    /// У администратора — null (доступны все преподаватели).
    /// </summary>
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Name => string.IsNullOrWhiteSpace(DisplayName) ? Login : DisplayName!;

    /// <summary>Видит всех преподавателей (админ — с правкой, проверяющий — на чтение).</summary>
    public bool SeesAllTeachers => IsAdmin || IsReviewer;

    public string RoleTitle => (IsAdmin, IsReviewer) switch
    {
        (true, true) => "Администратор, проверяющий",
        (true, false) => "Администратор",
        (false, true) => "Проверяющий",
        _ => "Пользователь"
    };
}
