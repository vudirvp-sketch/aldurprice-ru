# Архитектура AldurPrice

> Документ описывает целевую архитектуру: структуру проектов, слои, DI, потоки данных, потокобезопасность, стратегию тестирования. Архитектура спроектирована с учётом четырёх приоритетов: производительность, качество русского перевода, чистота кода, стабильность OCR.

## 1. Структура решения

Решение `AldurPrice.slnx` разбито на 6 проектов для чёткого разделения ответственности:

```
AldurPrice/
├── src/
│   ├── AldurPrice/                          # Основное приложение (WPF, .NET 9)
│   │   ├── App.xaml(.cs)                     # Точка входа WPF, DI-хост
│   │   ├── Configuration/                    # Опции, SettingsController
│   │   │   ├── AppOptions.cs                 # Глобальные настройки
│   │   │   ├── OcrOptions.cs                 # Параметры OCR (из appsettings.json)
│   │   │   ├── PricingOptions.cs             # Параметры цен
│   │   │   ├── TranslationOptions.cs         # Параметры перевода
│   │   │   ├── WindowOptions.cs              # Регион захвата
│   │   │   ├── SettingsController.cs         # Чтение/запись appsettings.json (M1.9)
│   │   │   └── Validators/                   # IValidateOptions<T>
│   │   ├── Contracts/                        # UI-only интерфейсы (M1.7+)
│   │   │   ├── ILeagueWindowReader.cs        # Захват окна игры + OCR (UI-dependent)
│   │   │   └── IOverlayRenderer.cs           # Отрисовка оверлея
│   │   ├── Capture/                          # Слой захвата экрана (M1.4)
│   │   │   ├── PrintWindowCapture.cs         # Быстрый захват через Win32
│   │   │   ├── WgcCapture.cs                 # Windows.Graphics.Capture
│   │   │   ├── FrameDiffer.cs                # pHash skip unchanged
│   │   │   ├── Poe2WindowMonitor.cs          # Foreground detection
│   │   │   └── Poe2WindowLocator.cs          # Поиск окна PoE2
│   │   ├── Pricing/                          # Слой цен (UI-dependent, M1.5+)
│   │   │   ├── Poe2ScoutClient.cs            # HTTP к api.poe2scout.com
│   │   │   ├── PoeNinjaClient.cs             # HTTP к poe.ninja
│   │   │   ├── PricingSourceRouter.cs        # Маршрутизация источников
│   │   │   ├── PriceQuoteFormatter.cs        # Форматирование "1.5ex"
│   │   │   └── UniqueItemTypeLookup.cs       # Категории уников
│   │   ├── Overlay/                          # WPF-оверлей (M1.7+)
│   │   │   ├── OverlayWindow.xaml(.cs)       # Click-through topmost window
│   │   │   ├── PriceRowLayout.cs             # Раскладка строк цен
│   │   │   ├── PriceColorCalculator.cs       # Пороги → color
│   │   │   ├── BannerWindow.xaml(.cs)        # "Нет цены" для скилл-гемов
│   │   │   └── SetupOverlayWindow.xaml(.cs)  # Setup: рисование региона
│   │   ├── Dashboard/                        # WPF-дашборд (M1.8+)
│   │   │   ├── MainWindow.xaml(.cs)          # Главное окно
│   │   │   ├── SettingsWindow.xaml(.cs)      # Диалог настроек
│   │   │   ├── ChangelogWindow.xaml(.cs)     # Окно чейнджлога
│   │   │   ├── ViewModels/                   # MVVM ViewModels
│   │   │   │   ├── MainViewModel.cs
│   │   │   │   ├── SettingsViewModel.cs
│   │   │   │   └── ChangelogViewModel.cs
│   │   │   └── Converters/                   # Value converters
│   │   ├── Localization/                     # Локализация (M2.1+)
│   │   │   ├── StringLocalizer.cs            # IStringLocalizer<App>
│   │   │   ├── StringLocalizerExtension.cs   # XAML markup extension
│   │   │   └── LocalizationService.cs        # Смена культуры
│   │   ├── Startup/                          # Инициализация (M3.5+)
│   │   │   ├── TesseractBootstrapper.cs      # Распаковка native DLL
│   │   │   ├── AppSettingsBootstrapper.cs    # Создание config при 1-м запуске
│   │   │   ├── UpdateChecker.cs              # GitHub Releases
│   │   │   ├── CrashLogger.cs                # Crash-логи
│   │   │   └── LosslessScalingDetector.cs    # Детект LS
│   │   ├── App/                              # Фоновые воркеры (M1.6+)
│   │   │   ├── LeaguePricingWorker.cs        # Обновление цен по таймеру
│   │   │   ├── PricingCacheRefreshWorker.cs  # Рефреш кэша
│   │   │   ├── AdaptiveScanIntervalController.cs # Адаптивный polling
│   │   │   ├── BugReportService.cs           # Сбор .zip для bug report
│   │   │   └── SingleInstanceGuard.cs        # Mutex single-instance
│   │   └── Resources/                        # Иконки, темы, шрифты, .resx
│   │       ├── Strings.resx                  # EN (нейтральная)
│   │       ├── Strings.ru.resx               # RU
│   │       ├── Themes/
│   │       │   ├── Dark.xaml                 # Тёмная тема (default)
│   │       │   └── Light.xaml                # Светлая тема
│   │       ├── Icons/                        # Векторные Path Data
│   │       └── Fonts/                        # Inter + PT Sans
│   │
│   ├── AldurPrice.Core/                      # Чистая логика (no UI, no I/O)
│   │   ├── Pricing/
│   │   │   ├── ItemNameParser.cs             # Парсинг "1x", "шт", trailing
│   │   │   ├── TierFallback.cs               # GREATER/PERFECT fallback
│   │   │   ├── FallbackProvider.cs           # Fuzzy matching, diacritics
│   │   │   ├── Levenshtein.cs                # Distance для RU OCR
│   │   │   └── IdAliases.cs                  # gcp, bauble, etc.
│   │   ├── Translation/
│   │   │   ├── ItemNameTranslator.cs         # Цепочка fallback'ов
│   │   │   ├── TranslationCache.cs           # In-memory LRU
│   │   │   ├── RuneshapeCombinationTranslator.cs # poe2db mapping
│   │   │   ├── RussianStemmer.cs             # Snowball RU stemmer (conservative)
│   │   │   └── RussianOcrPostProcessor.cs    # Текстовая постобработка кириллицы (M1.3)
│   │   └── Contracts/                        # Shared interfaces для DI
│   │       ├── IItemNameTranslator.cs        # Перевод названий
│   │       ├── IPricingSource.cs             # poe2scout / poe.ninja
│   │       ├── IPricingCache.cs              # Persistent кэш цен
│   │       ├── ISystemClock.cs               # Тестопригодное время
│   │       ├── PriceQuote.cs                 # Record
│   │       ├── PricingSnapshot.cs            # Record
│   │       ├── ParsedDetectedItem.cs         # Record
│   │       └── LeagueWindowSnapshot.cs       # Record
│   │
│   ├── AldurPrice.Data/                      # Persistence (SQLite, JSON)
│   │   ├── SqlitePricingCache.cs             # IPricingCache impl
│   │   ├── TranslationRepository.cs          # IPricingCache для переводов
│   │   ├── AldurPriceDbContext.cs            # Dapper / manual SQL
│   │   ├── Migrations/
│   │   │   ├── 001_init.sql                  # Схема prices, unique_ranges
│   │   │   └── 002_indexes.sql               # Индексы
│   │   └── JsonDataLoader.cs                 # Загрузка *.ndjson, *.json
│   │
│   ├── AldurPrice.Ocr/                       # OCR-движки + пайплайн (net9.0-windows10.0.19041.0)
│   │   ├── IOcrEngine.cs                     # Интерфейс + OcrResult/OcrLine records
│   │   ├── WindowsOcrEngine.cs               # Windows.Media.Ocr (primary)
│   │   ├── TesseractEngine.cs                # Tesseract 5.2 (fallback, IDisposable)
│   │   ├── OcrEngineResolver.cs              # Выбор доступного движка
│   │   ├── ImagePreprocessor.cs              # Бинаризация + color filter (M1.3)
│   │   ├── LeaguePanelDetector.cs            # Детектор открытой панели (M1.3)
│   │   ├── OcrPreprocessOptions.cs           # Опции предобработки (M1.3)
│   │   ├── OcrPipeline.cs                    # Оркестрация (M1.3)
│   │   ├── OcrLeagueWindowReader.cs          # Главный цикл (M1.10)
│   │   ├── OcrRowLayout.cs                   # Раскладка строк по Y (M1.10)
│   │   ├── ResolutionProfiles.cs             # Профили под разрешение (M1.10)
│   │   ├── DebugOverlayService.cs            # Визуализация для отладки (M3.6)
│   │   └── OcrInspectorWindow.xaml(.cs)      # Окно "OCR Pipeline Inspector" (M3.6)
│   │
│   └── AldurPrice.Capture/                   # Захват экрана (net9.0-windows)
│       ├── ICaptureStrategy.cs               # Интерфейс + CaptureRegion record
│       ├── PrintWindowCapture.cs             # Win32 PrintWindow (primary) — ✅ M1.4
│       ├── Poe2WindowLocator.cs              # Поиск окна PoE2 (Process.GetProcesses) — ✅ M1.4
│       ├── Win32/
│       │   └── NativeMethods.cs              # P/Invoke user32/gdi32 + RECT — ✅ M1.4
│       ├── WgcCapture.cs                     # WinRT WGC (fallback для LS) — M1.4b / M2 (KI-013)
│       ├── FrameDiffer.cs                    # pHash skip unchanged — M3.1
│       ├── Poe2WindowMonitor.cs              # Foreground detection — M3.3
│       └── CaptureOptions.cs                 # Настройки захвата (M1.9 / по мере необходимости)
│
├── tests/
│   ├── AldurPrice.Core.Tests/                # Unit-тесты логики (net9.0, кроссплатформенный)
│   ├── AldurPrice.Capture.Tests/             # Тесты захвата (net9.0-windows) — ✅ M1.4
│   ├── AldurPrice.Ocr.Tests/                 # Тесты OCR (net9.0-windows, mock bitmaps) — M1.10
│   ├── AldurPrice.Data.Tests/                # Тесты SQLite (in-memory)
│   ├── AldurPrice.UI.Tests/                  # UI-тесты (FlaUI)
│   └── AldurPrice.Integration/               # Интеграционные (HTTP mock)
│
├── ocr/
│   ├── translations/                         # *.ndjson из Exiled Exchange 2 (M1.5)
│   │   ├── eng.ndjson
│   │   ├── rus.ndjson
│   │   └── LICENSE                           # Лицензия Exiled Exchange 2
│   ├── tesseract/                            # traineddata для Tesseract (M1.10 / MSBuild target)
│   │   ├── eng.traineddata
│   │   └── rus.traineddata
│   ├── runeshape-combinations-ru.json        # Маппинг рун (из poe2db.tw) — ✅ embedded в Core
│   └── unique-category-map.json              # Мультиязычные ключ. слова
│
├── scripts/
│   ├── update-translations.py                # Подтягивание *.ndjson
│   ├── parse-poe2db-runeshapes.py            # Парсинг poe2db → JSON
│   └── benchmark-ocr.py                      # Бенчмарк производительности
│
├── config/
│   ├── appsettings.json                      # Дефолтный конфиг
│   └── appsettings.schema.json               # JSON-схема
│
├── docs/                                     # Эта документация
├── .github/                                  # CI, issue templates
├── installer.iss                             # Inno Setup
├── Directory.Build.props                     # Общие настройки сборки
├── Directory.Packages.props                  # Central package management
└── AldurPrice.slnx                           # Solution file
```

