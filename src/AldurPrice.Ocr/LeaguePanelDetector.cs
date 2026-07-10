using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Extensions.Logging;

namespace AldurPrice.Ocr;

/// <summary>
/// Детектор открытой панели рунешейпов лиги «Руны Альдура».
///
/// <para>Панель рунешейпов в PoE2 — это overlay-окно с тёмно-коричневым/тёмно-серым фоном
/// (RGB ≈ 20-70, 18-60, 15-55) и светлым текстом предметов. Когда панель закрыта —
/// на месте региона видна обычная сцена игры (яркие цвета). Простой эвристический детектор:
/// считаем пиксели в диапазоне «фона панели», если их доля выше порога — панель открыта.</para>
///
/// <para><b>M1.3 — упрощённая версия.</b> Полный HSV-segmentation с детектированием
/// конкретной формы/расположения панели — M2.x. Сейчас используется RGB-range matching
/// с сэмплингом (каждый N-й пиксель) для производительности. Этого достаточно для базового
/// gating'а: если панель не открыта, пропускаем OCR (экономит CPU).</para>
///
/// <para><b>Ложные срабатывания</b> возможны, если в регионе захвата видна тёмная сцена
/// (пещера, ночь). Для M1.3 это приемлемо: лучше лишний раз прогнать OCR и не показать
/// цены, чем пропустить реальную панель. Точная сегментация по форме — M2.x.</para>
/// </summary>
public sealed class LeaguePanelDetector
{
    private readonly ILogger<LeaguePanelDetector> _logger;
    private LeaguePanelDetectorOptions _options;

    public LeaguePanelDetector(ILogger<LeaguePanelDetector> logger)
        : this(logger, new LeaguePanelDetectorOptions())
    {
    }

    public LeaguePanelDetector(ILogger<LeaguePanelDetector> logger, LeaguePanelDetectorOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options;
    }

    /// <summary>Обновить опции детектора (вызывается при reload конфигурации).</summary>
    public void UpdateOptions(LeaguePanelDetectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Проверить, открыта ли панель рунешейпов на данном скриншоте.
    /// </summary>
    /// <param name="pngBytes">PNG-байты региона экрана (из capture layer).</param>
    /// <returns><c>true</c>, если панель предположительно открыта.</returns>
    public bool IsPanelOpen(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length == 0)
            return false;

        using var bmp = new Bitmap(new MemoryStream(pngBytes));
        return IsPanelOpen(bmp);
    }

    /// <summary>
    /// Проверить, открыта ли панель, для готового Bitmap (внутренняя перегрузка для тестов).
    /// </summary>
    internal bool IsPanelOpen(Bitmap bmp)
    {
        ArgumentNullException.ThrowIfNull(bmp);

        var opt = _options;
        var step = Math.Max(1, opt.SamplingStep);

        var totalSampled = 0;
        var panelPixels = 0;

        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                var stride = bd.Stride;
                var scan0 = (byte*)bd.Scan0.ToPointer();
                var bpp = 4;

                for (var y = 0; y < bd.Height; y += step)
                {
                    var row = scan0 + (y * stride);
                    for (var x = 0; x < bd.Width; x += step)
                    {
                        var p = row + (x * bpp);
                        var b = p[0];
                        var g = p[1];
                        var r = p[2];

                        totalSampled++;

                        if (r >= opt.PanelBgRMin && r <= opt.PanelBgRMax
                            && g >= opt.PanelBgGMin && g <= opt.PanelBgGMax
                            && b >= opt.PanelBgBMin && b <= opt.PanelBgBMax)
                        {
                            panelPixels++;
                        }
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bd);
        }

        if (totalSampled == 0)
            return false;

        var ratio = (double)panelPixels / totalSampled;
        var isOpen = ratio >= opt.MinPanelPixelRatio;

        // Логируем только при смене состояния — чтобы не спамить в debug-логах при каждом кадре.
        // Для тонкой настройки пользователь может включить Debug-уровень.
        _logger.LogDebug("Panel detection: ratio={Ratio:F3} threshold={Threshold:F3} open={Open}",
            ratio, opt.MinPanelPixelRatio, isOpen);

        return isOpen;
    }
}
