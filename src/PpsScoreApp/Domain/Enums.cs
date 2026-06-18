namespace PpsScoreApp.Domain;

/// <summary>
/// Пять основных разделов индивидуальных показателей работы преподавателя.
/// Значение enum совпадает с номером раздела в форме (1..5).
/// </summary>
public enum WorkSection
{
    UchebnoMetodicheskaya = 1,    // 1. Учебно-методическая работа
    NauchnoIssledovatelskaya = 2, // 2. Научно-исследовательская работа
    PovyshenieKvalifikacii = 3,   // 3. Повышение квалификации
    Vospitatelnaya = 4,           // 4. Воспитательная работа
    Organizacionnaya = 5          // 5. Организационная работа
}

/// <summary>
/// Способ определения баллов за вид работы.
/// </summary>
public enum ScoreInputKind
{
    /// <summary>Фиксированный балл — программа подставляет его сама.</summary>
    Fixed = 0,

    /// <summary>Выбор из заранее заданных вариантов (напр. 3/5/7, 30/50).</summary>
    Choice = 1,

    /// <summary>Произвольный балл вводит пользователь (напр. поручения зав. кафедрой).</summary>
    Manual = 2,

    /// <summary>Баллы = количество × ставка за единицу (напр. 1 балл за 1 практику).</summary>
    Quantity = 3
}

public static class WorkSectionExtensions
{
    public static string Title(this WorkSection s) => s switch
    {
        WorkSection.UchebnoMetodicheskaya => "Учебно-методическая работа",
        WorkSection.NauchnoIssledovatelskaya => "Научно-исследовательская работа",
        WorkSection.PovyshenieKvalifikacii => "Повышение квалификации",
        WorkSection.Vospitatelnaya => "Воспитательная работа",
        WorkSection.Organizacionnaya => "Организационная работа",
        _ => s.ToString()
    };

    public static string FullTitle(this WorkSection s) => $"{(int)s}. {s.Title()}";
}
