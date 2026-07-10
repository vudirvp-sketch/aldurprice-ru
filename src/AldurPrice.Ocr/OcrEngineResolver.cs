using Microsoft.Extensions.Logging;

namespace AldurPrice.Ocr;

/// <summary>
/// Выбор доступного OCR-движка при старте.
/// Логика:
/// <list type="number">
///   <item>Если доступен <see cref="WindowsOcrEngine"/> — используем его (primary).</item>
///   <item>Иначе пытаемся <see cref="TesseractEngine"/> (fallback).</item>
///   <item>Если оба недоступны — UI показывает диалог с инструкцией, запускаем без OCR.</item>
/// </list>
///
/// <para><b>M0 — заглушка.</b> Полная реализация с runtime failover — в M1.3.</para>
/// </summary>
public sealed class OcrEngineResolver
{
    private readonly WindowsOcrEngine _windows;
    private readonly TesseractEngine _tesseract;
    private readonly ILogger<OcrEngineResolver> _logger;

    public OcrEngineResolver(
        WindowsOcrEngine windows,
        TesseractEngine tesseract,
        ILogger<OcrEngineResolver> logger)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _tesseract = tesseract ?? throw new ArgumentNullException(nameof(tesseract));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Вернуть доступный движок. M0: всегда Windows (stub).</summary>
    public IOcrEngine Resolve()
    {
        if (_windows.IsAvailable)
        {
            _logger.LogInformation("Resolved OCR engine: {Name}", _windows.Name);
            return _windows;
        }

        if (_tesseract.IsAvailable)
        {
            _logger.LogInformation("Resolved OCR engine: {Name} (fallback)", _tesseract.Name);
            return _tesseract;
        }

        _logger.LogError("No OCR engine available. Application will run without OCR.");
        throw new InvalidOperationException(
            "No OCR engine available. Install Windows 10 1809+ or Tesseract traineddata.");
    }
}
