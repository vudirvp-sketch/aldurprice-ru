using AldurPrice.Capture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AldurPrice.Capture.Tests;

/// <summary>
/// Тесты <see cref="Poe2WindowLocator"/>: matching имён процессов, кэш,
/// edge-cases (процесс без окна, несколько совпадений, не найдено).
///
/// <para>Использует <see cref="FakeProcessEnumerator"/> вместо реального
/// <see cref="DefaultProcessEnumerator"/> — позволяет тестировать логику
/// без запущенного PoE2.</para>
/// </summary>
public sealed class Poe2WindowLocatorTests
{
    private static readonly ILogger<Poe2WindowLocator> NullLogger =
        NullLogger<Poe2WindowLocator>.Instance;

    // ---- Имена процессов по умолчанию ----

    [Fact]
    public void DefaultProcessNames_ContainsSteamVariant()
    {
        // Steam — наиболее распространённый способ игры в PoE2. Должен быть первым.
        Assert.Contains("PathOfExileSteam", Poe2WindowLocator.DefaultProcessNames);
        Assert.Equal("PathOfExileSteam", Poe2WindowLocator.DefaultProcessNames[0]);
    }

    [Fact]
    public void DefaultProcessNames_ContainsStandaloneVariants()
    {
        // Standalone launcher тоже должен поддерживаться.
        Assert.Contains("PathOfExile_x64", Poe2WindowLocator.DefaultProcessNames);
        Assert.Contains("PathOfExile", Poe2WindowLocator.DefaultProcessNames);
    }

    // ---- Успешный locating ----

    [Fact]
    public void TryLocate_SteamProcess_ReturnsHandle()
    {
        var hwnd = new IntPtr(0x1234);
        var fake = new FakeProcessEnumerator(new[]
        {
            new ProcessSnapshot(100, "explorer", IntPtr.Zero, "Explorer"),
            new ProcessSnapshot(200, "PathOfExileSteam", hwnd, "Path of Exile 2"),
            new ProcessSnapshot(300, "chrome", new IntPtr(0x9999), "Chrome"),
        });
        var locator = new Poe2WindowLocator(fake, NullLogger);

        var result = locator.TryLocate();

        Assert.NotNull(result);
        Assert.Equal(hwnd, result!.Handle);
        Assert.Equal(200, result.ProcessId);
        Assert.Equal("PathOfExileSteam", result.ProcessName);
        Assert.Equal("Path of Exile 2", result.Title);
        Assert.False(result.IsInvalid);
    }

    [Fact]
    public void TryLocate_StandaloneProcess_ReturnsHandle()
    {
        // Если Steam-вариант не найден, locator должен попробовать standalone.
        var hwnd = new IntPtr(0xABCD);
        var fake = new FakeProcessEnumerator(new[]
        {
            new ProcessSnapshot(100, "notepad", new IntPtr(0x1), "Untitled"),
            new ProcessSnapshot(500, "PathOfExile_x64", hwnd, "Path of Exile 2"),
        });
        var locator = new Poe2WindowLocator(fake, NullLogger);

        var result = locator.TryLocate();

        Assert.NotNull(result);
        Assert.Equal(hwnd, result!.Handle);
        Assert.Equal("PathOfExile_x64", result.ProcessName);
    }

    // ---- Не найдено ----

    [Fact]
    public void TryLocate_NoMatchingProcess_ReturnsNull()
    {
        var fake = new FakeProcessEnumerator(new[]
        {
            new ProcessSnapshot(100, "explorer", IntPtr.Zero, "Explorer"),
            new ProcessSnapshot(200, "chrome", new IntPtr(0x9999), "Chrome"),
            new ProcessSnapshot(300, "discord", new IntPtr(0x8888), "Discord"),
        });
        var locator = new Poe2WindowLocator(fake, NullLogger);

        var result = locator.TryLocate();

        Assert.Null(result);
    }

    [Fact]
    public void TryLocate_EmptyProcessList_ReturnsNull()
    {
        var fake = new FakeProcessEnumerator(Array.Empty<ProcessSnapshot>());
        var locator = new Poe2WindowLocator(fake, NullLogger);

        Assert.Null(locator.TryLocate());
    }

    [Fact]
    public void TryLocate_ProcessWithoutWindow_SkippedAndReturnsNull()
    {
        // Процесс найден, но окна нет (loading screen, minimized to tray).
        var fake = new FakeProcessEnumerator(new[]
        {
            new ProcessSnapshot(200, "PathOfExileSteam", IntPtr.Zero, ""),
        });
        var locator = new Poe2WindowLocator(fake, NullLogger);

        Assert.Null(locator.TryLocate());
    }

    // ---- Несколько совпадений ----

    [Fact]
    public void TryLocate_MultipleMatches_ReturnsFirstInProcessNameOrder()
    {
        // Если запущены и Steam, и standalone (маловероятно, но возможно) —
        // возвращаем первый по приоритету (Steam).
        var steamHwnd = new IntPtr(0x1000);
        var standaloneHwnd = new IntPtr(0x2000);
        var fake = new FakeProcessEnumerator(new[]
        {
            new ProcessSnapshot(500, "PathOfExile_x64", standaloneHwnd, "PoE2 standalone"),
            new ProcessSnapshot(400, "PathOfExileSteam", steamHwnd, "PoE2 steam"),
        });
        var locator = new Poe2WindowLocator(fake, NullLogger);

        var result = locator.TryLocate();

        Assert.NotNull(result);
        Assert.Equal(steamHwnd, result!.Handle);
        Assert.Equal("PathOfExileSteam", result.ProcessName);
    }