> **Примечание к структуре** (см. также STATUS.md → Architecture deviations):
> - **AD-001**: Shared-интерфейсы для DI (`IItemNameTranslator`, `IPricingSource`, `IPricingCache`, `ISystemClock`) живут в `AldurPrice.Core/Contracts/`. В `AldurPrice/Contracts/` только UI-only интерфейсы (`ILeagueWindowReader`, `IOverlayRenderer`). Иначе цикл `AldurPrice.Data → AldurPrice` (если интерфейсы в AldurPrice, Data не может их реализовать без ссылки на AldurPrice, а AldurPrice ссылается на Data для регистрации в DI).
> - **AD-003**: Все OCR-компоненты (движки + пайплайн) живут в `AldurPrice.Ocr/`. Раньше в этом документе они разделялись между `AldurPrice.Ocr/` (движки) и `AldurPrice/OCR/` (пайплайн). Разделение создавало цикл `AldurPrice → AldurPrice.Ocr → AldurPrice` (пайплайн зависит от `OcrOptions` из `AldurPrice/Configuration/`).
> - **AD-004**: `RussianOcrPostProcessor` живёт в `AldurPrice.Core/Translation/`, не в `AldurPrice.Ocr/`. Это чистая текстовая обработка, помещение в Core позволяет тестировать из кроссплатформенного `AldurPrice.Core.Tests`.

