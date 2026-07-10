namespace AldurPrice.Core.Contracts;

/// <summary>
/// Переводчик названий предметов: русское имя (из OCR) → английское (для поиска цен).
/// Реализация — <c>AldurPrice.Core.Translation.ItemNameTranslator</c>
/// с цепочкой fallback'ов: рунные комбинации → rus.ndjson → stemmer + Levenshtein.
/// </summary>
public interface IItemNameTranslator
{
    /// <summary>Перевести имя. Возвращает null, если перевод не найден (цен не будет).</summary>
    /// <param name="name">Имя предмета на языке клиента (русский по умолчанию).</param>
    /// <param name="language">Код языка: "rus", "eng", и т.д.</param>
    string? TryTranslate(string name, string language = "rus");
}
