using System.Globalization;

namespace AldurPrice.Core.Translation;

/// <summary>
/// Русский стеммер консервативного типа: снимает типовые окончания существительных,
/// прилагательных и глаголов, не вычисляя RV/R1/R2 регионов как полный Snowball.
///
/// <para>Это намеренное решение: полный Snowball Russian (см. https://snowballstem.org/algorithms/russian/stemmer.html)
/// снимает, например, окончание «на» из глагольного списка для слова «руна» → «ру»,
/// что ломает stem-matching имён предметов (ожидалось «рун», не «ру»). Для задач
/// OCR/перевода нам нужна стабильная базовая форма, не максимально агрессивный stem.</para>
///
/// <para>Поведение:
/// <list type="bullet">
///   <item>Приводит к нижнему регистру (InvariantCulture).</item>
///   <item>Если слово короче <see cref="MinStemLength"/> — возвращает как есть.</item>
///   <item>Перебирает окончания от длинных к коротким (список отсортирован по убыванию
///         длины), снимает первое подходящее (если после снятия остаётся
///         ≥ <see cref="MinStemLength"/> символов).</item>
///   <item>Если ни одно окончание не подошло — возвращает исходное слово в нижнем регистре.</item>
/// </list></para>
///
/// <para>Список окончаний покрывает: существительные (падежи, мн.ч.), прилагательные
/// (род/число/падеж), причастия, глаголы прошедшего времени и повелительного наклонения,
/// краткие формы. Этого достаточно для большинства названий предметов PoE2.</para>
///
/// <para>TODO (KI-007): рассмотреть портал полного Snowball Russian с RV-регионами,
/// но с дополнительным правилом, не снимающим глагольные окончания в словах,
/// где R1 пуст. См. STATUS.md → Known Issues.</para>
/// </summary>
public sealed class RussianStemmer
{
    /// <summary>Минимальная длина стема после снятия окончания.</summary>
    public const int MinStemLength = 3;

    // Окончания отсортированы по убыванию длины: длинные проверяются первыми,
    // чтобы снимать наиболее специфичный suffix (например, "ского" вместо "ого"
    // для "русского" → "рус", не "русск").
    //
    // Список покрывает: существительные (падежи, мн.ч.), прилагательные (полные
    // и краткие, на -ский/-цкий), причастия, глаголы прош. времени, инфинитивы.
    //
    // Намеренно НЕ включены: "на", "ло", "ли", "ла" (Snowball verb list) — они
    // пере-стемят "руна"→"ру", "дело"→"де", "родственники"→"родственник".
    // Также НЕ включены "ости" (пере-стемит "новости"→"нов") и "овать"/"евать"
    // (не встречаются в именах предметов PoE2, риск пере-стемминга выше пользы).
    private static readonly string[] Endings =
    [
        // 8-7 chars — verbal noun plural / prepositional
        "ованиями", "ениями", "ованиях",

        // 6 chars — verbal noun sg./genitive pl., abstract noun suffix
        "ование", "ования",
        "ствами", "ества",                      // abstract noun plural forms

        // 5 chars — adjective genitive/dative, instrumental plural, abstract noun
        "ского", "цкого",                       // adj. genitive m/n
        "скому", "цкому",                       // adj. dative m/n
        "скими", "цкими",                       // adj. instrumental plural
        "ство", "ество", "ость",                // abstract noun suffixes
        "ование", "ение", "ание",               // verbal noun singular
        "ования", "ения",                       // verbal noun genitive plural

        // 4 chars — adjective endings, participle, instrumental plural
        "ский", "цкий",                         // adj. m nom sg
        "ская", "цкая",                         // adj. f nom sg
        "ское", "цкое",                         // adj. n nom sg
        "ские", "цкие",                         // adj. pl nom
        "ской", "цкой",                         // adj. fem instr/prep
        "скую", "цкую",                         // adj. fem acc
        "ским", "цким",                         // adj. instr
        "ских", "цких",                         // adj. prep pl
        "иями", "оями", "уями", "еями",         // instrumental pl.
        "ован", "енан",                         // passive participle short

        // 3 chars — short adjective, instrumental, genitive, dative
        "ого", "его",                           // genitive m sg. (adj.)
        "ому", "ему",                           // dative m sg. (adj.)
        "ыми", "ими",                           // instrumental sg./pl. (adj.)
        "ами", "ями",                           // instrumental pl.
        "ах", "ях",                             // prepositional pl.
        "ость", "ство",                         // abstract noun (fallback, also 5-char above)

        // 2 chars — short endings, common case
        "ой", "ей", "аю", "ею",                 // instrumental sg. (adj./noun)
        "ая", "яя", "ое", "ее",                 // nominative sg. (adj.)
        "ие", "ые", "ия", "ья",                 // nominative pl. / fem.
        "ый", "ий",                             // m. adj. nom sg.
        "ую", "юю",                             // accusative sg. (adj.)
        "ью",                                   // fem. instrumental с soft sign
        "ов", "ев", "ам", "ям",                 // genitive/dat pl.
        "ть", "чь",                             // infinitive
        "есь", "ись",                           // reflexive
        "ан", "ен",                             // short participle

        // 1 char — shortest endings, applied last
        "ы", "и", "а", "я",                     // plural / fem. sg.
        "е", "о", "у", "ю",                     // prep/dat sg.
        "ь", "й"                                // soft sign / й
    ];

    /// <summary>Возвращает базовую форму слова (stem) или само слово, если окончание не снято.</summary>
    public string Stem(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        var lower = word.ToLower(CultureInfo.InvariantCulture);
        if (lower.Length <= MinStemLength)
            return lower;

        foreach (var ending in Endings)
        {
            if (lower.Length - ending.Length < MinStemLength)
                continue;

            if (lower.EndsWith(ending, StringComparison.Ordinal))
                return lower[..^ending.Length];
        }

        return lower;
    }
}
