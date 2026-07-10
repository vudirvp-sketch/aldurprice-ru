namespace AldurPrice.Ocr;

/// <summary>
/// Настройки предобработки изображения для OCR.
///
/// <para>Зеркалирует подсекцию <c>OCR</c> из <c>appsettings.json</c>, но без жёсткой
/// привязки к <c>AldurPrice.Configuration.OcrOptions</c> (тот живёт в основном проекте,
/// и ссылка на него из <c>AldurPrice.Ocr</c> создала бы цикл: <c>AldurPrice → Ocr → AldurPrice</c>).
/// <c>App.xaml.cs</c> строит этот record из <c>IOptions&lt;OcrOptions&gt;</c> при DI-регистрации.</para>
/// </summary>
public sealed record OcrPreprocessOptions
{
    /// <summary>Включить предобработку (greyscale + binarization).</summary>
    public bool EnableImagePreprocessing { get; init; } = true;

    /// <summary>Порог бинаризации (0-255). Пиксели с luminance выше порога → белые, ниже → чёрные.</summary>
    public int BinarizationThreshold { get; init; } = 145;

    /// <summary>Включить цветовой фильтр: выделять только пиксели «цвета текста».</summary>
    public bool EnableTextColorFiltering { get; init; } = true;

    /// <summary>Целевой R компонент цвета текста (PoE2 default: 50).</summary>
    public int TextColorTargetR { get; init; } = 50;

    /// <summary>Целевой G компонент (PoE2 default: 42).</summary>
    public int TextColorTargetG { get; init; } = 42;

    /// <summary>Целевой B компонент (PoE2 default: 34).</summary>
    public int TextColorTargetB { get; init; } = 34;

    /// <summary>Допуск по каждой компоненте R/G/B (Euclidean-like match).</summary>
    public int TextColorTolerance { get; init; } = 47;

    /// <summary>Максимальная luminance пикселя, чтобы считаться «текстом» (0-255).</summary>
    public int TextColorMaxLuminance { get; init; } = 145;

    /// <summary>Максимальный разброс |R-G|, |G-B|, |R-B| — чтобы отсеять цветной мусор.</summary>
    public int TextColorMaxChannelSpread { get; init; } = 29;
}

/// <summary>
/// Настройки детектора панели рунешейпов.
///
/// <para>Панель рунешейпов в PoE2 имеет характерный тёмный фон (тёмно-коричневый/тёмно-серый)
/// с заданным диапазоном цветов. Детектор считает пиксели в этом диапазоне и считает панель
/// открытой, если доля таких пикселей выше порога.</para>
/// </summary>
public sealed record LeaguePanelDetectorOptions
{
    /// <summary>Минимальный R компонент фона панели.</summary>
    public int PanelBgRMin { get; init; } = 20;

    /// <summary>Максимальный R компонент фона панели.</summary>
    public int PanelBgRMax { get; init; } = 70;

    /// <summary>Минимальный G компонент фона панели.</summary>
    public int PanelBgGMin { get; init; } = 18;

    /// <summary>Максимальный G компонент фона панели.</summary>
    public int PanelBgGMax { get; init; } = 60;

    /// <summary>Минимальный B компонент фона панели.</summary>
    public int PanelBgBMin { get; init; } = 15;

    /// <summary>Максимальный B компонент фона панели.</summary>
    public int PanelBgBMax { get; init; } = 55;

    /// <summary>Минимальная доля пикселей фона панели (от 0.0 до 1.0), чтобы считать панель открытой.</summary>
    public double MinPanelPixelRatio { get; init; } = 0.15;

    /// <summary>Шаг сэмплинга (1 = каждый пиксель, 4 = каждый 4-й). Ускоряет детектор в N² раз.</summary>
    public int SamplingStep { get; init; } = 4;
}
