using System.Text;

namespace PpsScoreApp.Services;

/// <summary>
/// Простое журналирование действий пользователей: на каждый день — отдельный
/// текстовый файл в папке «Logs» в корне приложения.
/// </summary>
public class AuditLogger
{
    private readonly string _dir;
    private readonly object _lock = new();

    public AuditLogger(IWebHostEnvironment env, IConfiguration config)
    {
        var path = config["Logging:AuditPath"];
        _dir = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(env.ContentRootPath, "Logs")
            : path;
        Directory.CreateDirectory(_dir);
    }

    public void Log(string? user, string action)
    {
        var now = DateTime.Now;
        var file = Path.Combine(_dir, $"{now:yyyy-MM-dd}.txt");
        var line = $"{now:HH:mm:ss} | {user ?? "—"} | {action}{Environment.NewLine}";
        lock (_lock)
        {
            File.AppendAllText(file, line, Encoding.UTF8);
        }
    }
}
