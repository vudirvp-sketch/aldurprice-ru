using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using AldurPrice.Capture.Win32;
using Microsoft.Extensions.Logging;

namespace AldurPrice.Capture;

/// <summary>
/// Win32 <c>PrintWindow</c> — primary стратегия захвата окна PoE2.
///
/// <para><b>Почему PrintWindow, а не BitBlt</b>: PoE2 рендерится через
/// DirectX/DirectComposition. Обычный <c>BitBlt</c> с window DC возвращает
/// чёрный прямоугольник, потому что compositor рисует минуя GDI.
/// <c>PrintWindow</c> с флагом <see cref="NativeMethods.PW_RENDERFULLCONTENT"/>
/// (Windows 8.1+) заставляет compositor отдать финальный кадр.</para>
///
/// <para><b>Поток данных</b>:
/// <list type="number">
///   <item><see cref="Poe2WindowLocator.TryLocate"/> → HWND PoE2.</item>
///   <item><c>GetClientRect</c> → размеры клиентской области (для валидации региона).</item>
///   <item><c>CreateCompatibleDC</c> + <c>CreateCompatibleBitmap</c> → memory DC.</item>
///   <item><c>PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT)</c> → рендер всего окна в DC.</item>
///   <item><c>Image.FromHbitmap</c> → <see cref="Bitmap"/> для managed-обработки.</item>
///   <item>Crop через <see cref="Bitmap.Clone(Rectangle, PixelFormat)"/> → финальный битмап.</item>
///   <item><c>Bitmap.Save</c> с <see cref="ImageFormat.Png"/> → PNG-байты для OCR-пайплайна.</item>
/// </list></para>
///
/// <para><b>Координаты региона</b>: <see cref="CaptureRegion"/> задаётся в
/// координатах относительно левого-верхнего угла клиентской области окна PoE2
/// (НЕ экранных). Это позволяет региону «двигаться» вместе с окном — пользователь
/// задаёт смещение один раз в <c>WindowOptions.CustomOffsetX/Y</c>, и захват
/// остаётся корректным при перемещении окна. См. AD-005 в STATUS.md.</para>
///
/// <para><b>Thread safety</b>: сам класс stateless (locator с внутренним кэшем
/// потокобезопасен). Все GDI-handle'ы — локальные переменные в
/// <see cref="CaptureCore"/>, не разделяются между потоками. Можно вызывать
/// <see cref="CaptureAsync"/> параллельно из нескольких потоков.</para>
///
/// <para><b>Disposable-ресурсы</b>: все GDI-объекты (DC, bitmap) и managed
/// <see cref="Bitmap"/> освобождаются в <c>finally</c>-блоке. Утечка GDI-handle
/// — типичная проблема capture-кода, поэтому каждое создание/удаление парное.</para>
/// </summary>
public sealed class PrintWindowCapture : ICaptureStrategy
{
    private readonly Poe2WindowLocator _locator;
    private readonly ILogger<PrintWindowCapture> _logger;

    public PrintWindowCapture(Poe2WindowLocator locator, ILogger<PrintWindowCapture> logger)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "printwindow";

    /// <inheritdoc/>
    public Task<byte[]> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (!region.IsValid)
            throw new ArgumentException("Capture region is not valid (width/height must be > 0).", nameof(region));

        cancellationToken.ThrowIfCancellationRequested();

