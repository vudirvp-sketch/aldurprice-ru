using AldurPrice.Core.Contracts;

namespace AldurPrice.Core.Translation;

/// <summary>
/// Заглушка ItemNameTranslator для M0. Реальная цепочка fallback'ов — M1.2.
/// </summary>
public sealed class ItemNameTranslator : IItemNameTranslator
{
    private readonly RussianStemmer _stemmer = new();

    /// <inheritdoc/>
    public string? TryTranslate(string name, string language = "rus")
    {
        // M0 stub: возвращаем null — перевода нет, цен не будет.
        // В M1.2 будет цепочка:
        //   1) RuneshapeCombinationTranslator (poe2db mapping)
        //   2) TranslationCache (.dat file)
        //   3) rus.ndjson (Exiled Exchange 2, 4319 предметов)
        //   4) translations.json (валюта, fallback)
        //   5) RussianStemmer + Levenshtein (fuzzy matching для OCR-искажений)
        //   6) Return as-is (warning в лог, цен нет)
        return null;
    }
}
