namespace AldurPrice.Core.Translation;

/// <summary>
/// In-memory LRU кэш переводов. M0 — заглушка.
/// В M1.2 будет ConcurrentDictionary с bounded size 10 000 и eviction.
/// </summary>
public sealed class TranslationCache
{
    /// <summary>Сохранить перевод в кэш. M0 stub.</summary>
    public void Store(string sourceName, string targetName) { }

    /// <summary>Получить перевод из кэша. M0 stub — всегда null.</summary>
    public string? TryLookup(string sourceName) => null;

    /// <summary>Очистить кэш (например, при смене языка).</summary>
    public void Clear() { }
}
