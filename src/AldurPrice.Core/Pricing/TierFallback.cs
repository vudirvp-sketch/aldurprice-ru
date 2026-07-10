namespace AldurPrice.Core.Pricing;

/// <summary>
/// GREATER/PERFECT fallback: если цены на «Greater Orb of X» нет, ищется «Orb of X».
/// M0 — заглушка. Полная реализация в M1.2.
/// </summary>
public sealed class TierFallback
{
    /// <summary>Возвращает базовый ключ без префиксов GREATER / PERFECT. M0 stub.</summary>
    public string? TryBaseKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        // M1.2: strip "Greater ", "Perfect " prefixes и retry в кэше.
        return null;
    }
}
