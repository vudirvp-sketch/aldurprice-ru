namespace AldurPrice.Core.Translation;

/// <summary>
/// Переводчик рунных комбинаций (ремнантов) лиги «Runes of Aldur».
/// Загрузчик JSON из poe2db.tw + точное/stem/Levenshtein сопоставление.
/// M0 — заглушка. Полная реализация в M1.2.
/// </summary>
public sealed class RuneshapeCombinationTranslator
{
    private readonly RussianStemmer _stemmer = new();

    /// <summary>Перевести русское название рунной комбинации в английское. M0 stub.</summary>
    public string? TryTranslate(string russianName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(russianName);
        // M1.2: загрузка ocr/runeshape-combinations-ru.json
        // 1) точное совпадение в Dictionary<string,string> (OrdinalIgnoreCase)
        // 2) stem-match через RussianStemmer (для падежей)
        // 3) Levenshtein distance ≤2 (для OCR-искажений)
        return null;
    }

    /// <summary>Количество загруженных комбинаций. M0 stub.</summary>
    public int Count => 0;
}
