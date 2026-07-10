using Microsoft.Extensions.Logging;

namespace AldurPrice.Ocr;

/// <summary>
/// Обёртка над Tesseract 5.2 (через NuGet-пакет <c>Tesseract</c>).
/// Fallback-движок для систем без Windows OCR (Windows 7/8) и для языков,
/// не установленных в Windows OCR language pack.
///
/// <para><b>M0 — заглушка.</b> Полная реализация с распаковкой native DLL
/// (<c>tesseract50.dll</c>, <c>leptonica-1.82.0.dll</c>) — в M1.3.</para>
/// </summary>
public sealed class TesseractEngine : IOcrEngine
{
    private readonly ILogger<TesseractEngine> _logger;

    public TesseractEngine(ILogger<TesseractEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("TesseractEngine initialised (M0 stub, no native DLL loaded yet)");
    }

    /// <inheritdoc/>
    public string Name => "tesseract";

    /// <inheritdoc/>
    /// <remarks>M0: считаем недоступным (native DLL ещё не загружается).
    /// В M1.3 — реальная проверка наличия traineddata и native DLL.</remarks>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public Task<OcrResult> RecognizeAsync(byte[] bitmap, string language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        // M1.3: TesseractEngine(traineddataPath, language, EngineMode.TesseractOnly)
        //       → Pix.LoadFromMemory(bitmap) → engine.Process(pix) → GetLines().
        throw new NotImplementedException("TesseractEngine.RecognizeAsync — implemented in M1.3");
    }
}