### 1.1. Обоснование разбиения на проекты

Разбиение на 6 проектов даёт три конкретных benefit:

**Компиляция быстрее.** `Core` (чистая логика) компилируется за 3–5 секунд и не зависит от WPF. При разработке логики pricing/translation можно запускать тесты без пересборки UI.

**Тестирование изолированно.** `Core.Tests` запускаются без загрузки WPF/WinForms assembly, что ускоряет тест-раннер на 30–40%. `Ocr.Tests` могут использовать mock-bitmap без реального захвата экрана. `Integration` тесты с HTTP-mock запускаются отдельно и медленнее.

**AOT-готовность.** `Core` и `Data` не используют reflection (только `System.Text.Json` с source generation), что приближает нас к NativeAOT в .NET 10. WPF пока не поддерживает AOT, но когда добавят — UI можно будет компилировать отдельно.

## 2. Dependency Injection

### 2.1. Состав сервисов

Все сервисы регистрируются в `App.xaml.cs` через `IServiceCollection`. Текущее состояние (M1.3):

```csharp
// Configuration (strongly-typed options)
services.Configure<AppOptions>(configuration.GetSection("App"));
services.Configure<PricingOptions>(configuration.GetSection("Pricing"));
services.Configure<OcrOptions>(configuration.GetSection("OCR"));
services.Configure<TranslationOptions>(configuration.GetSection("Translation"));
services.Configure<WindowOptions>(configuration.GetSection("Window"));

// Core (чистая логика)
services.AddSingleton<Core.Pricing.ItemNameParser>();
services.AddSingleton<Core.Pricing.Levenshtein>();
services.AddSingleton<Core.Pricing.FallbackProvider>();
services.AddSingleton<Core.Pricing.TierFallback>();
services.AddSingleton<Core.Translation.RussianStemmer>();
services.AddSingleton<Core.Translation.TranslationCache>();
services.AddSingleton<Core.Translation.RuneshapeCombinationTranslator>();
services.AddSingleton<Core.Translation.RussianOcrPostProcessor>();
services.AddSingleton<Core.Contracts.IItemNameTranslator, Core.Translation.ItemNameTranslator>();

// OCR (M1.3)
services.AddSingleton<Ocr.WindowsOcrEngine>();
services.AddSingleton<Ocr.TesseractEngine>();
services.AddSingleton<Ocr.OcrEngineResolver>();
services.AddSingleton<Ocr.ImagePreprocessor>();
services.AddSingleton<Ocr.LeaguePanelDetector>();
services.AddSingleton<Ocr.OcrPipeline>();
// M1.10: services.AddSingleton<Ocr.IOcrEngine>(sp => sp.GetRequiredService<Ocr.OcrEngineResolver>().Resolve());  // для OcrLeagueWindowReader

// Capture (M1.4)
services.AddSingleton<Capture.IProcessEnumerator, Capture.DefaultProcessEnumerator>();
services.AddSingleton<Capture.Poe2WindowLocator>();
services.AddSingleton<Capture.PrintWindowCapture>();
services.AddSingleton<Capture.ICaptureStrategy>(sp =>
    sp.GetRequiredService<Capture.PrintWindowCapture>());

// UI (WPF)
services.AddSingleton<MainWindow>();
```

