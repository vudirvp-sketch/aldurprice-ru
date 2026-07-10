using System.Text;

namespace AldurPrice.Core.Translation;

/// <summary>
/// Постобработка распознанного OCR-текста для русского языка.
///
/// <para>Применяется <b>после</b> OCR-движка и <b>до</b> <see cref="Pricing.ItemNameParser"/>.
/// Слой отвечает за нормализации, специфичные для кириллицы и типичных OCR-искажений,
/// которые не зависят от предметной логики (количество, уровень) — это забота
/// <see cref="Pricing.ItemNameParser"/>.</para>
///
/// <para>Помещён в <c>AldurPrice.Core</c>, а не в <c>AldurPrice.Ocr</c>, потому что:
/// <list type="bullet">
///   <item>Это чистая текстовая обработка без Windows-зависимостей.</item>
///   <item>Тестируется из <c>AldurPrice.Core.Tests</c> (net9.0, кроссплатформенный).</item>
///   <item>Логически соседствует с <see cref="RussianStemmer"/> и <see cref="Pricing.ItemNameParser"/>.</item>
/// </list></para>
///
/// <para>Список нормализаций (консервативный — лучше недокорректировать, чем сломать):
/// <list type="bullet">
///   <item><b>Ё → Е</b>: Windows OCR часто теряет точки над Ё, Tesseract иногда смешивает.
///         Для matching'а предметных имён Ё и Е эквивалентны (poe2db использует Е).</item>
///   <item><b>Удаление управляющих символов</b> (кроме \t и \n) — OCR иногда вставляет
///         непечатные символы из глифов.</item>
///   <item><b>Нормализация пробелов</b>: множественные → один, tab → пробел, trim.</item>
///   <item><b>Удаление висящих пунктуаций</b>: одинокие «-», «·», «•», «|» в начале/конце строки.</item>
///   <item><b>Замена типографских кавычек</b> («»„"''') на прямые — для совместимости
///         с downstream matching'ом.</item>
///   <item><b>Дефис-минус → дефис</b>: U+2010..U+2015 → U+002D (для consistency с
///         регулярками в ItemNameParser).</item>
///   <item><b>Нормализация апострофов</b>: U+2019, U+2018 → U+0027 (для имён типа «Фаррул»).</item>
/// </list></para>
///
/// <para><b>НЕ делает</b> (намеренно):
/// <list type="bullet">
///   <item>Latin→Cyrillic конверсию — это делает <see cref="Pricing.ItemNameParser"/>
///         (ему нужен контекст количества/уровня).</item>
///   <item>Исправление «0»→«о», «1»→«l» — слишком рискованно без контекста,
///         оставлено на fuzzy matching в <see cref="RuneshapeCombinationTranslator"/> (Levenshtein).</item>
///   <item>Слияние разбитых строк — требует knowing layout'а панели, это работа OcrPipeline.</item>
/// </list></para>
/// </summary>
public sealed class RussianOcrPostProcessor
{
    /// <summary>Обрабатывает одну строку OCR-текста.</summary>
    /// <param name="text">Сырой текст линии из OCR-движка.</param>
    /// <param name="language">Код языка: "rus", "eng". Для не-rus возвращается trim+whitespace-only нормализация.</param>
    /// <returns>Нормализованная строка. Никогда не <c>null</c>; может быть пустой.</returns>
    public string ProcessLine(string text, string language = "rus")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            // 1) Ё/ё → Е/е (только для рус).
            if (string.Equals(language, "rus", StringComparison.OrdinalIgnoreCase))
            {
                if (ch == 'Ё') { sb.Append('Е'); continue; }
                if (ch == 'ё') { sb.Append('е'); continue; }
            }

            // 2) Управляющие символы кроме \t и \n — выкинуть.
            if (char.IsControl(ch) && ch != '\t' && ch != '\n')
                continue;

            // 3) Типографские кавычки → прямые.
            if (ch == '\u00AB' || ch == '\u00BB'    // « »
                || ch == '\u201C' || ch == '\u201D'  // “ ”
                || ch == '\u201E' || ch == '\u201F'  // „ ‟
            )
            {
                sb.Append('"');
                continue;
            }

            // 4) Апострофы → прямой ASCII-апостроф.
            if (ch == '\u2018' || ch == '\u2019' || ch == '\u201B'  // ' ' ‛
                || ch == '\u2032')                                     // ′
            {
                sb.Append('\'');
                continue;
            }

            // 5) Дефис-минусы (U+2010..U+2015) → ASCII дефис.
            if (ch >= '\u2010' && ch <= '\u2015')
            {
                sb.Append('-');
                continue;
            }

            // 6) Различные неразрывные пробелы → обычный пробел.
            if (ch == '\u00A0'    // NBSP
                || ch == '\u2007' // FIGURE SPACE
                || ch == '\u2009' // THIN SPACE
                || ch == '\u200A' // HAIR SPACE
                || ch == '\u202F' // NARROW NO-BREAK SPACE
                || ch == '\u205F' // MEDIUM MATHEMATICAL SPACE
                || ch == '\u3000' // IDEOGRAPHIC SPACE
            )
            {
                sb.Append(' ');
                continue;
            }

            // 7) «•», «·», «◦» и подобные маркеры → пробел (OCR иногда вставляет из UI-декораций).
            if (ch == '\u2022' || ch == '\u00B7' || ch == '\u25E6' || ch == '\u25AA' || ch == '\u25CF')
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(ch);
        }

        // 8) Схлопнуть множественные пробелы, привести tab к пробелу.
        var result = CollapseWhitespace(sb.ToString());

        // 9) Удалить висящие дефисы/пунктуации в начале/конце.
        result = TrimStrayPunctuation(result);

        return result;
    }

    /// <summary>Обрабатывает массив строк (каждую отдельно). Сохраняет порядок.</summary>
    public IReadOnlyList<string> ProcessLines(IEnumerable<string> lines, string language = "rus")
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Select(l => string.IsNullOrWhiteSpace(l) ? string.Empty : ProcessLine(l, language)).ToList();
    }

    private static string CollapseWhitespace(string input)
    {
        var sb = new StringBuilder(input.Length);
        var prevSpace = false;
        foreach (var ch in input)
        {
            if (ch == '\t' || ch == ' ')
            {
                if (!prevSpace)
                    sb.Append(' ');
                prevSpace = true;
            }
            else if (ch == '\n')
            {
                // Сохраняем переводы строк — они значимы для OcrPipeline (разделяют line-ы).
                // Но убираем пробел перед \n.
                if (sb.Length > 0 && sb[^1] == ' ')
                    sb.Length--;
                sb.Append('\n');
                prevSpace = false;
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim('\n', ' ');
    }

    private static string TrimStrayPunctuation(string input)
    {
        // Удалить ведущие/хвостовые одиночные дефисы, точки, запятые, скобки,
        // маркеры списков, если в строке есть хотя бы один «нормальный» символ.
        if (string.IsNullOrEmpty(input))
            return input;

        var start = 0;
        var end = input.Length;
        while (start < end && IsStray(input[start]))
            start++;
        while (end > start && IsStray(input[end - 1]))
            end--;

        return start >= end ? string.Empty : input[start..end];
    }

    private static bool IsStray(char ch) =>
        ch == '-' || ch == '|' || ch == '·' || ch == '•' || ch == '_';
    // Точку (.) НЕ трогаем — может быть в «ур. 20», «1.5ex» и т.п.
    // Дефисы, пайпы, буллеты и подчёркивания — удаляем, если они в начале/конце строки.
}
