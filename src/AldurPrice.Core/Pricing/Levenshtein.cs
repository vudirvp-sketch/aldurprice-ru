namespace AldurPrice.Core.Pricing;

/// <summary>
/// Levenshtein distance с early exit по порогу.
/// Используется в RuneshapeCombinationTranslator для OCR-искажений кириллицы (Ш↔Щ, И↔Й и т.д.).
///
/// <para>Реализация классическая (Wagner-Fischer, матрица 1-D),
/// с возвратом maxDistance+1 сразу, как только расстояние превысило порог.</para>
/// </summary>
public sealed class Levenshtein(int maxDistance = 2)
{
    public int MaxDistance { get; } = maxDistance >= 0 ? maxDistance : 0;

    /// <summary>Расстояние Левенштейна между двумя строками. Если &gt; maxDistance — возвращает maxDistance+1.</summary>
    public int Distance(string a, string b)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(a);
        ArgumentException.ThrowIfNullOrWhiteSpace(b);

        if (a.Equals(b, StringComparison.Ordinal))
            return 0;

        var la = a.Length;
        var lb = b.Length;
        if (Math.Abs(la - lb) > MaxDistance)
            return MaxDistance + 1;

        // prev — предыдущая строка матрицы, curr — текущая.
        var prev = new int[lb + 1];
        var curr = new int[lb + 1];

        for (var j = 0; j <= lb; j++)
            prev[j] = j;

        for (var i = 1; i <= la; i++)
        {
            curr[0] = i;
            var rowMin = curr[0];

            for (var j = 1; j <= lb; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
                if (curr[j] < rowMin)
                    rowMin = curr[j];
            }

            if (rowMin > MaxDistance)
                return MaxDistance + 1;

            (prev, curr) = (curr, prev);
        }

        var result = prev[lb];
        return result > MaxDistance ? MaxDistance + 1 : result;
    }
}