Запланированные регистрации (M1.5+):

```csharp
// Pricing (M1.5+)
services.AddHttpClient<Poe2ScoutClient>("poe2scout");
services.AddHttpClient<PoeNinjaClient>("poe.ninja");
services.AddSingleton<PricingSourceRouter>();
services.AddSingleton<Core.Contracts.IPricingCache, Data.SqlitePricingCache>();  // M3.4

// App services (M1.6+, M3.7+)
services.AddHostedService<LeaguePricingWorker>();
services.AddHostedService<PricingCacheRefreshWorker>();
services.AddSingleton<CrashLogger>();
services.AddSingleton<UpdateChecker>();
services.AddSingleton<Core.Contracts.ISystemClock, SystemClock>();
services.AddSingleton<SingleInstanceGuard>();

// UI (M1.8+)
services.AddSingleton<MainViewModel>();
services.AddTransient<SettingsViewModel>();
```

### 2.2. Lifetime-стратегия

- **Singleton** — все stateful-сервисы (кэши, воркеры, локаторы окон). Это безопасно, потому что они либо потокобезопасны (`ConcurrentDictionary`), либо используют `lock`.
- **Transient** — stateless-парсеры, форматтеры, ViewModels для диалогов. Создаются заново при каждом запросе, GC-friendly для короткоживущих объектов.
- **HostedService** — фоновые воркеры через `IHostedService` / `BackgroundService`. Корректно останавливаются при shutdown через `CancellationToken`.

