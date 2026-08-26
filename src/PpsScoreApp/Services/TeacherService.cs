using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Data;
using PpsScoreApp.Domain;

namespace PpsScoreApp.Services;

/// <summary>
/// Справочник преподавателей. Управление вынесено на страницу «Пользователи»
/// (раньше добавление/удаление жило прямо на экране ввода показателей).
/// </summary>
public class TeacherService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly FileStorageService _storage;

    public TeacherService(IDbContextFactory<AppDbContext> factory, FileStorageService storage)
    {
        _factory = factory;
        _storage = storage;
    }

    public async Task<List<Teacher>> ListAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Teachers
            .Include(t => t.Department)
            .OrderBy(t => t.Department!.Name).ThenBy(t => t.FullName)
            .ToListAsync();
    }

    public async Task<Teacher?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Teachers.Include(t => t.Department).FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>Логины учётных записей, привязанных к преподавателям (ключ — TeacherId).</summary>
    public async Task<Dictionary<int, string>> LinkedAccountsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users
            .Where(u => u.TeacherId != null)
            .ToDictionaryAsync(u => u.TeacherId!.Value, u => u.Login);
    }

    /// <summary>Число внесённых работ преподавателя за все периоды.</summary>
    public async Task<int> EntryCountAsync(int teacherId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.WorkEntries.CountAsync(e => e.TeacherId == teacherId);
    }

    public async Task<(bool ok, string? error, Teacher? teacher)> CreateAsync(string fullName, int departmentId)
    {
        fullName = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName)) return (false, "Укажите ФИО.", null);
        if (departmentId == 0) return (false, "Выберите кафедру.", null);

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Teachers.AnyAsync(t => t.FullName == fullName && t.DepartmentId == departmentId))
            return (false, "Такой преподаватель на этой кафедре уже есть.", null);

        var t = new Teacher { FullName = fullName, DepartmentId = departmentId };
        db.Teachers.Add(t);
        await db.SaveChangesAsync();
        return (true, null, t);
    }

    public async Task<(bool ok, string? error)> UpdateAsync(int id, string fullName, int departmentId)
    {
        fullName = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName)) return (false, "Укажите ФИО.");
        if (departmentId == 0) return (false, "Выберите кафедру.");

        await using var db = await _factory.CreateDbContextAsync();
        var t = await db.Teachers.FindAsync(id);
        if (t == null) return (false, "Преподаватель не найден.");

        if (await db.Teachers.AnyAsync(x => x.Id != id && x.FullName == fullName && x.DepartmentId == departmentId))
            return (false, "Такой преподаватель на этой кафедре уже есть.");

        t.FullName = fullName;
        t.DepartmentId = departmentId;
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Удаляет преподавателя вместе с работами. Файлы на диске каскадом не чистятся,
    /// поэтому удаляются здесь до удаления строк (см. CLAUDE.md, п. 4.7).
    /// </summary>
    public async Task<(bool ok, string? error)> DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var teacher = await db.Teachers.FindAsync(id);
        if (teacher == null) return (false, "Преподаватель не найден.");

        var legacy = await db.WorkEntries
            .Where(e => e.TeacherId == id && e.StoredFileName != null)
            .Select(e => e.StoredFileName!)
            .ToListAsync();
        foreach (var f in legacy) _storage.Delete(f);

        var attachments = await db.WorkFiles
            .Where(f => f.WorkEntry!.TeacherId == id)
            .Select(f => f.StoredFileName)
            .ToListAsync();
        foreach (var f in attachments) _storage.Delete(f);

        db.Teachers.Remove(teacher);
        await db.SaveChangesAsync();
        return (true, null);
    }
}
