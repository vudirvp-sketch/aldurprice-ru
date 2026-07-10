using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;
using Tesseract;

// Alias чтобы различать локальный класс TesseractEngine (этот файл) и NuGet-класс
// Tesseract.TesseractEngine из пакета Tesseract 5.2.0. Без alias компилятор
// резолвит TesseractEngine к локальному классу (он "ближе" в текущем namespace).
using TessEngine = Tesseract.TesseractEngine;

namespace AldurPrice.Ocr;

/// <summary>
/// Обёртка над Tesseract 5.2 (через NuGet-пакет <c>Tesseract</c>).
/// Fallback-движок для систем без Windows OCR (Windows 7/8) и для языков,
/// не установленных в Windows OCR language pack.
///
/// <para><b>Native DLL</b>: пакет <c>Tesseract 5.2.0</c> включает native libs
/// (<c>tesseract50.dll</c>, <c>leptonica-1.82.0.dll</c>) для win-x64 и win-x86 как
/// content files — они копируются в output dir при сборке. Никакой отдельный
/// bootstrapper не требуется (в отличие от ручной установки Tesseract).</para>
///
/// <para><b>Traineddata</b>: файлы <c>{lang}.traineddata</c> должны лежать в каталоге
/// <c>TesseractDataPath</c> (по умолчанию: <c>./tessdata</c> относительно рабочего каталога).
/// В AldurPrice они bundl'ятся в <c>ocr/tesseract/</c> и копируются в output при сборке
/// (см. <c>AldurPrice.Ocr.csproj</c>, <c>None Include</c> для traineddata файлов).
/// Поддерживаются <c>eng.traineddata</c> и <c>rus.traineddata</c> (best quality).</para>
///
/// <para><b>Потокобезопасность</b>: Tesseract engine НЕ потокобезопасен — одновременный
/// доступ к одному <c>TesseractEngine</c> из нескольких потоков вызывает UB. Здесь
/// используется <c>ConcurrentDictionary</c> по языкам + <c>lock</c> на каждый engine:
/// потоки, работающие с одним языком, сериализуются, но разные языки обрабатываются параллельно.</para>
///
/// <para><b>Производительность</b>: инициализация engine (загрузка traineddata) — 200-500 мс.
/// Поэтому engine кэшируется по языку и переиспользуется. Распознавание одной строки —
/// 50-200 мс в зависимости от размера битмапа. Это медленнее Windows OCR (10-50 мс),
/// но Tesseract даёт лучшее качество на зашумлённых изображениях.</para>
/// </summary>
public sealed class TesseractEngine : IOcrEngine, IDisposable
{
    private readonly ILogger<TesseractEngine> _logger;
    private readonly string _tessDataPath;
    private readonly int _engineMode;  // 0=legacy, 1=LSTM+legacy, 2=LSTM only

    // Кэш engine'ов по языку. Lazy<T> обеспечивает потокобезопасную инициализацию.
    private readonly ConcurrentDictionary<string, Lazy<EngineContainer>> _engines = new();

    private bool _disposed;

    public TesseractEngine(ILogger<TesseractEngine> logger)
        : this(logger, tessDataPath: string.Empty, engineMode: 2)
    {
    }

    /// <summary>
    /// Создаёт Tesseract-движок с указанными путём к traineddata и режимом engine.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="tessDataPath">Путь к каталогу с .traineddata файлами. Если пусто —
    /// ищется <c>./tessdata</c> относительно рабочего каталога.</param>
    /// <param name="engineMode">0=Legacy, 1=LSTM+Legacy, 2=LSTM only (default, самый точный).</param>
    public TesseractEngine(ILogger<TesseractEngine> logger, string tessDataPath, int engineMode)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tessDataPath = string.IsNullOrWhiteSpace(tessDataPath)
            ? Path.Combine(AppContext.BaseDirectory, "tessdata")
            : tessDataPath;
        _engineMode = engineMode;
    }

    /// <inheritdoc/>
    public string Name => "tesseract";

    /// <inheritdoc/>
    /// <remarks>Проверяет: каталог tessdata существует + хотя бы один .traineddata файл на месте.</remarks>
    public bool IsAvailable
    {
        get
        {
            try
            {
                if (!Directory.Exists(_tessDataPath))
                {
                    _logger.LogDebug("Tesseract tessdata directory not found: {Path}", _tessDataPath);
                    return false;
                }
                var trainedFiles = Directory.GetFiles(_tessDataPath, "*.traineddata");
                if (trainedFiles.Length == 0)
                {
                    _logger.LogDebug("No .traineddata files in {Path}", _tessDataPath);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check Tesseract availability.");
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public Task<OcrResult> RecognizeAsync(byte[] bitmap, string language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        if (!IsAvailable)
            throw new InvalidOperationException(
                $"Tesseract is not available: tessdata directory '{_tessDataPath}' missing or empty.");

        // Tesseract SDK — synchronous API; оборачиваем в Task.Run чтобы не блокировать caller thread.
        // cancellationToken передаём в Run для cooperative cancellation.
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var container = _engines.GetOrAdd(language, lang => new Lazy<EngineContainer>(() =>
            {
                var engineMode = _engineMode switch
                {
                    0 => Tesseract.EngineMode.TesseractOnly,
                    1 => Tesseract.EngineMode.TesseractAndLstm,
                    _ => Tesseract.EngineMode.LstmOnly,
                };

                var engine = new TessEngine(_tessDataPath, lang, engineMode)
                {
                    DefaultPageSegMode = PageSegMode.Auto
                };

                _logger.LogInformation("Initialised Tesseract engine for language '{Language}' " +
                    "(mode={Mode}, tessdata='{Path}').", lang, engineMode, _tessDataPath);
                return new EngineContainer(engine);
            }, LazyThreadSafetyMode.ExecutionAndPublication));

            // Tesseract engine не потокобезопасен — сериализуем доступ.
            lock (container.Lock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var img = Pix.LoadFromMemory(bitmap);
                using var page = container.Engine.Process(img);

                var lines = new List<OcrLine>();
                using var iter = page.GetIterator();
                iter.Begin();
                do
                {
                    if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect))
                    {
                        var text = iter.GetText(PageIteratorLevel.TextLine);
                        if (!string.IsNullOrWhiteSpace(text))
                            lines.Add(new OcrLine(text.Trim(), rect.Y));
                    }
                    else
                    {
                        // Если bounding box недоступен, всё равно берём текст (Y=0).
                        var text = iter.GetText(PageIteratorLevel.TextLine);
                        if (!string.IsNullOrWhiteSpace(text))
                            lines.Add(new OcrLine(text.Trim(), 0));
                    }
                } while (iter.Next(PageIteratorLevel.TextLine));

                _logger.LogDebug("Tesseract recognized {LineCount} lines for language '{Language}'.",
                    lines.Count, language);

                return new OcrResult(lines);
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kv in _engines)
        {
            try
            {
                if (kv.Value.IsValueCreated)
                {
                    kv.Value.Value.Engine.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose Tesseract engine for language '{Language}'.", kv.Key);
            }
        }
        _engines.Clear();
    }

    /// <summary>Обёртка над TesseractEngine + lock object для потокобезопасного доступа.</summary>
    private sealed class EngineContainer
    {
        public TessEngine Engine { get; }
        public object Lock { get; } = new();

        public EngineContainer(TessEngine engine)
        {
            Engine = engine;
        }
    }
}
