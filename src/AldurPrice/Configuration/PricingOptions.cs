namespace AldurPrice.Configuration;

/// <summary>
/// Настройки pricing (секция "Pricing" в appsettings.json).
/// </summary>
public sealed class PricingOptions
{
    public string PricingSource { get; init; } = "poe2scout"; // poe2scout / poe.ninja
    public string League { get; init; } = "Runes of Aldur";
    public bool AutoPriceThresholds { get; init; } = true;
    public double RedThreshold { get; init; } = 0.5;
    public double OrangeThreshold { get; init; } = 1.0;
    public double GreenThreshold { get; init; } = 5.0;
    public string DisplayCurrency { get; init; } = "exalt";   // exalt / chaos
    public string DisplayCurrencySuffix { get; init; } = "ru"; // en / ru (суффикс "ex"/"экс")
    public bool CachePersistence { get; init; } = true;
    public int StaleCacheTtlMinutes { get; init; } = 60;
}
