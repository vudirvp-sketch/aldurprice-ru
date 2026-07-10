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
///   <item><b>TranslationCache</b> — точный match по bundled <c>rus.ndjson</c>
///         из Exiled Exchange 2 (~4 319 базовых предметов: гемы, валюта, уники).
///         Empty, если NDJSON ещё не bundled (см. KI-017) — тогда покрываются только
///         рунные комбинации из [1]. Stem/Levenshtein для базовых предметов — M1.10
///         (после калибровки на реальных скриншотах).</item>
///   <item><b>translations.json</b> — fallback для валюты (M1.5+, не реализовано).</item>
///   <item>Возврат <c>null</c> — цен не будет, в логе warning.</item>
/// </list></para>
///
/// <para>Текущее покрытие (M1.5-partial): рунные комбинации (~150) + базовые предметы
/// из <c>rus.ndjson</c> (4 319, после запуска <c>scripts/update-translations.py</c>).
/// Все остальные имена возвращают <c>null</c> — это нормально, не баг.</para>
/// </summary>
public sealed class ItemNameTranslator : IItemNameTranslator
{
    private readonly RuneshapeCombinationTranslator _runeshape;
    private readonly TranslationCache? _cache;
    private readonly RussianStemmer _stemmer = new();
    private readonly Levenshtein _levenshtein = new(maxDistance: 2);

    /// <summary>Создаёт переводчик с bundled embedded JSON (runeshape) и
    /// embedded <c>rus.ndjson</c>, если он bundled (DI production path).
    /// Если <c>rus.ndjson</c> не bundled (KI-017) — cache пустой, fallback [2] пропускается.</summary>
    public ItemNameTranslator()
        : this(new RuneshapeCombinationTranslator(), TranslationCache.LoadEmbeddedOrDefault())
    {
    }

    /// <summary>Создаёт переводчик с указанным <paramref name="runeshape"/> и без cache (test path).
    /// Сохранён для обратной совместимости с существующими тестами.</summary>
    public ItemNameTranslator(RuneshapeCombinationTranslator runeshape)
        : this(runeshape, cache: null)
    {
    }

    /// <summary>Создаёт переводчик с указанным <paramref name="runeshape"/> и
    /// <paramref name="cache"/> (test path для проверки цепочки fallback'ов).</summary>
    /// <param name="runeshape">Переводчик рунных комбинаций (fallback [1]).</param>
    /// <param name="cache">Кэш базовых предметов из NDJSON (fallback [2]).
    /// <c>null</c> — пропустить fallback [2] (только рунные комбинации).</param>
    public ItemNameTranslator(RuneshapeCombinationTranslator runeshape, TranslationCache? cache)
    {
        _runeshape = runeshape;
        _cache = cache;
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

        // [2] TranslationCache — базовые предметы из rus.ndjson (Exiled Exchange 2, 4319 шт.).
        //     Точный match (OrdinalIgnoreCase). Empty если rus.ndjson не bundled (KI-017).
        if (_cache is not null && _cache.TryLookup(normalized) is { } cacheEn)
            return cacheEn;

        // [3] translations.json — fallback для валюты (M1.5+, не реализовано в этой итерации).

        // [4] No match — return null, pricing layer will skip the item.
        return null;
    }
}
