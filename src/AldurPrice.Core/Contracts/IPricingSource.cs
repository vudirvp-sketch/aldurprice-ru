namespace AldurPrice.Core.Contracts;

/// <summary>
/// Источник рыночных цен (poe2scout / poe.ninja). HTTP-клиент, обновляется фоновым воркером.
/// </summary>
public interface IPricingSource
{
    /// <summary>Имя источника для логирования и Settings: "poe2scout" / "poe.ninja".</summary>
    string Name { get; }

    /// <summary>Запросить snapshot цен для конкретной лиги.</summary>
    /// <param name="league">Название лиги, например "Runes of Aldur".</param>
    /// <param name="cancellationToken">Токен отмены (15-минутный таймер).</param>
    Task<PricingSnapshot> FetchPricesAsync(string league, CancellationToken cancellationToken = default);
}
