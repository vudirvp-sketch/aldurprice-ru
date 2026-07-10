using AldurPrice.Core.Contracts;
using AldurPrice.Core.Pricing;

namespace AldurPrice.Core.Translation;

/// <summary>
/// Переводчик названий предметов: русское имя (из OCR) → английское (для поиска цен).
///
/// <para>Цепочка fallback'ов (см. docs/04-RU-LOCALIZATION.md section 3.1):
/// <list type="number">
///   <item><b>RuneshapeCombinationTranslator</b> — точный/stem/Levenshtein match
///         по bundled poe2db.tw JSON (~150 рунных комбинаций лиги «Руны Альдура»).</item>
///   <item><b>TranslationCache</b> — кэш из .dat файла (M1.5+, сейчас stub).</item>
///   <item><b>rus.ndjson</b> — 4319 базовых предметов из Exiled Exchange 2
///         (M1.5+, файл ещё не bundled).</item>
///   <item><b>translations.json</b> — fallback для валюты (M1.5+).</item>
///   <item><b>Stem + Levenshtein</b> на известных рунных комбинациях — fallback
///         поверх [1] с большим tolerance (по сути уже включён в [1]).</item>
///   <item>Возврат <c>null</c> — цен не будет, в логе warning.</item>
/// </list></para>
///
/// <para>Текущее покрытие (M1.2): только рунные комбинации (около 150 предметов).
/// Все остальные имена возвращают <c>null</c> — это нормально для M1, не баг.
/// Покрытие расширится в M1.5 (Exiled Exchange 2 NDJSON) и последующих итерациях.</para>
/// </summary>
public sealed class ItemNameTranslator : IItemNameTranslator
{
    private readonly RuneshapeCombinationTranslator _runeshape;
    private readonly RussianStemmer _stemmer = new();
    private readonly Levenshtein _levenshtein = new(maxDistance: 2);

    /// <summary>Создаёт переводчик с bundled embedded JSON (DI production path).</summary>
    public ItemNameTranslator()
        : this(new RuneshapeCombinationTranslator())
    {
    }

    /// <summary>Создаёт переводчик с указанным <paramref name="runeshape"/> (test path).</summary>
    public ItemNameTranslator(RuneshapeCombinationTranslator runeshape)
    {
        _runeshape = runeshape;
    }

    /// <inheritdoc/>
    public string? TryTranslate(string name, string language = "rus")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Для английского клиента имя уже canonical — возвращаем как есть.
        if (!string.Equals(language, "rus", StringComparison.OrdinalIgnoreCase))
            return name.Trim();

        var normalized = name.Trim();
        if (normalized.Length == 0)
            return null;

        // [1] Runeshape combinations (highest priority for this league's content).
        if (_runeshape.TryTranslate(normalized) is { } runeshapeEn)
            return runeshapeEn;

        // [2..4] Not yet available — TranslationCache / rus.ndjson / translations.json
        //       land in M1.5+ when the Exiled Exchange 2 NDJSON bundle is added.

        // [5] Stem + Levenshtein на всём словаре рунных комбинаций уже выполнен
        //     внутри _runeshape.TryTranslate (точнее, fallback [3] там).
        //     Расширять fallback на общие предметы без rus.ndjson бессмысленно —
        //     нет кандидатов для matching.

        // [6] No match — return null, pricing layer will skip the item.
        return null;
    }
}
