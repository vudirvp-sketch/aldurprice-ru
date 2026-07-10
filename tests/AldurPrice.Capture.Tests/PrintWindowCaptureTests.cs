using AldurPrice.Capture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AldurPrice.Capture.Tests;

/// <summary>
/// Тесты <see cref="PrintWindowCapture"/>.
///
/// <para><b>Что тестируется</b>: валидация аргументов, cancellation, name,
/// error-path «окно PoE2 не найдено». Эти тесты НЕ требуют реального PoE2
/// и не делают Win32-вызовов.</para>
///
/// <para><b>Что НЕ тестируется</b>: реальный захват через <c>PrintWindow</c>.
/// Это требует запущенного PoE2 на Windows — отложено в M1.10 (интеграционные
/// тесты на реальных скриншотах). См. STATUS.md → M1.10.</para>
/// </summary>
public sealed class PrintWindowCaptureTests
{
    private static readonly ILogger<PrintWindowCapture> NullLogger =
        NullLogger<PrintWindowCapture>.Instance;

    private static readonly ILogger<Poe2WindowLocator> LocatorNullLogger =
        NullLogger<Poe2WindowLocator>.Instance;

    // ---- Name ----

    [Fact]
    public void Name_ReturnsPrintWindow()
    {
        var capture = CreateCaptureWithEmptyLocator();
        Assert.Equal("printwindow", capture.Name);
    }

    // ---- Constructor validation ----

    [Fact]
    public void Constructor_NullLocator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PrintWindowCapture(null!, NullLogger));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var locator = CreateEmptyLocator();
        Assert.Throws<ArgumentNullException>(() =>
            new PrintWindowCapture(locator, null!));
    }

    // ---- Region validation ----

    [Fact]
    public async Task CaptureAsync_NullRegion_ThrowsArgumentNullException()
    {
        var capture = CreateCaptureWithEmptyLocator();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            capture.CaptureAsync(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 0, 0, 100)]   // zero width
    [InlineData(0, 0, 100, 0)]   // zero height
    [InlineData(0, 0, -1, 100)]  // negative width
    [InlineData(0, 0, 100, -1)]  // negative height
    public async Task CaptureAsync_InvalidRegion_ThrowsArgumentException(
        int x, int y, int w, int h)
    {
        var capture = CreateCaptureWithEmptyLocator();
        var region = new CaptureRegion(x, y, w, h);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            capture.CaptureAsync(region, CancellationToken.None));
    }

    // ---- Window not found ----

    [Fact]
    public async Task CaptureAsync_Poe2NotRunning_ThrowsInvalidOperationException()
    {
        // Locator с пустым списком процессов → TryLocate вернёт null →
        // PrintWindowCapture бросит InvalidOperationException.
        // Это тестирует error-path БЕЗ Win32-вызовов (GetClientRect не достигается).
        var capture = CreateCaptureWithEmptyLocator();
        var region = new CaptureRegion(0, 0, 100, 100);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.CaptureAsync(region, CancellationToken.None));
        Assert.Contains("PoE2 window not found", ex.Message);
    }

    // ---- Cancellation ----

    [Fact]
    public async Task CaptureAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var capture = CreateCaptureWithEmptyLocator();
        var region = new CaptureRegion(0, 0, 100, 100);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            capture.CaptureAsync(region, cts.Token));
    }

    // ---- Helpers ----

    /// <summary>
    /// Создаёт <see cref="PrintWindowCapture"/> с locator'ом, у которого
    /// пустой список процессов (TryLocate всегда вернёт null).
    /// Используется для тестов, которым не нужен реальный PoE2.
    /// </summary>
    private static PrintWindowCapture CreateCaptureWithEmptyLocator()
    {
        var locator = CreateEmptyLocator();
        return new PrintWindowCapture(locator, NullLogger);
    }

    private static Poe2WindowLocator CreateEmptyLocator()
    {
        var fakeEnumerator = new EmptyProcessEnumerator();
        return new Poe2WindowLocator(fakeEnumerator, LocatorNullLogger);
    }

    private sealed class EmptyProcessEnumerator : IProcessEnumerator
    {
        public IReadOnlyList<ProcessSnapshot> GetProcesses() =>
            Array.Empty<ProcessSnapshot>();
    }
}
