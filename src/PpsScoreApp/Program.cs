using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Mvc;
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

// Авторизация (cookie, без ASP.NET Core Identity)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PpsScoreAuth";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

// Сервисы приложения
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<WordExportService>();
builder.Services.AddScoped<ExportBundleService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddSingleton<AuditLogger>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// --- Вход / выход (куку можно выставить только из HTTP-запроса, не из circuit) ---
app.MapPost("/auth/login", async (HttpContext http, UserService users, AuditLogger audit,
    [FromForm] string login, [FromForm] string password, [FromForm] string? returnUrl) =>
{
    var user = await users.ValidateAsync(login, password);
    if (user == null)
    {
        audit.Log(login, "Неудачная попытка входа");
        return Results.Redirect("/login?error=1");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Login),
        new("DisplayName", user.Name),
    };
    if (user.IsAdmin) claims.Add(new(ClaimTypes.Role, "Admin"));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });

    audit.Log(user.Login, "Вход в систему");
    return Results.LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
}).DisableAntiforgery();

app.MapPost("/auth/logout", async (HttpContext http, AuditLogger audit) =>
{
    audit.Log(http.User.Identity?.Name, "Выход из системы");
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).DisableAntiforgery();

// Скачивание прикреплённых файлов (только авторизованным)
app.MapGet("/files/{id:int}", async (int id, IDbContextFactory<AppDbContext> f, FileStorageService storage) =>
{
    await using var db = await f.CreateDbContextAsync();
    var entry = await db.WorkEntries.FindAsync(id);
    if (entry?.StoredFileName == null) return Results.NotFound();
    var bytes = await storage.ReadAsync(entry.StoredFileName);
    if (bytes == null) return Results.NotFound();
    return Results.File(bytes, "application/octet-stream", entry.OriginalFileName ?? entry.StoredFileName);
}).RequireAuthorization();

// Создание БД и наполнение справочников
await DbInitializer.InitializeAsync(app.Services);

app.Run();
