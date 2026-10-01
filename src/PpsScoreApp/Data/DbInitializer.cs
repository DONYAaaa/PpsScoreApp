using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Domain;

namespace PpsScoreApp.Data;

public static class DbInitializer
{
    /// <summary>
    /// Создаёт БД (если нет) и наполняет справочники кафедр и видов работ.
    /// Справочники сидируются только при пустых таблицах — пользовательские данные не трогаются.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        // Для быстрого старта используем EnsureCreated. Чтобы перейти на миграции,
        // удалите эту строку и используйте db.Database.Migrate() (см. README).
        await db.Database.EnsureCreatedAsync();

        await SyncDepartmentsAsync(db);
        await SyncWorkTypesAsync(db);

        // Учётная запись администратора по умолчанию (логин admin)
        if (!await db.Users.AnyAsync(u => u.IsAdmin))
        {
            db.Users.Add(new Domain.AppUser
            {
                Login = "admin",
                DisplayName = "Администратор",
                PasswordHash = Services.PasswordHasher.Hash("88863795"),
                IsAdmin = true,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Приводит справочник кафедр к листу «Кафедры» формы ИП.
    /// Сопоставление по названию: совпавшим обновляются аббревиатура и зав. кафедрой,
    /// недостающие добавляются. Кафедра, которой нет в форме, удаляется только если на ней
    /// не числится ни одного преподавателя — иначе остаётся (связь Teacher → Department Restrict).
    /// Отдельного UI для кафедр нет, справочник живёт только здесь, поэтому такая сверка безопасна.
    /// </summary>
    private static async Task SyncDepartmentsAsync(AppDbContext db)
    {
        var seed = SeedData.Departments();

        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(seed);
            await db.SaveChangesAsync();
            return;
        }

        var existing = await db.Departments.Include(d => d.Teachers).ToListAsync();
        var byName = existing
            .GroupBy(d => d.Name)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var s in seed)
        {
            if (byName.TryGetValue(s.Name, out var cur))
            {
                cur.ShortName = s.ShortName;
                cur.HeadName = s.HeadName;
            }
            else
            {
                db.Departments.Add(s);
            }
        }

        var seedNames = seed.Select(s => s.Name).ToHashSet();
        var obsolete = existing.Where(d => !seedNames.Contains(d.Name) && d.Teachers.Count == 0).ToList();
        if (obsolete.Count > 0) db.Departments.RemoveRange(obsolete);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Приводит справочник видов работ к текущему бланку ИП.
    /// Строки сопоставляются по паре (раздел, номер): существующие обновляются, недостающие
    /// добавляются. Ничего не удаляется — внесённые показатели ссылаются на виды работ.
    /// Благодаря этому обновление формы не требует ручного SQL по строкам справочника
    /// (нужен только ALTER для новых колонок, см. CLAUDE.md).
    /// </summary>
    private static async Task SyncWorkTypesAsync(AppDbContext db)
    {
        var seed = SeedData.WorkTypes();

        if (!await db.WorkTypes.AnyAsync())
        {
            db.WorkTypes.AddRange(seed);
            await db.SaveChangesAsync();
            return;
        }

        var existing = await db.WorkTypes.Include(w => w.Options).ToListAsync();
        var byKey = existing
            .GroupBy(w => (w.Section, w.Number))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var s in seed)
        {
            if (!byKey.TryGetValue((s.Section, s.Number), out var cur))
            {
                db.WorkTypes.Add(s);
                continue;
            }

            cur.Title = s.Title;
            cur.InputKind = s.InputKind;
            cur.FixedPoints = s.FixedPoints;
            cur.UnitPoints = s.UnitPoints;
            cur.UnitName = s.UnitName;
            cur.Hint = s.Hint;
            cur.DisplayOrder = s.DisplayOrder;
            cur.Level = s.Level;
            cur.IsHeader = s.IsHeader;
            cur.ShowNumber = s.ShowNumber;
            SyncOptions(db, cur, s);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Обновляет варианты баллов «на месте» (по порядку), чтобы не пересоздавать их Id.</summary>
    private static void SyncOptions(AppDbContext db, WorkType cur, WorkType seed)
    {
        var curOpts = cur.Options.OrderBy(o => o.DisplayOrder).ThenBy(o => o.Id).ToList();
        var newOpts = seed.Options.OrderBy(o => o.DisplayOrder).ToList();

        for (int i = 0; i < newOpts.Count; i++)
        {
            if (i < curOpts.Count)
            {
                curOpts[i].Label = newOpts[i].Label;
                curOpts[i].Points = newOpts[i].Points;
                curOpts[i].DisplayOrder = i;
            }
            else
            {
                cur.Options.Add(new ScoreOption
                {
                    Label = newOpts[i].Label,
                    Points = newOpts[i].Points,
                    DisplayOrder = i
                });
            }
        }

        if (curOpts.Count > newOpts.Count)
            db.ScoreOptions.RemoveRange(curOpts.Skip(newOpts.Count));
    }
}
