using PpsScoreApp.Domain;

namespace PpsScoreApp.Services;

/// <summary>
/// Правила лимитов из инструкции (решение Учёного совета, протокол № 13 от 18.07.2013):
/// макс. по разделу — 50 баллов, макс. итого — 100 баллов, итог не может быть меньше 0.
/// </summary>
public static class ScoringRules
{
    public const decimal SectionCap = 50m;
    public const decimal TotalCap = 100m;
    public const decimal TotalFloor = 0m;

    public static decimal CapSection(decimal raw) => Math.Min(raw, SectionCap);

    public static decimal CapTotal(decimal sumOfCappedSections)
    {
        var t = Math.Min(sumOfCappedSections, TotalCap);
        return Math.Max(t, TotalFloor);
    }
}
