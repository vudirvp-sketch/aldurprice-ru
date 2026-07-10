using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace AldurPrice.Ocr;

/// <summary>
/// Обёртка над <c>Windows.Media.Ocr</c> — нативный OCR в Windows 10 1809+.
/// Primary движок (быстрее Tesseract, без native dependency).
///
/// <para>Требует <c>TargetFramework=net9.0-windows10.0.19041.0</c> для прямого доступа
/// к WinRT API. На более старых SDK нужен <c>Microsoft.Windows.SDK.Contracts</c>.</para>
///
/// <para><b>Языковая поддержка</b>: Windows OCR использует установленные языковые пакеты
/// OCR (Settings → Time &amp; Language → Language → Add a language → Optical character recognition).
/// <c>IsAvailable</c> проверяет наличие установленного распознавателя для текущего default-языка
/// (русский в AldurPrice). Если русский не установлен — движок возвращает <c>false</c> и
/// <c>OcrEngineResolver</c> переключится на Tesseract.</para>
///
/// <para><b>Координаты</b>: WinRT <c>OcrLine</c> не имеет direct Y-координаты, но содержит
/// <c>Words</c> с <c>BoundingRect</c>. Y линии = min Y её слов.</para>
/// </summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly ILogger<WindowsOcrEngine> _logger;
    private readonly Lazy<bool> _availabilityCheck;

    public WindowsOcrEngine(ILogger<WindowsOcrEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _availabilityCheck = new Lazy<bool>(CheckAvailability, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc/>
    public string Name => "windows";

    /// <inheritdoc/>
    /// <remarks>
    /// Проверяет, что хотя бы один OCR-язык установлен в системе. Ленивая инициализация —
    /// повторные вызовы берут закэшированный результат. Если у пользователя Windows без
    /// русских OCR-языковых пакетов, вернёт false (но возможно true если установлен english).
    /// </remarks>
    public bool IsAvailable => _availabilityCheck.Value;

    /// <inheritdoc/>
    public async Task<OcrResult> RecognizeAsync(byte[] bitmap, string language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        if (!IsAvailable)
            throw new InvalidOperationException("Windows OCR engine is not available on this system.");

        // 1. Декодируем PNG → SoftwareBitmap (BGRA8, premultiplied alpha).
        SoftwareBitmap softwareBitmap;
        using (var stream = new InMemoryRandomAccessStream())
        {
            await stream.WriteAsync(bitmap.AsBuffer());
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        using (softwareBitmap)
        {
            // 2. Создаём OCR engine для запрошенного языка.
            // Tesseract-коды (rus, eng, deu, fra, ...) конвертируем в BCP-47 (ru, en, de, fr, ...)
            // — Windows OCR принимает только BCP-47.
            var bcp47 = TesseractToBcp47(language);
            var lang = new Language(bcp47);
            if (!OcrEngine.IsLanguageSupported(lang))
            {
                _logger.LogWarning("Windows OCR does not support language '{Language}' (BCP-47: '{Bcp47}'). " +
                    "Install the OCR language pack in Windows Settings.", language, bcp47);
                throw new InvalidOperationException(
                    $"Windows OCR does not support language '{language}' (BCP-47: '{bcp47}'). " +
                    "Install the corresponding OCR language pack in Windows Settings.");
            }

            var engine = OcrEngine.TryCreateFromLanguage(lang)
                ?? throw new InvalidOperationException(
                    $"Failed to create Windows OCR engine for language '{language}' (BCP-47: '{bcp47}').");

            // 3. Распознаём.
            cancellationToken.ThrowIfCancellationRequested();
            var result = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);

            // 4. Маппим WinRT OcrResult → AldurPrice.Ocr.OcrResult.
            var lines = new List<OcrLine>(result.Lines.Count);
            foreach (var line in result.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.Text))
                    continue;

                // Y координата линии = min Y её слов.
                var minY = int.MaxValue;
                foreach (var word in line.Words)
                {
                    var y = (int)word.BoundingRect.Y;
                    if (y < minY) minY = y;
                }
                if (minY == int.MaxValue) minY = 0;

                lines.Add(new OcrLine(line.Text.Trim(), minY));
            }

            _logger.LogDebug("Windows OCR recognized {LineCount} lines for language '{Language}' (BCP-47: '{Bcp47}').",
                lines.Count, language, bcp47);

            return new OcrResult(lines);
        }
    }

    /// <summary>
    /// Конвертирует Tesseract-коды (ISO 639-2/B: rus, eng, deu, ...) в BCP-47 (ru, en, de, ...).
    /// Windows OCR принимает только BCP-747 tags. Если код уже в BCP-47 формате — возвращается как есть.
    /// </summary>
    private static string TesseractToBcp47(string language)
    {
        // Сначала проверяем точный match по ISO 639-2/B → BCP-47 маппингу.
        if (TesseractToBcp47Map.TryGetValue(language, out var bcp47))
            return bcp47;

        // Если код уже 2-буквенный — считаем что это BCP-47 (ru, en, de, fr, ...).
        if (language.Length == 2)
            return language;

        // Иначе возвращаем как есть — Windows OCR сообщит, что язык не поддерживается,
        // и пользователь поймёт, что нужно установить правильный языковой пакет.
        return language;
    }

    // Tesseract (ISO 639-2/B, 3-letter) → BCP-47 (ISO 639-1, 2-letter) mapping.
    // Покрывает все 8 языков, поддерживаемых PoE2 (см. docs/04-RU-LOCALIZATION.md §2.1).
    private static readonly Dictionary<string, string> TesseractToBcp47Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rus"] = "ru",
        ["eng"] = "en",
        ["deu"] = "de",
        ["fra"] = "fr",
        ["spa"] = "es",
        ["por"] = "pt",
        ["kor"] = "ko",
        ["chi_tra"] = "zh-Hant",  // Traditional Chinese
        ["chi_sim"] = "zh-Hans",  // Simplified Chinese (на всякий случай)
    };

    private bool CheckAvailability()
    {
        try
        {
            // Если хотя бы один язык доступен — считаем движок пригодным.
            // Точную проверку под конкретный язык делаем в RecognizeAsync (OcrEngine.IsLanguageSupported).
            var available = OcrEngine.AvailableRecognizerLanguages.Count > 0;
            _logger.LogInformation("WindowsOcrEngine availability: {Available} " +
                "(available languages: {Languages})",
                available,
                string.Join(", ", OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag)));
            return available;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Windows OCR availability. " +
                "Likely running on Windows < 10 1809 or without WinRT support.");
            return false;
        }
    }
}
