namespace AldurPrice.Core.Contracts;

/// <summary>
/// Распознанный предмет: имя, количество, уровень (если применимо).
/// Производный от OCR-текста результат после парсинга (ItemNameParser).
/// </summary>
public sealed record ParsedDetectedItem(
    string Name,
    int Quantity,
    int? Level,
    string Language)
{
    public override string ToString() =>
        Level is { } lvl ? $"{Name} ×{Quantity} (lvl {lvl})" : $"{Name} ×{Quantity}";
}
