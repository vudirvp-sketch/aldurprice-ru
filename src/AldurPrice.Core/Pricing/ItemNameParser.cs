using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AldurPrice.Core.Contracts;

namespace AldurPrice.Core.Pricing;

/// <summary>
/// Парсер распознанного имени предмета: выделение количества («1x», «2 шт», trailing-числа),
/// уровня («lvl 20», «ур. 20», «(lvl 20)»), нормализация OCR-искажений.
///
/// <para>OCR-нормализация конвертирует латиницу в кириллицу ТОЛЬКО когда слово
/// содержит хотя бы один кириллический символ. Это предотвращает повреждение
/// legitimately-английских имён (например, клиент мог бы вернуть «Exalted Orb»
/// на английском, если игрок использует EN-клиент). Стратегия: если в слове есть
/// кириллица И латиница, конвертируем Latin-look-alike в Cyrillic.</para>
///
/// <para>Поддерживаемые формат-префиксы/суффиксы количества:
/// <list type="bullet">
///   <item>Leading «1x », «2 x », «3× » (с любым unicode-пробелом)</item>
///   <item>Leading «2 шт», «2 шт.»</item>
///   <item>Trailing « ×5», « x5»</item>
/// </list></para>
///
/// <para>Поддерживаемые форматы уровня:
/// <list type="bullet">
///   <item>«(lvl 20)», «(ур. 20)», «(level 20)»</item>
///   <item>Trailing « lvl 20», « ур. 20»</item>
/// </list></para>
/// </summary>
public sealed class ItemNameParser
{
    // Латиница → кириллица (только визуально похожие глифы).
    // Применяется только внутри слова, уже содержащего кириллицу.
    private static readonly Dictionary<char, char> LatinToCyrillic = new()
    {
        ['a'] = 'а', ['A'] = 'А',
        ['e'] = 'е', ['E'] = 'Е',
        ['o'] = 'о', ['O'] = 'О',
        ['p'] = 'р', ['P'] = 'Р',
        ['c'] = 'с', ['C'] = 'С',
        ['x'] = 'х', ['X'] = 'Х',
        ['y'] = 'у', ['Y'] = 'У',
        ['i'] = 'і', // редко, но бывает в OCR
        ['t'] = 'т', // менее надёжно, но Tesseract часто путает
    };

    // Leading quantity: "1x ", "2 x ", "3× ", "2 шт ", "2 шт. "
    private static readonly Regex LeadingQuantityRegex = new(
        @"^\s*(?<qty>\d+)\s*(?:x|×|шт\.?)\s+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(200));

    // Trailing quantity: " ×5", " x5"
    private static readonly Regex TrailingQuantityRegex = new(
        @"\s*[×x]\s*(?<qty>\d+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(200));

    // Level: "(lvl 20)", "(ур. 20)", "lvl 20", "ур. 20"
    // Поддерживает диапазоны "lvl 18-20".
    private static readonly Regex LevelRegex = new(
        @"(?:\(\s*(?:lvl|ур\.?|level)\s*(?<lvl>\d+(?:\s*-\s*\d+)?)\s*\)|\s+(?:lvl|ур\.?|level)\s*(?<lvl2>\d+(?:\s*-\s*\d+)?)\s*)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(200));

    /// <summary>Распарсить имя предмета из OCR-текста.</summary>
    /// <param name="rawName">Сырой текст из OCR (может содержать количество, уровень).</param>
    /// <param name="language">Код языка клиента: "rus", "eng".</param>
    /// <returns><see cref="ParsedDetectedItem"/> с очищенным Name, Quantity, Level, Language.</returns>
    public ParsedDetectedItem Parse(string rawName, string language = "rus")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawName);

        var text = NormalizeWhitespace(rawName.Trim());
        var quantity = 1;
        int? level = null;

        // 1) Leading quantity: "1x Rune" → ("Rune", 1)
        var m = LeadingQuantityRegex.Match(text);
        if (m.Success)
        {
            quantity = int.Parse(m.Groups["qty"].Value, CultureInfo.InvariantCulture);
            text = text[m.Length..].TrimStart();
        }

        // 2) Trailing quantity: "Rune x5" → ("Rune", 5)
        m = TrailingQuantityRegex.Match(text);
        if (m.Success)
        {
            // Trailing quantity overrides leading if both present (rare in practice).
            quantity = int.Parse(m.Groups["qty"].Value, CultureInfo.InvariantCulture);
            text = text[..m.Index].TrimEnd();
        }

        // 3) Level: "Rune (lvl 20)" → ("Rune", level=20) or "Rune lvl 20" → same.
        m = LevelRegex.Match(text);
        if (m.Success)
        {
            var lvlGroup = m.Groups["lvl"];
            if (!lvlGroup.Success) lvlGroup = m.Groups["lvl2"];
            if (lvlGroup.Success)
            {
                // Если указан диапазон "18-20", берём первое число (min level).
                var lvlStr = lvlGroup.Value;
                var dashIdx = lvlStr.IndexOf('-', StringComparison.Ordinal);
                if (dashIdx >= 0)
                    lvlStr = lvlStr[..dashIdx];
                if (int.TryParse(lvlStr.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var lvl) && lvl >= 0)
                {
                    level = lvl;
                    text = text[..m.Index].TrimEnd();
                }
            }
        }

        // 4) OCR normalization: convert Latin look-alikes to Cyrillic in mixed words.
        if (string.Equals(language, "rus", StringComparison.OrdinalIgnoreCase))
            text = NormalizeLatinToCyrillic(text);

        return new ParsedDetectedItem(text, Quantity: quantity, Level: level, Language: language);
    }

    /// <summary>Нормализует множественные пробелы в один, удаляетleading/trailing.</summary>
    private static string NormalizeWhitespace(string input)
    {
        var sb = new StringBuilder(input.Length);
        var prevSpace = false;
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace)
                    sb.Append(' ');
                prevSpace = true;
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Конвертирует латиницу в кириллицу внутри слов, уже содержащих кириллицу.
    /// Слова без кириллицы (например, чисто английские) не трогает.</summary>
    private static string NormalizeLatinToCyrillic(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);
        var wordBuffer = new StringBuilder();
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (wordBuffer.Length > 0)
                {
                    sb.Append(NormalizeWord(wordBuffer.ToString()));
                    wordBuffer.Clear();
                }
                sb.Append(ch);
            }
            else
            {
                wordBuffer.Append(ch);
            }
        }
        if (wordBuffer.Length > 0)
            sb.Append(NormalizeWord(wordBuffer.ToString()));
        return sb.ToString();
    }

    private static string NormalizeWord(string word)
    {
        // Detect any Cyrillic character.
        var hasCyrillic = false;
        for (var i = 0; i < word.Length; i++)
        {
            if (IsCyrillic(word[i]))
            {
                hasCyrillic = true;
                break;
            }
        }
        if (!hasCyrillic)
            return word;

        // Has Cyrillic — convert any Latin look-alikes to Cyrillic.
        var sb = new StringBuilder(word.Length);
        foreach (var ch in word)
        {
            sb.Append(LatinToCyrillic.TryGetValue(ch, out var cyr) ? cyr : ch);
        }
        return sb.ToString();
    }

    private static bool IsCyrillic(char ch) =>
        (ch >= '\u0400' && ch <= '\u04FF') // Cyrillic block
        || (ch >= '\u0500' && ch <= '\u052F'); // Cyrillic Supplement
}
