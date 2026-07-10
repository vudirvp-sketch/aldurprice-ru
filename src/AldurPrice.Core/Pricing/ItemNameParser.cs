using AldurPrice.Core.Contracts;

namespace AldurPrice.Core.Pricing;

/// <summary>
/// Парсер распознанного имени предмета: выделение количества («1x», «шт», trailing-числа),
/// нормализация OCR-искажений (L→1, O→0 и т.д.). M0 — заглушка. Полная реализация в M1.2.
/// </summary>
public sealed class ItemNameParser
{
    /// <summary>Распарсить имя предмета из OCR-текста. M0 stub — возвращает как-is, Quantity=1.</summary>
    public ParsedDetectedItem Parse(string rawName, string language = "rus")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawName);
        return new ParsedDetectedItem(rawName.Trim(), Quantity: 1, Level: null, Language: language);
    }
}