        // Синхронный Win32-вызов. PrintWindow быстрый (~5-15 мс на 1080p), блокировка
        // thread-pool потока на это время приемлема. Если станет узким местом —
        // можно обернуть в Task.Run, но для M1.4 это преждевременная оптимизация.
        byte[] pngBytes = CaptureCore(region);
        return Task.FromResult(pngBytes);
    }

    private byte[] CaptureCore(CaptureRegion region)
    {
        // 1. Найти окно PoE2.
        var window = _locator.TryLocate();
        if (window is null)
        {
            throw new InvalidOperationException(
                "PoE2 window not found. Ensure Path of Exile 2 is running before capturing.");
        }

        // 2. Валидация: регион должен помещаться в клиентскую область.
        //    GetClientRect возвращает left/top = 0, right/bottom = width/height.
        if (!NativeMethods.GetClientRect(window.Handle, out var clientRect))
        {
            throw new InvalidOperationException(
                $"GetClientRect failed for PoE2 window (hwnd=0x{window.Handle.ToInt64():X}). " +
                $"Win32 error: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
        }

        int clientWidth = clientRect.Width;
        int clientHeight = clientRect.Height;

        if (clientWidth <= 0 || clientHeight <= 0)
        {
            throw new InvalidOperationException(
                $"PoE2 client area has non-positive dimensions ({clientWidth}x{clientHeight}). " +
                "Window may be minimized.");
        }

        // Если регион выходит за клиентскую область — это ошибка конфигурации.
        // Не обрезаем молча (молчаливый crop даст чёрные пиксели и сломает OCR),
        // а бросаем явное исключение с понятным сообщением.
        if (region.X < 0 || region.Y < 0 ||
            region.X + region.Width > clientWidth ||
            region.Y + region.Height > clientHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(region),
                $"Capture region ({region.X},{region.Y} {region.Width}x{region.Height}) " +
                $"is outside PoE2 client area ({clientWidth}x{clientHeight}). " +
                "Reconfigure WindowOptions offsets or rerun setup overlay.");
        }

        // 3. GDI-ресурсы. Создаём ВСЕ до PrintWindow, освобождаем ВСЕ в finally.
        //    Порядок освобождения: обратный к созданию.
        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hbmp = IntPtr.Zero;
        IntPtr hbmpOld = IntPtr.Zero;
        Bitmap? fullBitmap = null;
        Bitmap? croppedBitmap = null;

        try
        {
            // Создаём memory DC совместимый с экраном (hdc = IntPtr.Zero → screen DC).
            hdcMem = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
            if (hdcMem == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"CreateCompatibleDC failed. Win32 error: {GetLastWin32Error()}");
            }

            // Bitmap для рендера ВСЕГО окна (PrintWindow не умеет в подрегион).
            hbmp = NativeMethods.CreateCompatibleBitmap(hdcMem, clientWidth, clientHeight);
            if (hbmp == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"CreateCompatibleBitmap failed ({clientWidth}x{clientHeight}). " +
                    $"Win32 error: {GetLastWin32Error()}");
            }

            // Выбираем bitmap в DC (обязательно — без этого PrintWindow рисует в никуда).
            hbmpOld = NativeMethods.SelectObject(hdcMem, hbmp);

            // 4. PrintWindow с PW_RENDERFULLCONTENT — рендер окна в memory DC.
            //    Возвращает false, если окно не отвечает или не поддерживает PrintWindow.
            bool ok = NativeMethods.PrintWindow(
                window.Handle, hdcMem, NativeMethods.PW_RENDERFULLCONTENT);
            if (!ok)
            {
                var err = GetLastWin32Error();
                throw new InvalidOperationException(
                    $"PrintWindow returned false for PoE2 window (hwnd=0x{window.Handle.ToInt64():X}). " +
                    $"Win32 error: {err}. Window may be unresponsive or use unsupported renderer.");
            }

            // 5. Конвертируем HBITMAP → managed Bitmap. Image.FromHbitmap делает копию
            //    пикселей, после чего GDI-bitmap можно освобождать.
            fullBitmap = Image.FromHbitmap(hbmp);
            if (fullBitmap is null)
            {
                throw new InvalidOperationException("Image.FromHbitmap returned null.");
            }

            // 6. Crop — берём только нужный регион. Clone создаёт НОВЫЙ bitmap
            //    с копией пикселей (не view). После этого fullBitmap можно диспозить.
            var cropRect = new Rectangle(region.X, region.Y, region.Width, region.Height);
            croppedBitmap = fullBitmap.Clone(cropRect, fullBitmap.PixelFormat);

            // 7. PNG-encode. MemoryStream — чтобы вернуть byte[] без файла на диске.
            using var ms = new MemoryStream();
            croppedBitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        finally
        {
            // Освобождение в обратном порядке. Каждый GDI-handle — ценный ресурс
            // (Windows даёт ~10000 на процесс, утечка 10 за capture = 1000 captures = crash).
            croppedBitmap?.Dispose();
            fullBitmap?.Dispose();

            // Возвращаем старый объект в DC (обязательно перед DeleteObject bitmap'а).
            if (hbmpOld != IntPtr.Zero && hdcMem != IntPtr.Zero)
            {
                NativeMethods.SelectObject(hdcMem, hbmpOld);
            }
            if (hbmp != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(hbmp);
            }
            if (hdcMem != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(hdcMem);
            }
        }
    }

    private static string GetLastWin32Error() =>
        System.Runtime.InteropServices.Marshal.GetLastWin32Error()
            .ToString(CultureInfo.InvariantCulture);
}
