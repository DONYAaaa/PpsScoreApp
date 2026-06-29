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
        return await db.Users.OrderByDescending(u => u.IsAdmin).ThenBy(u => u.Login).ToListAsync();
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

    public async Task<(bool ok, string? error)> CreateAsync(string login, string? displayName, string password, bool isAdmin)
    {
        login = (login ?? "").Trim();
        if (string.IsNullOrWhiteSpace(login)) return (false, "Укажите логин.");
        if (string.IsNullOrEmpty(password) || password.Length < 4) return (false, "Пароль не короче 4 символов.");

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Login == login))
            return (false, "Пользователь с таким логином уже существует.");

        db.Users.Add(new AppUser
        {
            Login = login,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            IsAdmin = isAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (true, null);
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
