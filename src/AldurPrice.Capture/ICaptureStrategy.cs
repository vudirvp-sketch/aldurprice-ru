namespace AldurPrice.Capture;

/// <summary>
/// Стратегия захвата региона окна PoE2. Реализации —
/// <see cref="PrintWindowCapture"/> (Win32 <c>PrintWindow</c>, primary) и
/// <c>WgcCapture</c> (<c>Windows.Graphics.Capture</c>, fallback для Lossless Scaling —
/// stub до M1.4b / M2, см. KI-013 в STATUS.md).
/// </summary>
public interface ICaptureStrategy
{
    /// <summary>Имя стратегии: "printwindow" / "wgc".</summary>
    string Name { get; }

    /// <summary>
    /// Захватить указанный регион окна PoE2. Возвращает PNG-байты, готовые для
    /// подачи в <c>OcrPipeline.ProcessAsync</c>.
    /// </summary>
    /// <param name="region">
    /// Регион в координатах относительно левого-верхнего угла клиентской области
    /// окна PoE2 (НЕ экранных). Это позволяет региону двигаться вместе с окном.
    /// См. AD-005 в STATUS.md.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="InvalidOperationException">
    /// Окно PoE2 не найдено, минимизировано или PrintWindow не смог рендерить.
    /// </exception>
    Task<byte[]> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken = default);
}

/// <summary>
/// Регион захвата в координатах относительно клиентской области окна PoE2
/// (left-top = 0,0). См. комментарий на <see cref="ICaptureStrategy.CaptureAsync"/>.
/// </summary>
public sealed record CaptureRegion(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// True, если регион имеет положительные размеры. Координаты X/Y могут быть
    /// любыми (валидация на попадание в клиентскую область делается в реализации).
    /// </summary>
    public bool IsValid => Width > 0 && Height > 0;
}
