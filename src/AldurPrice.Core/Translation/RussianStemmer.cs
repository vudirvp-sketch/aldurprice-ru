namespace AldurPrice.Core.Translation;

/// <summary>
/// Минимальная реализация русского стеммера для M0 (каркас проекта).
/// Снимает типовые окончания: -ой, -ая, -ую, -юю, -ое, -ее, -ие, -ые,
/// -ами, -ями, -ими, -ыми, -ах, -ях, -ы, -и, -е, -о, -у, -ю, -а, -я, -ь.
/// Не удаляет окончание, если после удаления остаётся меньше <see cref="MinStemLength"/> символов.
///
/// <para>Полный Snowball-алгоритм будет портирован в M1.2 (см. docs/05-ROADMAP.md).
/// Этот вариант достаточен для smoke-теста и базовой нормализации падежей.</para>
/// </summary>
public sealed class RussianStemmer
{
    /// <summary>Минимальная длина стема после снятия окончания.</summary>
    public const int MinStemLength = 3;

    // Порядок важен: сначала более длинные окончания, потом более короткие.
    // Записаны в нижнем регистре — вход приводится к lower перед сравнением.
    private static readonly string[] Endings =
    [
        "иями", "оями", "уями", "еями",   // instrumental pl.
        "ами",  "ями",  "ими",  "ыми",    // instrumental pl.
        "ого",  "его",  "ому",  "ему",    // genitive/dat sg.
        "ыми",  "ими",                   // instrumental sg.
        "ах",   "ях",                    // prepositional pl.
        "ой",   "ей",   "аю",   "ею",     // instrumental sg.
        "ая",   "яя",   "ое",   "ее",     // nominative sg. (adj.)
        "ие",   "ые",   "ия",   "ья",     // nominative pl. / fem.
        "ую",   "юю",                    // accusative sg. (adj.)
        "ов",   "ев",   "ам",   "ям",     // genitive/dat pl.
        "ы",    "и",    "а",    "я",      // plural / fem. sg.
        "е",    "о",    "у",    "ю",      // prep/dat sg.
        "ь",    "й"                       // soft sign / й
    ];

    /// <summary>Возвращает базовую форму слова (stem) или само слово, если окончание не снято.</summary>
    public string Stem(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        var lower = word.ToLowerInvariant();
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
