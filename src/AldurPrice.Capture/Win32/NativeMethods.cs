using System.Runtime.InteropServices;

namespace AldurPrice.Capture.Win32;

/// <summary>
/// Централизованные P/Invoke-декларации для <c>user32.dll</c> и <c>gdi32.dll</c>,
/// используемые слоем захвата экрана (<see cref="PrintWindowCapture"/>).
///
/// <para><b>Зачем отдельный файл</b>: P/Invoke-сигнатуры громоздки, их удобно
/// держать в одном месте. <see cref="PrintWindowCapture"/> вызывает только
/// высокоуровневые методы отсюда, что упрощает чтение бизнес-логики.</para>
///
/// <para><b>Документация Win32 API</b>:
/// <list type="bullet">
///   <item><see href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow">PrintWindow</see></item>
///   <item><see href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclientrect">GetClientRect</see></item>
///   <item><see href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect">GetWindowRect</see></item>
///   <item><see href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindow">IsWindow</see></item>
///   <item><see href="https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt">BitBlt</see></item>
/// </list></para>
/// </summary>
internal static class NativeMethods
{
    // --- PrintWindow flags ---

    /// <summary>
    /// <c>PW_RENDERFULLCONTENT</c> (0x00000002). Заставляет <c>PrintWindow</c>
    /// рендерить содержимое окна, даже если оно использует DirectComposition
    /// (WPF, Modern UI, PoE2 renderer). Без этого флага WPF-подобные окна
    /// захватываются как чёрные прямоугольники.
    ///
    /// <para>Требует Windows 8.1+. На Windows 7 флаг игнорируется (там нет
    /// DirectComposition), capture работает через обычный BitBlt-путь.</para>
    /// </summary>
    public const int PW_RENDERFULLCONTENT = 0x00000002;

    /// <summary>
    /// <c>PW_CLIENTONLY</c> (0x00000001). Рендерить только клиентскую область
    /// (без заголовка и рамки). В AldurPrice не используется — нам нужен весь
    /// window bitmap, а crop делается отдельно через <see cref="PrintWindowCapture"/>.
    /// Оставлено для документации / будущего использования.
    /// </summary>
    public const int PW_CLIENTONLY = 0x00000001;

    /// <summary>
    /// <c>SRCCOPY</c> (0x00CC0020). Ter-Raster-Operation code для <c>BitBlt</c>:
    /// копировать источник как есть. Используется при crop-операции.
    /// </summary>
    public const int SRCCOPY = 0x00CC0020;

    // --- user32.dll ---

    /// <summary>
    /// Рендерит окно в указанный device context. Возвращает <c>true</c> при успехе.
    /// </summary>
    /// <param name="hwnd">HWND целевого окна.</param>
    /// <param name="hdcBlt">HDC приёмника (куда рендерить).</param>
    /// <param name="nFlags">Комбинация флагов <c>PW_*</c> (см. <see cref="PW_RENDERFULLCONTENT"/>).</param>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

    /// <summary>
    /// Возвращает клиентский прямоугольник окна (в координатах относительно
    /// левого-верхнего угла клиентской области, поэтому <c>left</c>/<c>top</c> = 0,
    /// а <c>right</c>/<c>bottom</c> = ширина/высота).
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// Возвращает bounding rectangle окна в экранных координатах
    /// (включая заголовок и рамки).
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// Проверяет, валиден ли HWND (окно существует). Используется для проверки
    /// закэшированного handle перед захватом — экономит Process scan.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    /// <summary>
    /// Получает DC окна для рисования. В AldurPrice не используется напрямую
    /// (PrintWindow сам создаёт DC), но оставлено для будущего WgcCapture/BitBlt path.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    /// <summary>
    /// Освобождает DC, полученный через <see cref="GetWindowDC"/>.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    // --- gdi32.dll ---

    /// <summary>Создаёт memory DC, совместимый с указанным.</summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    /// <summary>Создаёт bitmap, совместимый с указанным DC.</summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    /// <summary>Выбирает GDI-объект в DC. Возвращает предыдущий объект.</summary>
    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    /// <summary>Удаляет GDI-объект (bitmap, pen, brush, ...).</summary>
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

    /// <summary>Удаляет memory DC (созданный через <see cref="CreateCompatibleDC"/>).</summary>
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(IntPtr hdc);

    /// <summary>
    /// Блочный перенос пикселей между DC. Используется для crop-операции
    /// (копирование региона из window-bitmap в final-bitmap).
    /// </summary>
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BitBlt(
        IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
}

/// <summary>
/// Win32 <c>RECT</c>: прямоугольник в экранных или клиентских координатах.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;

    public override readonly string ToString() =>
        $"{{Left={Left}, Top={Top}, Right={Right}, Bottom={Bottom}, W={Width}, H={Height}}}";
}
