namespace AldurPrice.Core.Contracts;

/// <summary>
/// Тестопригодное абстрагирование времени. В production — <c>SystemClock</c>,
/// в тестах — mock с предустановленным <see cref="DateTimeOffset"/>.
/// </summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

internal sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
