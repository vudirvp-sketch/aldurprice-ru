namespace AldurPrice.Capture;

/// <summary>
/// Стратегия захвата региона окна PoE2. Реализации —
/// <see cref="PrintWindowCapture"/> (Win32 <c>PrintWindow</c>, primary) и
/// <c>WgcCapture</c> (<c>Windows.Graphics.Capture</c>, fallback для Lossless Scaling).
/// </summary>
public interface ICaptureStrategy
{
    /// <summary>Имя стратегии: "printwindow" / "wgc".</summary>
    string Name { get; }

    /// <summary>Захватить указанный регион экрана. Возвращает PNG-байты.</summary>
    /// <param name="region">Регион в экранных координатах (x, y, w, h).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<byte[]> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken = default);
}

/// <summary>Регион захвата в экранных координатах.</summary>
public sealed record CaptureRegion(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}
