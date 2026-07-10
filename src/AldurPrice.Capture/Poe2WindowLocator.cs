using System.Diagnostics;
using AldurPrice.Capture.Win32;
using Microsoft.Extensions.Logging;

namespace AldurPrice.Capture;

/// <summary>
/// Поиск окна Path of Exile 2 среди запущенных процессов. Возвращает HWND
/// главного окна игры для последующего захвата через <see cref="PrintWindowCapture"/>.
///
/// <para><b>Имена процессов</b>: PoE2 распространяется через Steam и standalone
/// launcher, имена исполняемых файлов различаются. <see cref="DefaultProcessNames"/>
/// покрывает известные варианты; список расширяется по мере обнаружения новых
/// (см. KI-013 — список нужно верифицировать на реальном PoE2 install).</para>
///
/// <para><b>Кэширование</b>: сканирование <c>Process.GetProcesses()</c> относительно
/// дорого (20-50 мс на Windows при ~200 процессах). OCR-цикл опрашивает захват
/// каждые 50-200 мс — без кэша мы бы тратили ~25% CPU на scan. Кэш хранит
/// найденный HWND <see cref="CacheTtlMilliseconds"/> (default 5 с), валидность
/// handle перепроверяется через <c>IsWindow</c> перед возвращением.</para>
///
/// <para><b>Тестопригодность</b>: реальное перечисление процессов вынесено в
/// <see cref="IProcessEnumerator"/>. В production используется
/// <see cref="DefaultProcessEnumerator"/> (обёртка над <c>Process.GetProcesses</c>),
/// в unit-тестах — fake с предзаданным списком. Это позволяет тестировать логику
/// matching'а и кэша без реального PoE2 процесса.</para>
///
/// <para><b>Thread safety</b>: кэш защищён <c>lock</c>. Одновременные вызовы
/// <see cref="TryLocate"/> безопасны — худший случай: два потока одновременно
/// сделают scan, но результат будет консистентным (last writer wins для кэша).</para>
/// </summary>
public sealed class Poe2WindowLocator
{
    /// <summary>
    /// Имена процессов PoE2 (без расширения <c>.exe</c>), которые_locator
    /// пытается найти. Порядок важен: сначала Steam-вариант (распространённее),
    /// потом standalone.
    ///
    /// <para><b>Верификация</b>: список основан на публичной информации о PoE2
    /// early access. Если на реальной машине PoE2 не детектится — см. KI-013.</para>
    /// </summary>
    public static readonly string[] DefaultProcessNames =
    [
        "PathOfExileSteam",      // Steam-версия (наиболее распространённая)
        "PathOfExile_x64",       // Standalone 64-bit
        "PathOfExile",           // Standalone legacy / 32-bit fallback
        "PathOfExileSteam_x64"   // Steam 64-bit (future-proof)
    ];

    /// <summary>
    /// TTL кэша найденного HWND, в миллисекунтах. После истечения locator
    /// делает новый scan. 5 с — баланс между «не сканировать слишком часто»
    /// и «быстро заметить, что игрок перезапустил PoE2».
    /// </summary>
    public const int CacheTtlMilliseconds = 5_000;

    private readonly IProcessEnumerator _enumerator;
    private readonly ILogger<Poe2WindowLocator> _logger;
    private readonly string[] _processNames;
    private readonly object _cacheLock = new();
    private CachedHandle? _cached;

    /// <summary>
    /// Создаёт locator с default списком имён процессов.
    /// </summary>
    public Poe2WindowLocator(IProcessEnumerator enumerator, ILogger<Poe2WindowLocator> logger)
        : this(enumerator, logger, DefaultProcessNames)
    {
    }

