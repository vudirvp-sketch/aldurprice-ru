using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AldurPrice.Core.Pricing;

namespace AldurPrice.Core.Translation;

/// <summary>
/// Переводчик рунных комбинаций лиги «Runes of Aldur».
///
/// <para>Источник данных — <c>ocr/runeshape-combinations-ru.json</c> (embedded resource),
/// генерируется скриптом <c>scripts/parse-poe2db-runeshapes.py</c> из poe2db.tw.
/// ~150 записей: base runes, alloys, lineage runes, ward runes, ancient runes,
/// master rune, special runes.</para>
///
/// <para>Цепочка matching'а:
/// <list type="number">
///   <item><b>Exact match</b> — OrdinalIgnoreCase сравнение full name.</item>
///   <item><b>Stem match</b> — оба слова (input и candidate) обрабатываются
///         <see cref="RussianStemmer"/>-ом слово-за-словом; stem-ключи
///         сравниваются на равенство. Ловит падежи: «Руной огня» → stem-key
///         «рун огн» == stem-key от «Руна огня».</item>
///   <item><b>Levenshtein</b> — если exact и stem не сработали, ищем ближайший
///         candidate с distance ≤ 2. Ловит OCR-искажения кириллицы:
///         «Руна агня» → «Руна огня» (distance=1).</item>
/// </list></para>
///
/// <para>DI: используйте default-constructor для production (грузит embedded JSON).
/// Для тестов — конструктор с параметром <see cref="Stream"/> (inline JSON).</para>
/// </summary>
public sealed class RuneshapeCombinationTranslator
{
    private const string EmbeddedResourceName =
        "AldurPrice.Core.Translation.runeshape-combinations-ru.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly Dictionary<string, string> _ruToEn;       // exact, OrdinalIgnoreCase
    private readonly Dictionary<string, string> _stemKeyToEn;  // stem-of-ru → en
    private readonly List<KeyValuePair<string, string>> _ruEntries; // for Levenshtein
    private readonly RussianStemmer _stemmer = new();
    private readonly Levenshtein _levenshtein = new(maxDistance: 2);

    /// <summary>Создаёт переводчик, грузя bundled embedded JSON (production path).</summary>
    /// <exception cref="InvalidDataException">JSON не найден или повреждён.</exception>
    public RuneshapeCombinationTranslator()
        : this(LoadEmbedded())
    {
    }

    /// <summary>Создаёт переводчик из произвольного потока JSON (test path).</summary>
    /// <exception cref="JsonException">JSON повреждён.</exception>
    public RuneshapeCombinationTranslator(Stream jsonStream)
    {
        var doc = JsonSerializer.Deserialize<RuneshapeDocument>(jsonStream, JsonOpts)
            ?? throw new InvalidDataException("Runeshape combinations JSON deserialized to null.");
        if (doc.Combinations is null || doc.Combinations.Count == 0)
            throw new InvalidDataException("Runeshape combinations JSON contains no entries.");

        _ruToEn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _stemKeyToEn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _ruEntries = new List<KeyValuePair<string, string>>(doc.Combinations.Count);

        foreach (var entry in doc.Combinations)
        {
            if (string.IsNullOrWhiteSpace(entry.Ru) || string.IsNullOrWhiteSpace(entry.En))
                continue;

            var ruNorm = entry.Ru.Trim();
            var enNorm = entry.En.Trim();

            // Exact-match map (first wins on duplicate; poe2db hrefs are unique).
            if (!_ruToEn.ContainsKey(ruNorm))
                _ruToEn[ruNorm] = enNorm;

            // Stem-key map. For "Руна огня" → stem key "рун огн".
            var stemKey = BuildStemKey(ruNorm);
            if (!_stemKeyToEn.ContainsKey(stemKey))
                _stemKeyToEn[stemKey] = enNorm;

            // Levenshtein candidate list (lowercased for case-insensitive distance).
            _ruEntries.Add(new KeyValuePair<string, string>(
                ruNorm.ToLower(CultureInfo.InvariantCulture), enNorm));
        }
    }

    /// <summary>Количество загруженных комбинаций.</summary>
    public int Count => _ruToEn.Count;

    /// <summary>Перевести русское название рунной комбинации в английское.</summary>
    /// <param name="russianName">Имя с OCR (любой регистр, любая форма падежа).</param>
    /// <returns>Английское имя или <c>null</c>, если matching не сработал.</returns>
    public string? TryTranslate(string russianName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(russianName);

        var normalized = russianName.Trim();

        // [1] Exact match (case-insensitive).
        if (_ruToEn.TryGetValue(normalized, out var en))
            return en;

        // [2] Stem match (handles падежи).
        var stemKey = BuildStemKey(normalized);
        if (!string.IsNullOrEmpty(stemKey) && _stemKeyToEn.TryGetValue(stemKey, out en))
            return en;

        // [3] Levenshtein (handles OCR-искажения кириллицы).
        var lower = normalized.ToLower(CultureInfo.InvariantCulture);
        var bestDist = int.MaxValue;
        string? best = null;
        foreach (var (ruLower, enName) in _ruEntries)
        {
            var d = _levenshtein.Distance(lower, ruLower);
            if (d < bestDist)
            {
                bestDist = d;
                best = enName;
                if (d == 0) break;
            }
        }
        return bestDist <= _levenshtein.MaxDistance ? best : null;
    }

    /// <summary>Строит stem-ключ: каждое слово проходит через <see cref="RussianStemmer"/>,
    /// результаты объединяются пробелом. «Руной огня» → «рун огн».</summary>
    private string BuildStemKey(string input)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(_stemmer.Stem(words[i]));
        }
        return sb.ToString();
    }

    private static Stream LoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidDataException(
                $"Embedded resource '{EmbeddedResourceName}' not found. " +
                "Ensure ocr/runeshape-combinations-ru.json is included as EmbeddedResource " +
                "in AldurPrice.Core.csproj.");
        return stream;
    }

    private sealed class RuneshapeDocument
    {
        public int Version { get; set; }
        public string? Source { get; set; }
        public string? FetchedAt { get; set; }
        public int Count { get; set; }
        public List<RuneshapeEntry> Combinations { get; set; } = new();
    }

    private sealed class RuneshapeEntry
    {
        public string En { get; set; } = string.Empty;
        public string Ru { get; set; } = string.Empty;
        public string? Href { get; set; }
        public string? Tier { get; set; }
    }
}
