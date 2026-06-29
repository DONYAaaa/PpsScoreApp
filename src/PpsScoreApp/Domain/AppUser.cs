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

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Name => string.IsNullOrWhiteSpace(DisplayName) ? Login : DisplayName!;
}
