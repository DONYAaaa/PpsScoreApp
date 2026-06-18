using Microsoft.EntityFrameworkCore;
using PpsScoreApp.Components;
using PpsScoreApp.Data;
using PpsScoreApp.Services;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community license (бесплатно для организаций с выручкой < $1M)
QuestPDF.Settings.License = LicenseType.Community;

// Blazor (interactive server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// EF Core (фабрика контекстов — безопасно для Blazor Server)
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlServer(conn));

// Сервисы приложения
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<WordExportService>();
builder.Services.AddScoped<ExportBundleService>();
builder.Services.AddSingleton<FileStorageService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Эндпоинт скачивания прикреплённых файлов
app.MapGet("/files/{id:int}", async (int id, IDbContextFactory<AppDbContext> f, FileStorageService storage) =>
{
    await using var db = await f.CreateDbContextAsync();
    var entry = await db.WorkEntries.FindAsync(id);
    if (entry?.StoredFileName == null) return Results.NotFound();
    var bytes = await storage.ReadAsync(entry.StoredFileName);
    if (bytes == null) return Results.NotFound();
    return Results.File(bytes, "application/octet-stream", entry.OriginalFileName ?? entry.StoredFileName);
});

// Создание БД и наполнение справочников
await DbInitializer.InitializeAsync(app.Services);

app.Run();
