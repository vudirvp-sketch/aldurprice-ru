namespace AldurPrice.Core.Contracts;

/// <summary>
/// Снимок рыночных цен с источника (poe2scout / poe.ninja) на момент времени.
/// Содержит набор <see cref="PriceQuote"/> для конкретной лиги.
/// </summary>
public sealed record PricingSnapshot(
    string League,
    string Source,
    IReadOnlyList<PriceQuote> Prices,
    DateTimeOffset FetchedAt)
{
    public static PricingSnapshot Empty(string league, string source) =>
        new(league, source, Array.Empty<PriceQuote>(), DateTimeOffset.UtcNow);
}