## 3. Поток данных

### 3.1. Главный OCR-цикл (с оптимизациями)

```
[Timer 50-200ms adaptive]
    ↓
[1] Poe2WindowMonitor.IsForeground?  ── NO → skip, hide overlay
    ↓ YES
[2] ICaptureStrategy.Capture(region)
    ↓
[3] FrameDiffer.HasChanged(bitmap)?  ── NO → skip OCR, reuse last prices
    ↓ YES
[4] LeaguePanelDetector.Update(bitmap)  ── CLOSED → skip, hide overlay
    ↓ OPEN
[5] ImagePreprocessor.Process(bitmap)
    ↓ (binarization + color filter RGB(50,42,34) tol 47)
[6] IOcrEngine.RecognizeAsync(preprocessed)
    ↓ (Windows OCR primary, Tesseract fallback)
[7] OcrTextPostProcessor.ExtractFromRowTexts(rawText, yPositions, lang)
    ↓ (RussianOcrPostProcessor for "rus")
[string[] itemNames, int[] yPositions]
    ↓
[8] ItemNameParser.ParseDetectedItem(name, language="rus")
    ↓ (extract "1x", "шт", trailing number, OCR distortions)
[ParsedDetectedItem[] (Name, Quantity, Level)]
    ↓
[9] IItemNameTranslator.ToEnglish(name)
    ↓ (1) RuneshapeCombination → (2) TranslationCache .dat →
       (3) rus.ndjson → (4) translations.json → (5) RussianStemmer + Levenshtein
[englishNames]
    ↓
[10] IPricingCache.TryGetPrice(englishName)
    ↓ (SQLite persistent, TTL 15 min, stale fallback 60 min)
[PriceQuote[]]
    ↓
[11] PriceQuoteFormatter.Format(quote, divine, exalt, currency, suffix)
    ↓ ("1.5ex" / "1.5экс" / "0.3d" / "0.3д" / "12c" / "12с")
[(string formatted, Color color)[]]
    ↓
[12] IOverlayRenderer.Update(prices, colors, yPositions)
    ↓ (WPF Dispatcher.BeginInvoke)
[WPF overlay с цветными ценами]
```

