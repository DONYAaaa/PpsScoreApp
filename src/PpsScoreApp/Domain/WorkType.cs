using System.ComponentModel.DataAnnotations;

namespace PpsScoreApp.Domain;

/// <summary>
/// Вид работы из справочника (строка формы ИП). Всего ~70 позиций по 5 разделам.
/// </summary>
public class WorkType
{
    public int Id { get; set; }

    /// <summary>Раздел.</summary>
    public WorkSection Section { get; set; }

    /// <summary>Номер пункта внутри раздела, напр. «1», «3», «4.1».</summary>
    [Required, MaxLength(10)]
    public string Number { get; set; } = string.Empty;

    /// <summary>Описание вида работы (как в форме).</summary>
    [Required, MaxLength(1000)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Способ начисления баллов.</summary>
    public ScoreInputKind InputKind { get; set; }

    /// <summary>Фиксированный балл (для InputKind = Fixed).</summary>
    public decimal? FixedPoints { get; set; }

    /// <summary>Ставка за единицу (для InputKind = Quantity), напр. 1 балл за 1 практику.</summary>
    public decimal? UnitPoints { get; set; }

    /// <summary>Единица измерения для количественного начисления, напр. «практика».</summary>
    [MaxLength(60)]
    public string? UnitName { get; set; }

    /// <summary>Подсказка для пользователя (например пояснение из инструкции).</summary>
    [MaxLength(500)]
    public string? Hint { get; set; }

    /// <summary>Порядок отображения внутри раздела.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Уровень вложенности строки формы: 0 — пункт, 1 — подпункт «–», 2 — подпункт «а)/б)».
    /// Задаёт отступ в таблице ввода и в Excel.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Строка-заголовок группы подпунктов (напр. «Публикация научной статьи (за отчётный период):»).
    /// Баллы по ней не вносятся — они вносятся по вложенным подпунктам.
    /// </summary>
    public bool IsHeader { get; set; }

    /// <summary>
    /// Печатать ли номер в колонке «№». В форме у подпунктов раздела 2 номер общий с пунктом
    /// (ячейка объединена), а у подпунктов 4.4.1–4.4.4 номера свои.
    /// </summary>
    public bool ShowNumber { get; set; } = true;

    /// <summary>Варианты баллов (для InputKind = Choice).</summary>
    public List<ScoreOption> Options { get; set; } = new();

    public string FullNumber => $"{(int)Section}.{Number}";
    public string Display => $"{FullNumber}. {Title}";
}

/// <summary>Вариант начисления баллов для вида работы с переменным баллом.</summary>
public class ScoreOption
{
    public int Id { get; set; }

    public int WorkTypeId { get; set; }
    public WorkType? WorkType { get; set; }

    /// <summary>Балл за этот вариант.</summary>
    public decimal Points { get; set; }

    /// <summary>Подпись варианта, напр. «Разработка впервые».</summary>
    [Required, MaxLength(300)]
    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
