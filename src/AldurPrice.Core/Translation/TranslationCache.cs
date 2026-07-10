using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AldurPrice.Core.Translation;

/// <summary>
/// In-memory кэш переводов базовых предметов: локализованное имя → английское <c>refName</c>.
///
/// <para><b>Источник данных</b> — <c>ocr/translations/rus.ndjson</c> (embedded resource),
/// генерируется скриптом <c>scripts/update-translations.py</c> из репозитория Exiled Exchange 2.
/// ~4 319 записей на язык: базовые типы, уники, гемы, валюта. Формат записи (одна строка — один JSON-объект):
/// <code>
/// {"name":"Подчиняющая порча","refName":"Abiding Hex","namespace":"GEM","icon":"...","tags":[],"tradeTag":"abiding-hex","w":1,"h":1}
/// </code></para>
///
/// <para><b>Lookup</b> — точный match (OrdinalIgnoreCase). Этого достаточно для базовых предметов:
/// <c>RussianOcrPostProcessor</c> нормализует OCR-выхлоп (Ё→Е, кавычки, пробелы), после чего
/// точное сравнение с 4 319 canonical-именами покрывает подавляющее большинство случаев.
/// Stem/Levenshtein для базовых предметов — M1.10 (после калибровки на реальных скриншотах),
/// для рунных комбинаций уже реализован в <see cref="RuneshapeCombinationTranslator"/>.</para>
///
/// <para><b>Thread safety</b>: <see cref="ConcurrentDictionary{TKey,TValue}"/> — безопасен для
/// параллельных чтений. <see cref="LoadNdjson"/> и <see cref="Clear"/> мутируют коллекцию
/// (ожидается single-writer при startup); читатели во время reload увидят промежуточное состояние,
/// что приемлемо (lookup просто промахнётся и вернёт null — перевод не будет показан, повторится на следующем тике).</para>
///
/// <para><b>DI</b>: default-конструктор для production (грузит embedded NDJSON, если он bundled;
/// иначе остаётся пустым — см. KI-017). Stream-конструктор — для unit-тестов.</para>
/// </summary>
public sealed class TranslationCache
{
    /// <summary>LogicalName embedded-ресурса в сборке <c>AldurPrice.Core</c>.</summary>
    /// <remarks>Не bundled по умолчанию — пользователь запускает <c>scripts/update-translations.py</c>
    /// для загрузки <c>ocr/translations/rus.ndjson</c> (4 319 строк, ~2 МБ). См. KI-017, docs/07-TRANSLATION-SOURCES.md.</remarks>
    private const string EmbeddedResourceName = "AldurPrice.Core.Translation.rus.ndjson";

    private readonly ConcurrentDictionary<string, string> _map =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Создаёт пустой кэш (legacy API, для DI-сценариев с ручным <see cref="LoadNdjson"/>).</summary>
    public TranslationCache()
    {
    }

    /// <summary>Создаёт кэш и грузит NDJSON из указанного потока (test path / explicit load).</summary>
    /// <param name="ndjsonStream">Поток с NDJSON (одна JSON-запись на строку, формат Exiled Exchange 2).</param>
    /// <exception cref="InvalidDataException">Поток не содержит ни одной валидной записи.</exception>
    public TranslationCache(Stream ndjsonStream)
    {
        LoadNdjson(ndjsonStream);
    }

    /// <summary>Создаёт кэш и пытается загрузить embedded <c>rus.ndjson</c> (production path).
    /// Если ресурс не найден (NDJSON ещё не bundled — см. KI-017) — кэш остаётся пустым,
    /// <see cref="Count"/> == 0. Это graceful degradation: перевод рунных комбинаций
    /// через <see cref="RuneshapeCombinationTranslator"/> продолжает работать.
    /// Если NDJSON bundled, но пуст/повреждён — бросает <see cref="InvalidDataException"/>
    /// (fail-fast: явная ошибка данных, пользователь должен увидеть).</summary>
    public static TranslationCache LoadEmbeddedOrDefault()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null)
            return new TranslationCache(); // rus.ndjson не bundled — пустой кэш (KI-017).

        var cache = new TranslationCache();
        cache.LoadNdjson(stream); // бросает InvalidDataException при пустом/повреждённом NDJSON.
        return cache;
    }

    /// <summary>Количество загруженных пар переводов.</summary>
    public int Count => _map.Count;

    /// <summary>Сохранить одну пару переводов (runtime override / legacy API).</summary>
    /// <param name="sourceName">Локализованное имя (ключ).</param>
    /// <param name="targetName">Английское <c>refName</c> (значение).</param>
    public void Store(string sourceName, string targetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        _map[sourceName] = targetName;
    }

    /// <summary>Найти перевод для локализованного имени (точный match, OrdinalIgnoreCase).</summary>
    /// <returns>Английское <c>refName</c> или <c>null</c>, если запись не найдена.</returns>
    public string? TryLookup(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        return _map.TryGetValue(sourceName, out var target) ? target : null;
    }

    /// <summary>Очистить кэш (например, при смене языка клиента).</summary>
    public void Clear() => _map.Clear();

    /// <summary>Загрузить NDJSON из потока: одна JSON-запись на строку (формат Exiled Exchange 2).
    /// Записи с пустым <c>name</c>/<c>refName</c> или где <c>name == refName</c> (нет перевода)
    /// пропускаются. Существующие ключи перезаписываются (last wins).</summary>
    /// <param name="ndjsonStream">UTF-8 поток с NDJSON.</param>
    /// <exception cref="InvalidDataException">Поток не содержит ни одной валидной пары name→refName.</exception>
    public void LoadNdjson(Stream ndjsonStream)
    {
        ArgumentNullException.ThrowIfNull(ndjsonStream);

        var loaded = 0;
        using var reader = new StreamReader(ndjsonStream, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // Пропускаем повреждённые строки вместо падения всего load.
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    continue;

                if (!root.TryGetProperty("name", out var nameEl) ||
                    !root.TryGetProperty("refName", out var refEl))
                    continue;

                var name = nameEl.ValueKind == JsonValueKind.String ? nameEl.GetString() : null;
                var refName = refEl.ValueKind == JsonValueKind.String ? refEl.GetString() : null;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(refName))
                    continue;

                // Пропуск записей без перевода (name == refName, например eng.ndjson).
                if (string.Equals(name, refName, StringComparison.Ordinal))
                    continue;

                _map[name!] = refName!;
                loaded++;
            }
        }

        if (loaded == 0)
            throw new InvalidDataException(
                "NDJSON stream contained no valid name→refName pairs. " +
                "Expected Exiled Exchange 2 format: one JSON object per line with " +
                "'name' and 'refName' string fields.");
    }
}