### 3.2. Фоновое обновление цен (с persistence)

```
[HostedService LeaguePricingWorker, интервал 15 мин]
    ↓
[1] При старте: загрузка из SQLite в in-memory hot cache
    ↓
[2] Timer tick → IPricingSource.FetchPricesAsync(league)
    ↓ (HTTP с retry 3×, timeout 30s, circuit breaker)
[3] PricingSnapshot (currency, runes, uncutgems, ...)
    ↓
[4] SqlitePricingCache.Update(snapshot)
    ↓ (UPSERT в prices.db, обновление lastUpdated)
[5] Уведомить подписчиков (event PriceCacheUpdated)
    ↓
[6] Если HTTP failed → использовать кэш с пометкой "stale"
    ↓ (TTL 60 мин, потом скрыть цены)
```

## 4. Потокобезопасность

### 4.1. Кэш цен

`SqlitePricingCache` использует `SQLiteConnection` с `WriteAheadLogging` enabled — это позволяет параллельные чтения во время записи. Запись всегда в `lock`-блоке, чтение без блокировки (через `Microsoft.Data.Sqlite` с pooled connections). Для in-memory кэша (горячий слой поверх SQLite) — `ConcurrentDictionary<string, PriceQuote>`.

### 4.2. Кэш переводов

`ItemNameTranslator._recentTranslations` — `ConcurrentDictionary<string, string>` с `StringComparer.Ordinal`. Это быстрый LRU-подобный кэш (без eviction, но bounded — 10 000 записей, при переполнении очищается полностью). Для 99% кейсов этого достаточно, eviction добавит сложности без заметного benefit.

### 4.3. Оверлей

WPF-оверлей обновляется через `Dispatcher.BeginInvoke(() => { ... })` (асинхронно, без блокировки фонового потока). Все вычисления цен происходят в background-потоке OCR-цикла, в UI-поток передаются только финальные `PriceRow[]` для рендеринга. Это стандартный паттерн WPF и он работает.

## 5. Стратегия локализации

### 5.1. UI-строки (.resx)

Все пользовательские строки вынесены в `Resources/Strings.resx` (нейтральная культура = en) и `Resources/Strings.ru.resx` (русская). Доступ через `IStringLocalizer<App>` из `Microsoft.Extensions.Localization`:

```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly IStringLocalizer<App> _loc;

    [ObservableProperty] private string _title;
    [ObservableProperty] private string _languageLabel;

    public MainViewModel(IStringLocalizer<App> loc, ISystemClock clock)
    {
        _loc = loc;
        RefreshStrings();
    }

    public void RefreshStrings()
    {
        Title = _loc["Dashboard_Title"];          // "Настройки" / "Settings"
        LanguageLabel = _loc["Dashboard_Language"]; // "Язык" / "Language"
    }
}
```

В XAML — через расширение разметки:

```xml
<Window xmlns:i18n="clr-namespace:AldurPrice.Localization">
  <TextBlock Text="{i18n:StringLocalizer Key=Dashboard_Title}"/>
</Window>
```

Культура切换 в Settings: `LocalizationService.SetCulture("ru-RU")`. WPF подхватывает автоматически, все `ObservableProperty` обновляются через `RefreshStrings()`.

### 5.2. Предметные строки (JSON)

Перевод названий предметов — трёхуровневый fallback (см. [04-RU-LOCALIZATION.md](04-RU-LOCALIZATION.md)):

