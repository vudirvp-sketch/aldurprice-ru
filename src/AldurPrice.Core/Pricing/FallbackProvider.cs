namespace AldurPrice.Core.Pricing;

/// <summary>
/// Fuzzy matching для OCR-искажений и диакритик (cross-language).
/// Использует <see cref="Levenshtein"/> + нормализацию диакритик.
/// M0 — заглушка. Полная реализация в M1.2.
/// </summary>
public sealed class FallbackProvider
{
    /// <summary>Найти ближайшее совпадение в наборе ключей. M0 stub — всегда null.</summary>
    public string? TryFuzzyMatch(string input, IEnumerable<string> candidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        // M1.2: Levenshtein distance ≤2 + stemming + diacritics normalization.
        return null;
    }
}
