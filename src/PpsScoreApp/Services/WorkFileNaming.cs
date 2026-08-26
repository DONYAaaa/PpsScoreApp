namespace PpsScoreApp.Services;

/// <summary>
/// Имя файла доказательной базы по требованию инструкции:
/// «Номер пункта_Порядковый номер.*», например «2.4_1.pdf».
/// Расширение берётся из исходного имени файла.
/// </summary>
public static class WorkFileNaming
{
    public static string Build(string fullNumber, int index, string? originalFileName)
    {
        var ext = Path.GetExtension(originalFileName ?? "");
        return $"{fullNumber}_{index}{ext}";
    }
}