1. **`runeshape-combinations-ru.json`** — кастомный маппинг рунных комбинаций (ремнантов), спарсенный с poe2db.tw. Около 80–120 записей: «Руна огня» → «Rune of Fire», «Сплав Рунного отца» → «Runefather's Alloy» и т.д.
2. **`rus.ndjson`** — 4 319 предметов из Exiled Exchange 2 (базовые типы, уники, гемы).
3. **`translations.json`** — 100 предметов с мультиязычным fallback.
4. **`RussianStemmer` + `Levenshtein`** — fuzzy matching для OCR-искажений и падежей.

### 5.3. Автоопределение языка клиента

`Poe2ConfigFile.cs` читает `%USERPROFILE%\Documents\My Games\Path of Exile 2\game.ini`, извлекает `language = "russian"`, и автоматически устанавливает `OcrOptions.Language = "rus"`. Пользователь может переопределить в Settings. Это решает частую проблему: пользователь ставит RU-клиент, но забывает переключить язык OCR в приложении.

## 6. Стратегия тестирования

### 6.1. Unit-тесты (Core.Tests)

Покрывают всю чистую логику:

- `ItemNameParserTests` — парсинг количества «1x», «шт», trailing-числа, OCR-искажений (L→1, O→0, и т.д.)
- `RuneshapeCombinationTranslatorTests` — все 80+ рунных комбинаций должны переводиться корректно
- `RussianStemmerTests` — 50+ слов во всех падежах
- `RussianOcrPostProcessorTests` — нормализация «Ё»→«Е», апострофы, слияние строк
- `RussianOcrDistortionTests` — 30+ вариантов OCR-искажений кириллицы
- `LevenshteinTests` — distance для типичных подстановок (Ш↔Щ, И↔Й, Д↔Л)
- `FallbackProviderTests` — fuzzy matching, диакритики, cross-language
- `TierFallbackTests` — GREATER/PERFECT → базовый орб/руна
- `IdAliasesTests` — gcp → Gemcutter's Prism и т.д.

Цель: 90%+ покрытие Core.

### 6.2. Интеграционные тесты (Integration.Tests)

- HTTP-mock через `WireMock.Net` — тестирование `Poe2ScoutClient` и `PoeNinjaClient` с реальными JSON-ответами, сохранёнными в `tests/fixtures/`
- End-to-end пайплайн: `OcrPricingSimulator` — подаёт битмап, проверяет финальные цены
- Persistence: сохранение → перезапуск → загрузка кэша цен
- FrameDiffer: identical frames skipped, different frames processed
- AdaptiveScanInterval: интервал уменьшается при высокой CPU, увеличивается при idle

### 6.3. UI-тесты (UI.Tests)

Через `FlaUI UIA3` (WPF UI Automation):

- Открытие Settings, изменение языка, проверка обновления UI
- Переключение источника цен, проверка обновления кэша
- Setup overlay: рисование региона, сохранение
- Скриншот-тесты: сравнение с эталоном в обоих языках

### 6.4. Тесты на реальных скриншотах

`tests/fixtures/screenshots/` — коллекция из 20+ скриншотов панели рунешейпов в разных условиях:

- Разные разрешения (1080p, 1440p, 4K)
- Разная яркость UI (-0.5, 0.0, 0.5)
- С прокруткой, без прокрутки
- С русским клиентом, с английским (для regression)
- С Lossless Scaling, без него

Тест `OcrPipelineRealScreenshotsTests` прогоняет каждый скриншот через пайплайн и проверяет, что ожидаемые предметы распознаны и цены найдены.

## 7. Конфигурация

### 7.1. Структура appsettings.json

