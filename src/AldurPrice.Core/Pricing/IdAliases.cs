namespace AldurPrice.Core.Pricing;

/// <summary>
/// Алиасы ID валюты: gcp → Gemcutter's Prism, bauble → Glassblower's Bauble, и т.д.
/// M0 — заглушка. Полный маппинг в M1.2.
/// </summary>
public static class IdAliases
{
    private static readonly Dictionary<string, string> AliasToCanonical = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gcp"]    = "Gemcutter's Prism",
        ["bauble"] = "Glassblower's Bauble",
        ["chisel"] = "Cartographer's Chisel",
        ["fuse"]   = "Orb of Fusing",
        ["scour"]  = "Orb of Scouring",
        ["regal"]  = "Regal Orb",
        ["exalted"]= "Exalted Orb",
        ["divine"] = "Divine Orb",
        ["chaos"]  = "Chaos Orb",
    };

    /// <summary>Развернуть алиас в каноническое английское имя. Если алиас неизвестен — вернуть как-is.</summary>
    public static string Resolve(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        return AliasToCanonical.TryGetValue(alias.Trim(), out var canonical) ? canonical : alias;
    }

    /// <summary>Все известные алиасы (для тестов).</summary>
    public static IReadOnlyDictionary<string, string> Known => AliasToCanonical;
}
