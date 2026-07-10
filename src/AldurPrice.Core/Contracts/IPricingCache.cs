namespace AldurPrice.Core.Contracts;

/// <summary>
/// Persistent кэш рыночных цен (SQLite в production).
/// Реализация — <c>AldurPrice.Data.SqlitePricingCache</c>.
/// </summary>
public interface IPricingCache
{
    /// <summary>Получить цену по английскому ключу. Возвращает null, если записи нет или истёк TTL.</summary>
    PriceQuote? TryGetPrice(string englishKey);

    /// <summary>True, если запись есть, но она «stale» (старше <c>StaleCacheTtlMinutes</c>).</summary>
    bool IsStale(string englishKey);

    /// <summary>Записать snapshot цен в кэш (UPSERT, в одной транзакции).</summary>
    void Update(PricingSnapshot snapshot);

    /// <summary>Загрузить весь горячий кэш из persistent-слоя (используется при старте).</summary>
    IReadOnlyDictionary<string, PriceQuote> LoadHotCache();
}