    // ---- Кэш ----

    [Fact]
    public void TryLocate_SecondCall_WhenCachedHwndBecomesInvalid_Rescans()
    {
        // Кэш locator'а перепроверяет HWND через Win32 IsWindow() перед возвратом.
        // Для фейкового HWND (0x1234) IsWindow вернёт false — кэш инвалидируется,
        // и второй вызов делает повторный scan. Это реальное поведение на Windows,
        // когда игрок закрывает PoE2: cached HWND становится невалидным.
        //
        // Проверить "cache hit" path (IsWindow == true) в unit-тесте невозможно
        // без создания реального окна — это интеграционный тест (M1.10).
        var hwnd = new IntPtr(0x1234);
        var fake = new FakeProcessEnumerator(new[]
        {
            new ProcessSnapshot(200, "PathOfExileSteam", hwnd, "PoE2"),
        });
        var locator = new Poe2WindowLocator(fake, NullLogger);

        // Первый вызов — scan.
        var result1 = locator.TryLocate();
        Assert.NotNull(result1);
        Assert.Equal(1, fake.CallCount);

        // Второй вызов: кэш есть, но IsWindow(0x1234) == false → инвалидация → rescan.
        var result2 = locator.TryLocate();
        Assert.Equal(2, fake.CallCount);
        Assert.NotNull(result2);
    }

    [Fact]
    public void InvalidateCache_ForcesRescanOnNextCall()
    {
        var hwnd = new IntPtr(0x1234);
        var fake = new FakeProcessEnumerator(new[]
        {
            new ProcessSnapshot(200, "PathOfExileSteam", hwnd, "PoE2"),
        });
        var locator = new Poe2WindowLocator(fake, NullLogger);

        locator.TryLocate();
        Assert.Equal(1, fake.CallCount);

        locator.InvalidateCache();

        locator.TryLocate();
        Assert.Equal(2, fake.CallCount);
    }

    // ---- Constructor validation ----

    [Fact]
    public void Constructor_NullEnumerator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Poe2WindowLocator(null!, NullLogger));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var fake = new FakeProcessEnumerator(Array.Empty<ProcessSnapshot>());
        Assert.Throws<ArgumentNullException>(() =>
            new Poe2WindowLocator(fake, null!));
    }

    [Fact]
    public void Constructor_EmptyProcessNames_Throws()
    {
        var fake = new FakeProcessEnumerator(Array.Empty<ProcessSnapshot>());
        Assert.Throws<ArgumentException>(() =>
            new Poe2WindowLocator(fake, NullLogger, Array.Empty<string>()));
    }

    [Fact]
    public void Constructor_CustomProcessNames_UsedInsteadOfDefault()
    {
        // Если пользователь задал кастомный список — locator ищет ТОЛЬКО по нему.
        var fake = new FakeProcessEnumerator(new[]
        {
            new ProcessSnapshot(1, "PathOfExileSteam", new IntPtr(0x1), "PoE2"),  // default name, не должен сматчиться
            new ProcessSnapshot(2, "MyCustomPoE2", new IntPtr(0x2), "Custom"),
        });
        var locator = new Poe2WindowLocator(fake, NullLogger, new[] { "MyCustomPoE2" });

        var result = locator.TryLocate();

        Assert.NotNull(result);
        Assert.Equal("MyCustomPoE2", result!.ProcessName);
    }

    // ---- Enumerator throws ----

    [Fact]
    public void TryLocate_EnumeratorThrows_ReturnsNull()
    {
        var fake = new ThrowingProcessEnumerator();
        var locator = new Poe2WindowLocator(fake, NullLogger);

        // Process.GetProcesses может упасть на Windows с правовыми ограничениями.
        // Locator не должен пробрасывать исключение — возвращает null.
        Assert.Null(locator.TryLocate());
    }

    // ---- Helpers ----

    /// <summary>
    /// Fake <see cref="IProcessEnumerator"/>: возвращает предзаданный список,
    /// считает вызовы для проверки кэша.
    /// </summary>
    private sealed class FakeProcessEnumerator : IProcessEnumerator
    {
        private readonly IReadOnlyList<ProcessSnapshot> _processes;
        public int CallCount;

        public FakeProcessEnumerator(IReadOnlyList<ProcessSnapshot> processes)
        {
            _processes = processes;
        }

        public IReadOnlyList<ProcessSnapshot> GetProcesses()
        {
            CallCount++;
            return _processes;
        }
    }

    /// <summary>
    /// Fake, который всегда бросает — для проверки error handling.
    /// </summary>
    private sealed class ThrowingProcessEnumerator : IProcessEnumerator
    {
        public IReadOnlyList<ProcessSnapshot> GetProcesses() =>
            throw new InvalidOperationException("Simulated Process.GetProcesses failure.");
    }
}
