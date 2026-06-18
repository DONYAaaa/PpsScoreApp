using Microsoft.EntityFrameworkCore;

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

        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(SeedData.Departments());
            await db.SaveChangesAsync();
        }

        if (!await db.WorkTypes.AnyAsync())
        {
            db.WorkTypes.AddRange(SeedData.WorkTypes());
            await db.SaveChangesAsync();
        }
    }
}
