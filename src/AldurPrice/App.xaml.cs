using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using AldurPrice.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AldurPrice;

/// <summary>
/// Точка входа WPF. Создаёт DI-хост через <c>Host.CreateDefaultBuilder()</c>,
/// регистрирует все сервисы (M0 — заглушки, M1+ — реальные реализации),
/// подключает crash handlers и single-instance guard.
/// </summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Mutex освобождается в OnExit, IDisposable для WPF Application нестандартен.")]
public partial class App : Application
{
    // _host инициализируется только когда _ownsMutex=true (первый экземпляр).
    // При втором запуске мы сразу выходим через Shutdown(0), до _host дело не доходит.
    private readonly IHost _host = null!;
    private readonly Mutex? _singleInstanceMutex;
    private readonly bool _ownsMutex;

    public App()
    {
        // Single-instance guard: если AldurPrice уже запущен — выходим.
        // Mutex name "Global\" делает его跨-session (для всех пользователей).
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Global\AldurPrice_SingleInstance",
            createdNew: out _ownsMutex);

        if (!_ownsMutex)
        {
            // M0 stub: в M1.8 будет activate существующего окна вместо silent exit.
            Shutdown(0);
            return;
        }

        _host = BuildHost();

        // Crash handlers — все три уровня иерархии исключений .NET.
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateDefaultBuilder();

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Загрузка config/appsettings.json — относительно текущей директории.
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            config.AddJsonFile(configPath, optional: true, reloadOnChange: true);
        });

        builder.ConfigureServices((context, services) =>
        {
            // === Configuration (strongly-typed options) ===
            services.Configure<AppOptions>(context.Configuration.GetSection("App"));
            services.Configure<PricingOptions>(context.Configuration.GetSection("Pricing"));
            services.Configure<OcrOptions>(context.Configuration.GetSection("OCR"));
            services.Configure<TranslationOptions>(context.Configuration.GetSection("Translation"));
            services.Configure<WindowOptions>(context.Configuration.GetSection("Window"));

            // === Core (чистая логика) ===
            services.AddSingleton<Core.Pricing.ItemNameParser>();
            services.AddSingleton<Core.Pricing.Levenshtein>();
            services.AddSingleton<Core.Pricing.FallbackProvider>();
            services.AddSingleton<Core.Pricing.TierFallback>();
            services.AddSingleton<Core.Translation.RussianStemmer>();
            // TranslationCache: factory грузит embedded rus.ndjson (если bundled, иначе пустой — KI-017).
            // DI выберет 2-параметровый конструктор ItemNameTranslator(runeshape, cache) →
            // этот экземпляр инжектится в translator как fallback [2].
            services.AddSingleton<Core.Translation.TranslationCache>(_ =>
                Core.Translation.TranslationCache.LoadEmbeddedOrDefault());
            services.AddSingleton<Core.Translation.RuneshapeCombinationTranslator>();
            services.AddSingleton<Core.Translation.RussianOcrPostProcessor>();
            services.AddSingleton<Core.Contracts.IItemNameTranslator, Core.Translation.ItemNameTranslator>();

            // === Data (persistence) ===
            // M3.4: services.AddSingleton<Core.Contracts.IPricingCache, Data.SqlitePricingCache>();

            // === OCR (M1.3) ===
            services.AddSingleton<Ocr.WindowsOcrEngine>();
            services.AddSingleton<Ocr.TesseractEngine>();
            services.AddSingleton<Ocr.OcrEngineResolver>();
            services.AddSingleton<Ocr.ImagePreprocessor>();
            services.AddSingleton<Ocr.LeaguePanelDetector>();
            services.AddSingleton<Ocr.OcrPipeline>();
            // M1.10: services.AddSingleton<Ocr.IOcrEngine>(sp => sp.GetRequiredService<Ocr.OcrEngineResolver>().Resolve());  // для OcrLeagueWindowReader

            // === Capture (M1.4) ===
            services.AddSingleton<Capture.IProcessEnumerator, Capture.DefaultProcessEnumerator>();
            services.AddSingleton<Capture.Poe2WindowLocator>();
            services.AddSingleton<Capture.PrintWindowCapture>();
            services.AddSingleton<Capture.ICaptureStrategy>(sp =>
                sp.GetRequiredService<Capture.PrintWindowCapture>());

            // === UI ===
            services.AddSingleton<MainWindow>();
        });

        return builder.Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!_ownsMutex)
            return;

        // Синхронный старт хоста — иначе async void в OnStartup может привести к race
        // между запуском фоновых сервисов и показом окна.
        _host.StartAsync().GetAwaiter().GetResult();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("AldurPrice v{Version} starting up", ThisAssembly.Info.Version);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            try
            {
                _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Best-effort shutdown — если воркеры не остановились за 5 сек, не блокируем выход.
                Console.Error.WriteLine($"Host stop failed: {ex.Message}");
            }
            finally
            {
                _host.Dispose();
            }
        }

        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        // M0 stub: в M4.3 будет CrashLogger.Log(ex) + dialog «Отправить отчёт».
        Console.Error.WriteLine($"[FATAL] AppDomain.UnhandledException: {ex}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // M0 stub: логирование + пометка как observed (чтобы не крашило процесс).
        Console.Error.WriteLine($"[ERROR] UnobservedTaskException: {e.Exception}");
        e.SetObserved();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // M0 stub: логирование + подавление (чтобы UI-поток не падал).
        Console.Error.WriteLine($"[ERROR] DispatcherUnhandledException: {e.Exception}");
        e.Handled = true;
    }
}

/// <summary>
/// Совместимый способ получить версию сборки без Microsoft.Extensions.DependencyModel.
/// В M0 — простая обёртка над <see cref="System.Reflection.Assembly.GetEntryAssembly"/>.
/// </summary>
internal static class ThisAssembly
{
    public static class Info
    {
        public static string Version =>
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";
    }
}
