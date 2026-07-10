using Microsoft.Extensions.Logging;

namespace AldurPrice.Ocr;

/// <summary>
/// Обёртка над <c>Windows.Media.Ocr</c> — нативный OCR в Windows 10 1809+.
/// Primary движок (быстрее Tesseract, без native dependency).
///
/// <para><b>M0 — заглушка.</b> Полная реализация с <c>Windows.Media.Ocr.OcrEngine</c> — в M1.3.</para>
/// </summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly ILogger<WindowsOcrEngine> _logger;

    public WindowsOcrEngine(ILogger<WindowsOcrEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("WindowsOcrEngine initialised (M0 stub)");
    }

    /// <inheritdoc/>
    public string Name => "windows";

    /// <inheritdoc/>
    /// <remarks>M0: считаем доступным всегда. В M1.3 — реальная проверка через
    /// <c>OcrEngine.TryCreateFromLanguage</c> и таймер на 1809+ build.</remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public Task<OcrResult> RecognizeAsync(byte[] bitmap, string language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        // M1.3: BitmapDecoder → OcrEngine.TryCreateFromLanguage(language) → RecognizeAsync.
        throw new NotImplementedException("WindowsOcrEngine.RecognizeAsync — implemented in M1.3");
    }
}
