using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Data;
using PpsScoreApp.Domain;

namespace PpsScoreApp.Services;

public class UserService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public UserService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<AppUser>> ListAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users
            .Include(u => u.Teacher).ThenInclude(t => t!.Department)
            .OrderByDescending(u => u.IsAdmin).ThenBy(u => u.Login)
            .ToListAsync();
    }

    /// <summary>
    /// Актуальные права по логину. Берутся из БД, а не из куки, чтобы изменения
    /// привязки администратором действовали без перелогина.
    /// </summary>
    public async Task<AppUser?> GetByLoginAsync(string? login)
    {
        if (string.IsNullOrWhiteSpace(login)) return null;
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users
            .Include(u => u.Teacher).ThenInclude(t => t!.Department)
            .FirstOrDefaultAsync(u => u.Login == login);
    }

    /// <summary>Проверяет логин и пароль. Возвращает пользователя или null.</summary>
    public async Task<AppUser?> ValidateAsync(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password)) return null;
        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login && u.IsActive);
        if (user == null) return null;
        return PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public async Task<(bool ok, string? error)> CreateAsync(string login, string? displayName, string password,
        bool isAdmin, bool isReviewer, int? teacherId)
    {
        login = (login ?? "").Trim();
        if (string.IsNullOrWhiteSpace(login)) return (false, "Укажите логин.");
        if (string.IsNullOrEmpty(password) || password.Length < 4) return (false, "Пароль не короче 4 символов.");
        if (!isAdmin && !isReviewer && teacherId is null)
            return (false, "Выберите преподавателя: обычная учётная запись правит только его показатели.");

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Login == login))
            return (false, "Пользователь с таким логином уже существует.");

        var check = await CheckTeacherAsync(db, teacherId, null);
        if (check != null) return (false, check);

        db.Users.Add(new AppUser
        {
            Login = login,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            IsAdmin = isAdmin,
            IsReviewer = isReviewer,
            IsActive = true,
            TeacherId = teacherId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Меняет привязку учётной записи к преподавателю и роли (администратор, проверяющий).</summary>
    public async Task<(bool ok, string? error)> UpdateAccessAsync(int userId, bool isAdmin, bool isReviewer, int? teacherId)
    {
        if (!isAdmin && !isReviewer && teacherId is null)
            return (false, "Выберите преподавателя: обычная учётная запись правит только его показатели.");

        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return (false, "Пользователь не найден.");

        if (user.IsAdmin && !isAdmin && await db.Users.CountAsync(u => u.IsAdmin && u.IsActive) <= 1)
            return (false, "Нельзя снять права у последнего администратора.");

        var check = await CheckTeacherAsync(db, teacherId, userId);
        if (check != null) return (false, check);

        user.IsAdmin = isAdmin;
        user.IsReviewer = isReviewer;
        user.TeacherId = teacherId;
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Существует ли преподаватель и не занят ли он другой учётной записью.</summary>
    private static async Task<string?> CheckTeacherAsync(AppDbContext db, int? teacherId, int? exceptUserId)
    {
        if (teacherId is not int tid) return null;
        if (!await db.Teachers.AnyAsync(t => t.Id == tid))
            return "Преподаватель не найден.";

        var busy = await db.Users
            .Where(u => u.TeacherId == tid && (exceptUserId == null || u.Id != exceptUserId))
            .Select(u => u.Login)
            .FirstOrDefaultAsync();
        return busy == null ? null : $"К этому преподавателю уже привязана учётная запись «{busy}».";
    }

    public async Task<int> CountAdminsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.CountAsync(u => u.IsAdmin && u.IsActive);
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var u = await db.Users.FindAsync(id);
        if (u != null)
        {
            db.Users.Remove(u);
            await db.SaveChangesAsync();
        }
    }
}