```jsonc
{
  "App": {
    "Language": "auto",              // UI language: auto / ru / en
    "LogLevel": "Information",
    "BringToForeground": true,
    "AlwaysOnTop": false,
    "CloseWithPoE2": false,
    "OpenWithPoE2": false,
    "AllOverlaysDisabled": false,
    "PricingOverlay": true,
    "Banner": true,
    "AdaptiveScanInterval": true,    // адаптивный интервал сканирования
    "PauseWhenUnfocused": true       // пауза когда PoE2 не в фокусе
  },
  "Pricing": {
    "PricingSource": "poe2scout",    // poe2scout / poe.ninja
    "League": "Runes of Aldur",
    "AutoPriceThresholds": true,
    "RedThreshold": 0.5,
    "OrangeThreshold": 1.0,
    "GreenThreshold": 5.0,
    "DisplayCurrency": "exalt",      // exalt / chaos
    "DisplayCurrencySuffix": "ru",   // en / ru (суффикс: "ex"/"экс")
    "CachePersistence": true,
    "StaleCacheTtlMinutes": 60
  },
  "OCR": {
    "Language": "rus",               // default русский
    "OcrBackend": "windows",         // windows / tesseract
    "CaptureMode": "printwindow",    // printwindow / desktop (WGC)
    "SaveDebugImages": false,
    "DebugImageIntervalSeconds": 15,
    "DebugOverlay": false,
    "ScanIntervalMs": 100,           // 50-500, с adaptive
    "OverlayScale": null,
    "EnableImagePreprocessing": true,
    "BinarizationThreshold": 145,
    "EnableTextColorFiltering": true,
    "TextColorTargetR": 50,
    "TextColorTargetG": 42,
    "TextColorTargetB": 34,
    "TextColorTolerance": 47,
    "TextColorMaxLuminance": 145,
    "TextColorMaxChannelSpread": 29,
    "OcrEngineMode": 2,              // Tesseract: 0=legacy, 1=LSTM+legacy, 2=LSTM only
    "BypassOcrCache": false,
    "TesseractDataPath": "",
    "FrameDiffing": true             // пропускать unchanged frames
  },
  "Translation": {
    "RuneshapeCombinationsPath": "ocr/runeshape-combinations-ru.json",
    "AutoUpdateFromExiledExchange": true
  },
  "Update": {
    "AutoUpdate": true,
    "GithubToken": null
  },
  "Window": {
    "InitialSetupComplete": false,
    "CustomOffsetX": null,
    "CustomOffsetY": null,
    "CustomWidth": null,
    "CustomHeight": null
  }
}
```

### 7.2. Валидация

Каждая секция валидируется через `IValidateOptions<T>`:

- `AppOptionsValidator` — `Language` ∈ {`auto`, `ru`, `en`}
- `PricingOptionsValidator` — `RedThreshold < OrangeThreshold < GreenThreshold`
- `OcrOptionsValidator` — `ScanIntervalMs` ∈ [50, 500], `BinarizationThreshold` ∈ [0, 255]

При невалидном конфиге — warning в лог + fallback на дефолт. Приложение не падает.

JSON-схема `config/appsettings.schema.json` даёт автокомплит в VS Code / Rider.

## 8. Сборка и публикация

### 8.1. Debug-сборка

```bash
dotnet build -c Debug
dotnet run --project src/AldurPrice
```

Framework-dependent, быстрая сборка, hot-reload через `dotnet watch`.

### 8.2. Release-сборка (single-file)

```bash
dotnet publish src/AldurPrice -c Release -r win-x64 --self-contained
```

Опции в `AldurPrice.csproj`:

```xml
<PublishSingleFile>true</PublishSingleFile>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
<SelfContained>true</SelfContained>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<DebugType>none</DebugType>
<DebugSymbols>false</DebugSymbols>
<TrimMode>partial</TrimMode>
<ReadyToRun>true</ReadyToRun>
```

### 8.3. Установщик

Inno Setup (`installer.iss`) создаёт:
- `%LOCALAPPDATA%\AldurPrice\` — файлы приложения
- `%APPDATA%\AldurPrice\config\` — конфиг
- Пункт в Пуске + деинсталлятор

### 8.4. CI/CD (GitHub Actions)

`.github/workflows/ci.yml`:

- **On push/PR:** `dotnet test` на Windows-latest
- **On tag `v*`:** `dotnet publish` + загрузка .zip + .exe в GitHub Release
- **Weekly:** `scripts/update-translations.py` → commit если есть изменения

Подробности CI — в [06-SETUP.md](06-SETUP.md).
