namespace PpsScoreApp.Services;

/// <summary>Хранение прикреплённых к работам файлов в локальной папке.</summary>
public class FileStorageService
{
    private readonly string _root;

    public FileStorageService(IWebHostEnvironment env, IConfiguration config)
    {
        var path = config["FileStorage:Path"];
        _root = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
            : path;
        Directory.CreateDirectory(_root);
    }

    /// <summary>Сохраняет поток, возвращает уникальное имя файла на диске.</summary>
    public async Task<string> SaveAsync(Stream stream, string originalName)
    {
        var ext = Path.GetExtension(originalName);
        var stored = $"{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(_root, stored);
        await using var fs = new FileStream(full, FileMode.Create);
        await stream.CopyToAsync(fs);
        return stored;
    }

    public string GetFullPath(string storedName) => Path.Combine(_root, storedName);

    public bool Exists(string storedName) => File.Exists(GetFullPath(storedName));

    public void Delete(string? storedName)
    {
        if (string.IsNullOrEmpty(storedName)) return;
        var full = GetFullPath(storedName);
        if (File.Exists(full)) File.Delete(full);
    }

    public async Task<byte[]?> ReadAsync(string storedName)
    {
        var full = GetFullPath(storedName);
        return File.Exists(full) ? await File.ReadAllBytesAsync(full) : null;
    }
}
