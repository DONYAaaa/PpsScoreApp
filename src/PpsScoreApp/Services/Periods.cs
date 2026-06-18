namespace PpsScoreApp.Services;

public static class Periods
{
    /// <summary>Текущий учебный год в формате «2025/26» (с сентября начинается новый).</summary>
    public static string CurrentAcademicYear()
    {
        var now = DateTime.Now;
        int start = now.Month >= 9 ? now.Year : now.Year - 1;
        return Format(start);
    }

    public static string Format(int startYear) => $"{startYear}/{(startYear + 1) % 100:D2}";

    /// <summary>Список учебных годов: несколько назад и один вперёд от текущего.</summary>
    public static List<string> List(int back = 4, int forward = 1)
    {
        var now = DateTime.Now;
        int curStart = now.Month >= 9 ? now.Year : now.Year - 1;
        var res = new List<string>();
        for (int y = curStart + forward; y >= curStart - back; y--)
            res.Add(Format(y));
        return res;
    }

    public static readonly int[] Semesters = { 1, 2 };
}
