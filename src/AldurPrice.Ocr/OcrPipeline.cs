using AldurPrice.Core.Translation;
using Microsoft.Extensions.Logging;

namespace AldurPrice.Ocr;

/// <summary>
/// Оркестратор OCR-пайплайна: связывает детектор панели, препроцессор, движок OCR
/// и постпроцессор кириллицы в единый flow.
///
/// <para>Поток данных (см. docs/02-ARCHITECTURE.md §3.1 Главный OCR-цикл):
/// <list type="number">
///   <item><b>LeaguePanelDetector.IsPanelOpen</b> — если панель закрыта, возвращаем
///         пустой результат, не тратим CPU на OCR.</item>
///   <item><b>ImagePreprocessor.Process</b> — greyscale + color filter + binarization.
///         Возвращает PNG-байты, оптимизированные для OCR.</item>
///   <item><b>OcrEngineResolver.Resolve().RecognizeAsync</b> — primary Windows OCR,
///         fallback на Tesseract (логика в resolver'е).</item>
///   <item><b>RussianOcrPostProcessor.ProcessLine</b> — для языка "rus": нормализация Ё→Е,
///         кавычки, пробелы, типичные OCR-мусор-символы.</item>
///   <item>Возвращаем финальный <see cref="OcrResult"/> с очищенными линиями и Y-координатами.</item>
/// </list></para>
///
/// <para><b>Thread safety</b>: сам pipeline stateless (все поля readonly). Потокобезопасность
/// отдельных компонентов — ответственность каждого компонента. См. документацию на
/// <see cref="WindowsOcrEngine"/>, <see cref="TesseractEngine"/>, <see cref="ImagePreprocessor"/>,
/// <see cref="LeaguePanelDetector"/>.</para>
/// </summary>
public sealed class OcrPipeline
{
    private readonly OcrEngineResolver _resolver;
    private readonly ImagePreprocessor _preprocessor;
    private readonly LeaguePanelDetector _panelDetector;
    private readonly RussianOcrPostProcessor _postProcessor;
    private readonly ILogger<OcrPipeline> _logger;

    public OcrPipeline(
        OcrEngineResolver resolver,
        ImagePreprocessor preprocessor,
        LeaguePanelDetector panelDetector,
        RussianOcrPostProcessor postProcessor,
        ILogger<OcrPipeline> logger)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
        _panelDetector = panelDetector ?? throw new ArgumentNullException(nameof(panelDetector));
        _postProcessor = postProcessor ?? throw new ArgumentNullException(nameof(postProcessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Прогнать битмап через весь OCR-пайплайн.
    /// </summary>
    /// <param name="bitmap">PNG-байты региона экрана из capture layer.</param>
    /// <param name="language">Код языка OCR: "rus", "eng", и т.д.</param>
    /// <param name="preprocessOptions">Настройки препроцессора (null = default).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>
    /// <see cref="OcrResult"/> с распознанными и постобработанными линиями.
    /// Если панель закрыта — пустой результат (0 линий).
    /// </returns>
    public async Task<OcrResult> ProcessAsync(
        byte[] bitmap,
        string language,
        OcrPreprocessOptions? preprocessOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        // 1. Panel detection — если панель закрыта, не тратим CPU на OCR.
        bool panelOpen;
        try
        {
            panelOpen = _panelDetector.IsPanelOpen(bitmap);
        }
        catch (Exception ex)
        {
            // Детектор не должен падать весь pipeline — лучше прогнать OCR и показать цены,
            // чем тихо ничего не делать. Логируем warning, считаем что панель открыта.
            _logger.LogWarning(ex, "LeaguePanelDetector failed; assuming panel is open.");
            panelOpen = true;
        }

        if (!panelOpen)
        {
            _logger.LogDebug("League panel not detected; skipping OCR.");
            return new OcrResult(Array.Empty<OcrLine>());
        }

        // 2. Preprocessing — отдельный try/catch: если упал препроцессор, пробуем
        //    оригинальный битмап (Windows OCR умеет работать с цветными изображениями).
        byte[] ocrInput;
        try
        {
            ocrInput = preprocessOptions != null
                ? _preprocessor.Process(bitmap, preprocessOptions)
                : _preprocessor.Process(bitmap);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImagePreprocessor failed; using original bitmap.");
            ocrInput = bitmap;
        }

        // 3. OCR через resolver (windows-first → tesseract fallback).
        var engine = _resolver.Resolve();
        OcrResult rawResult;
        try
        {
            rawResult = await engine.RecognizeAsync(ocrInput, language, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR engine '{EngineName}' failed for language '{Language}'.",
                engine.Name, language);
            throw;
        }

        // 4. Post-processing — для русского языка применяем кириллические нормализации.
        //    Для остальных языков — только whitespace cleanup (через тот же ProcessLine).
        var processedLines = new List<OcrLine>(rawResult.Lines.Count);
        foreach (var line in rawResult.Lines)
        {
            var cleaned = _postProcessor.ProcessLine(line.Text, language);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                processedLines.Add(new OcrLine(cleaned, line.Y));
            }
        }

        _logger.LogInformation("OCR pipeline done: engine={Engine}, language={Language}, " +
            "rawLines={Raw}, processedLines={Processed}.",
            engine.Name, language, rawResult.Lines.Count, processedLines.Count);

        return new OcrResult(processedLines);
    }
}
