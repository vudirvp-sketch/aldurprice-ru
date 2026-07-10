using AldurPrice.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace AldurPrice.Data;

/// <summary>
/// SQLite-based persistent кэш рыночных цен (WAL mode, UPSERT, batch-запись).
/// Реализация <see cref="IPricingCache"/>.
///
/// <para><b>M0 — заглушка.</b> Полная реализация с миграциями 001_init.sql / 002_indexes.sql,
/// WAL-режимом и stale-fallback — в M3.4 (см. docs/05-ROADMAP.md).</para>
/// </summary>
public sealed class SqlitePricingCache : IPricingCache, IDisposable
{
    private readonly ILogger<SqlitePricingCache> _logger;
    private bool _disposed;

    public SqlitePricingCache(ILogger<SqlitePricingCache> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SqlitePricingCache initialised (M0 stub, no persistence yet)");
    }

    /// <inheritdoc/>
    public PriceQuote? TryGetPrice(string englishKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishKey);
        // M3.4: SELECT FROM prices WHERE key=? AND updated_at > now - ttl
        return null;
    }

    /// <inheritdoc/>
    public bool IsStale(string englishKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishKey);
        // M3.4: SELECT updated_at FROM prices WHERE key=? → compare with StaleCacheTtlMinutes
        return false;
    }

    /// <inheritdoc/>
    public void Update(PricingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        // M3.4: BEGIN TRANSACTION; INSERT ... ON CONFLICT(key) DO UPDATE; COMMIT;
        _logger.LogDebug("Update called for {Count} prices from {Source} (M0 stub, no-op)",
            snapshot.Prices.Count, snapshot.Source);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, PriceQuote> LoadHotCache()
    {
        // M3.4: SELECT key, chaos_value, divine_value, exalt_value, ... FROM prices WHERE league=?
        return new Dictionary<string, PriceQuote>(StringComparer.Ordinal);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        // M3.4: _connection?.Dispose(); VACUUM если db > 50 МБ.
        _disposed = true;
    }
}