    /// <summary>
    /// Создаёт locator с кастомным списком имён процессов (для тестов / config override).
    /// </summary>
    public Poe2WindowLocator(
        IProcessEnumerator enumerator,
        ILogger<Poe2WindowLocator> logger,
        string[] processNames)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processNames = processNames ?? throw new ArgumentNullException(nameof(processNames));
        if (_processNames.Length == 0)
            throw new ArgumentException("At least one process name is required.", nameof(processNames));
    }

    /// <summary>
    /// Найти окно PoE2. Возвращает <c>null</c>, если игра не запущена или
    /// окно ещё не создано (например, во время loading screen).
    /// </summary>
    /// <returns>
    /// <see cref="Poe2WindowHandle"/> с HWND и метаданными, или <c>null</c>.
    /// </returns>
    public Poe2WindowHandle? TryLocate()
    {
        // 1. Проверяем кэш — если валиден (HWND ещё существует и TTL не истёк),
        //    возвращаем без scan. Это быстрый path для OCR-цикла.
        lock (_cacheLock)
        {
            if (_cached is { } cached && IsCacheValid(cached))
            {
                return cached.Handle;
            }
            // Кэш невалиден — забываем, ниже сделаем новый scan.
            _cached = null;
        }

        // 2. Scan — вне lock, чтобы не блокировать другие потоки во время медленного Process.GetProcesses.
        var handle = ScanOnce();
        if (handle is null)
        {
            _logger.LogDebug("PoE2 window not found. Tried process names: {Names}",
                string.Join(", ", _processNames));
            return null;
        }

        // 3. Сохраняем в кэш. Если другой поток уже записал — перезаписываем
        //    (handle'ы равнозначны, last writer wins).
        lock (_cacheLock)
        {
            _cached = new CachedHandle(handle, Environment.TickCount64);
        }

        _logger.LogInformation("PoE2 window located: pid={Pid}, name='{Name}', hwnd=0x{Hwnd:X}, title='{Title}'",
            handle.ProcessId, handle.ProcessName, handle.Handle.ToInt64(), handle.Title);
        return handle;
    }

    /// <summary>
    /// Сбросить кэш. Полезно в тестах и при получении события «окно закрылось»
    /// (future: Poe2WindowMonitor в M3.3 будет дергать это при EVENT_OBJECT_DESTROY).
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cached = null;
        }
    }

    private static bool IsCacheValid(CachedHandle cached)
    {
        var ageMs = Environment.TickCount64 - cached.CachedAtMs;
        if (ageMs >= CacheTtlMilliseconds)
            return false;
        // HWND мог стать невалидным (игра закрылась, окно пересоздалось).
        // IsWindow быстрый (~1 мкс), не требует Process scan.
        return NativeMethods.IsWindow(cached.Handle.Handle);
    }

    private Poe2WindowHandle? ScanOnce()
    {
        IReadOnlyList<ProcessSnapshot> processes;
        try
        {
            processes = _enumerator.GetProcesses();
        }
        catch (Exception ex)
        {
            // Process.GetProcesses может упасть на Windows с правовыми ограничениями
            // (например, системные процессы). Это не должно валить locator.
            _logger.LogError(ex, "Failed to enumerate processes.");
            return null;
        }

        foreach (var name in _processNames)
        {
            foreach (var p in processes)
            {
                if (!string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Процесс найден, но окна ещё нет (loading screen, minimized to tray).
                if (p.MainWindowHandle == IntPtr.Zero)
                {
                    _logger.LogDebug("Process '{Name}' (pid={Pid}) found but has no main window.",
                        name, p.Id);
                    continue;
                }

                return new Poe2WindowHandle(
                    Handle: p.MainWindowHandle,
                    ProcessId: p.Id,
                    ProcessName: p.ProcessName,
                    Title: p.MainWindowTitle ?? string.Empty);
            }
        }

        return null;
    }

    private sealed record CachedHandle(Poe2WindowHandle Handle, long CachedAtMs);
}

/// <summary>
/// Найденное окно PoE2: HWND + метаданные процесса. Immutable.
/// </summary>
public sealed record Poe2WindowHandle(IntPtr Handle, int ProcessId, string ProcessName, string Title)
{
    /// <summary>True, если HWND невалиден (например, нулевой).</summary>
    public bool IsInvalid => Handle == IntPtr.Zero;
}

/// <summary>
/// Абстракция над <c>Process.GetProcesses()</c> для тестопригодности.
/// В production используется <see cref="DefaultProcessEnumerator"/>,
/// в тестах — fake с предзаданным списком.
/// </summary>
public interface IProcessEnumerator
{
    /// <summary>Возвращает snapshot всех запущенных процессов.</summary>
    IReadOnlyList<ProcessSnapshot> GetProcesses();
}

/// <summary>
/// Immutable-представление процесса для <see cref="IProcessEnumerator"/>.
/// Содержит только нужные locator'у поля (не обёртка над <see cref="Process"/>,
/// который IDisposable и имеет много лишнего).
/// </summary>
public sealed record ProcessSnapshot(int Id, string ProcessName, IntPtr MainWindowHandle, string? MainWindowTitle);

/// <summary>
/// Production-реализация <see cref="IProcessEnumerator"/>: обёртка над
/// <c>Process.GetProcesses()</c>. Корректно освобождает все <see cref="Process"/>
/// объекты (каждый из них держит handle).
/// </summary>
public sealed class DefaultProcessEnumerator : IProcessEnumerator
{
    /// <inheritdoc/>
    public IReadOnlyList<ProcessSnapshot> GetProcesses()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            // На Linux/Mac вернёт пустой массив; на Windows с правовыми ограничениями — exception.
            return Array.Empty<ProcessSnapshot>();
        }

        try
        {
            var snapshots = new List<ProcessSnapshot>(processes.Length);
            foreach (var p in processes)
            {
                try
                {
                    // ProcessName может упасть (Access Denied для системных процессов).
                    // Пропускаем такие — PoE2 пользовательский процесс, доступа хватит.
                    var name = p.ProcessName;
                    snapshots.Add(new ProcessSnapshot(
                        Id: p.Id,
                        ProcessName: name,
                        MainWindowHandle: p.MainWindowHandle,
                        MainWindowTitle: p.MainWindowTitle));
                }
                catch (Exception)
                {
                    // Best-effort: пропускаем процессы, к которым нет доступа.
                }
            }
            return snapshots;
        }
        finally
        {
            // Process реализует IDisposable (держит kernel handle). Освобождаем все,
            // даже если какой-то упал при чтении свойств.
            foreach (var p in processes)
            {
                try { p.Dispose(); }
                catch (Exception) { /* best-effort */ }
            }
        }
    }
}
