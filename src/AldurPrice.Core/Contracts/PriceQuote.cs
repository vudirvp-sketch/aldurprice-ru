namespace AldurPrice.Core.Contracts;

/// <summary>
/// Quote одной позиции предмета: значение в chaos-equivalent и валюты (chaos/divine/exalt).
/// immutable record, безопасен для передачи между потоками.
/// </summary>
public sealed record PriceQuote(
    string Key,
    double ChaosValue,
    double? DivineValue,
    double? ExaltValue,
    int Quantity,
    string League,
    DateTimeOffset UpdatedAt)
{
    public override string ToString() =>
        $"{Key} = {ChaosValue:0.##}c" +
        (DivineValue is { } d ? $" / {d:0.##}d" : "") +
        (ExaltValue is { } e ? $" / {e:0.##}ex" : "");
}
