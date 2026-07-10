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
│   │   │   ├── OcrOptions.cs                 # Параметры OCR
│   │   │   ├── PricingOptions.cs             # Параметры цен
│   │   │   ├── TranslationOptions.cs         # Параметры перевода
│   │   │   ├── WindowOptions.cs              # Регион захвата
│   │   │   ├── SettingsController.cs         # Чтение/запись appsettings.json
│   │   │   └── Validators/                   # IValidateOptions<T>
│   │   ├── Contracts/                        # Интерфейсы для DI
│   │   │   ├── IPricingSource.cs             # poe2scout / poe.ninja
│   │   │   ├── IPricingCache.cs              # Persistent кэш цен
│   │   │   ├── ILeagueWindowReader.cs        # Захват окна игры + OCR
│   │   │   ├── IOverlayRenderer.cs           # Отрисовка оверлея
│   │   │   ├── IOcrEngine.cs                 # Windows OCR / Tesseract
│   │   │   ├── ICaptureStrategy.cs           # PrintWindow / WGC
│   │   │   ├── IItemNameTranslator.cs        # Перевод названий
│   │   │   └── ISystemClock.cs               # Тестопригодное время
│   │   ├── Capture/                          # Слой захвата экрана
│   │   │   ├── PrintWindowCapture.cs         # Быстрый захват через Win32
│   │   │   ├── WgcCapture.cs                 # Windows.Graphics.Capture
│   │   │   ├── FrameDiffer.cs                # pHash skip unchanged
│   │   │   ├── Poe2WindowMonitor.cs          # Foreground detection
│   │   │   └── Poe2WindowLocator.cs          # Поиск окна PoE2
│   │   ├── OCR/                              # OCR-пайплайн (UI-dependent)
│   │   │   ├── OcrPipeline.cs                # Оркестрация
│   │   │   ├── OcrLeagueWindowReader.cs      # Главный цикл
│   │   │   ├── ImagePreprocessor.cs          # Бинаризация + color filter
│   │   │   ├── LeaguePanelDetector.cs        # Детектор открытой панели
│   │   │   ├── OcrTextPostProcessor.cs       # Нормализация текста
│   │   │   ├── RussianOcrPostProcessor.cs    # Специфика кириллицы
│   │   │   ├── OcrRowLayout.cs               # Раскладка строк по Y
│   │   │   ├── ResolutionProfiles.cs         # Профили под разрешение
│   │   │   ├── DebugOverlayService.cs        # Визуализация для отладки
│   │   │   └── OcrInspectorWindow.xaml(.cs)  # Окно "OCR Pipeline Inspector"
│   │   ├── Pricing/                          # Слой цен (UI-dependent)
│   │   │   ├── Poe2ScoutClient.cs            # HTTP к api.poe2scout.com
│   │   │   ├── PoeNinjaClient.cs             # HTTP к poe.ninja
│   │   │   ├── PricingSourceRouter.cs        # Маршрутизация источников
│   │   │   ├── ItemNameParser.cs             # Парсинг количества, aliases
│   │   │   ├── PriceQuoteFormatter.cs        # Форматирование "1.5ex"
│   │   │   └── UniqueItemTypeLookup.cs       # Категории уников
│   │   ├── Overlay/                          # WPF-оверлей
│   │   │   ├── OverlayWindow.xaml(.cs)       # Click-through topmost window
│   │   │   ├── PriceRowLayout.cs             # Раскладка строк цен
│   │   │   ├── PriceColorCalculator.cs       # Пороги → color
│   │   │   ├── BannerWindow.xaml(.cs)        # "Нет цены" для скилл-гемов
│   │   │   └── SetupOverlayWindow.xaml(.cs)  # Setup: рисование региона
│   │   ├── Dashboard/                        # WPF-дашборд
│   │   │   ├── MainWindow.xaml(.cs)          # Главное окно
│   │   │   ├── SettingsWindow.xaml(.cs)      # Диалог настроек
│   │   │   ├── ChangelogWindow.xaml(.cs)     # Окно чейнджлога
│   │   │   ├── ViewModels/                   # MVVM ViewModels
│   │   │   │   ├── MainViewModel.cs
│   │   │   │   ├── SettingsViewModel.cs
│   │   │   │   └── ChangelogViewModel.cs
│   │   │   └── Converters/                   # Value converters
│   │   ├── Localization/                     # Локализация
│   │   │   ├── StringLocalizer.cs            # IStringLocalizer<App>
│   │   │   ├── StringLocalizerExtension.cs   # XAML markup extension
│   │   │   └── LocalizationService.cs        # Смена культуры
│   │   ├── Startup/                          # Инициализация
│   │   │   ├── TesseractBootstrapper.cs      # Распаковка native DLL
│   │   │   ├── AppSettingsBootstrapper.cs    # Создание config при 1-м запуске
│   │   │   ├── UpdateChecker.cs              # GitHub Releases
│   │   │   ├── CrashLogger.cs                # Crash-логи
│   │   │   └── LosslessScalingDetector.cs    # Детект LS
│   │   ├── App/                              # Фоновые воркеры
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
│   │   │   └── RussianStemmer.cs             # Snowball RU stemmer
│   │   └── Contracts/                        # Shared interfaces
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
│   ├── AldurPrice.Ocr/                       # OCR-движки
│   │   ├── IOcrEngine.cs                     # Интерфейс
│   │   ├── WindowsOcrEngine.cs               # Windows.Media.Ocr
│   │   ├── TesseractEngine.cs                # Tesseract 5.2
│   │   ├── OcrEngineResolver.cs              # Выбор доступного движка
│   │   └── OcrOptions.cs                     # Опции OCR
│   │
│   └── AldurPrice.Capture/                   # Захват экрана
│       ├── ICaptureStrategy.cs
│       ├── PrintWindowCapture.cs             # Win32 PrintWindow
│       ├── WgcCapture.cs                     # WinRT WGC
│       └── CaptureOptions.cs
│
├── tests/
│   ├── AldurPrice.Core.Tests/                # Unit-тесты логики
│   ├── AldurPrice.Ocr.Tests/                 # Тесты OCR (mock bitmaps)
│   ├── AldurPrice.Data.Tests/                # Тесты SQLite (in-memory)
│   ├── AldurPrice.UI.Tests/                  # UI-тесты (FlaUI)
│   └── AldurPrice.Integration/               # Интеграционные (HTTP mock)
│
├── ocr/
│   ├── translations/                         # *.ndjson из Exiled Exchange 2
│   │   ├── eng.ndjson
│   │   ├── rus.ndjson
│   │   └── LICENSE                           # Лицензия Exiled Exchange 2
│   ├── tesseract/                            # traineddata для Tesseract
│   │   ├── eng.traineddata
│   │   └── rus.traineddata
│   ├── runeshape-combinations-ru.json        # Маппинг рун (из poe2db.tw)
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

### 1.1. Обоснование разбиения на проекты

Разбиение на 6 проектов даёт три конкретных benefit:

**Компиляция быстрее.** `Core` (чистая логика) компилируется за 3–5 секунд и не зависит от WPF. При разработке логики pricing/translation можно запускать тесты без пересборки UI.

**Тестирование изолированно.** `Core.Tests` запускаются без загрузки WPF/WinForms assembly, что ускоряет тест-раннер на 30–40%. `Ocr.Tests` могут использовать mock-bitmap без реального захвата экрана. `Integration` тесты с HTTP-mock запускаются отдельно и медленнее.

**AOT-готовность.** `Core` и `Data` не используют reflection (только `System.Text.Json` с source generation), что приближает нас к NativeAOT в .NET 10. WPF пока не поддерживает AOT, но когда добавят — UI можно будет компилировать отдельно.

## 2. Dependency Injection

### 2.1. Состав сервисов

Все сервисы регистрируются в `App.xaml.cs` через `IServiceCollection`:

```csharp
// Configuration
services.AddSingleton<IOptionsMonitor<AppOptions>, AppOptions>();
services.AddSingleton<IOptionsMonitor<OcrOptions>, OcrOptions>();
services.AddSingleton<IOptionsMonitor<PricingOptions>, PricingOptions>();
services.AddSingleton<SettingsController>();

// Capture
services.AddSingleton<ICaptureStrategy, PrintWindowCapture>();  // default
services.AddSingleton<FrameDiffer>();
services.AddSingleton<Poe2WindowLocator>();
services.AddSingleton<Poe2WindowMonitor>();

// OCR
services.AddSingleton<IOcrEngine, WindowsOcrEngine>();          // primary
services.AddSingleton<IOcrEngine, TesseractEngine>();            // fallback (keyed)
services.AddSingleton<ImagePreprocessor>();
services.AddSingleton<LeaguePanelDetector>();
services.AddSingleton<OcrPipeline>();

// Translation
services.AddSingleton<IItemNameTranslator, ItemNameTranslator>();
services.AddSingleton<TranslationCache>();
services.AddSingleton<RuneshapeCombinationTranslator>();

// Pricing
services.AddHttpClient<Poe2ScoutClient>("poe2scout");
services.AddHttpClient<PoeNinjaClient>("poe.ninja");
services.AddSingleton<PricingSourceRouter>();
services.AddSingleton<IPricingCache, SqlitePricingCache>();
services.AddSingleton<ItemNameParser>();

// App services
services.AddHostedService<LeaguePricingWorker>();
services.AddHostedService<PricingCacheRefreshWorker>();
services.AddSingleton<CrashLogger>();
services.AddSingleton<UpdateChecker>();
services.AddSingleton<ISystemClock, SystemClock>();
services.AddSingleton<SingleInstanceGuard>();

// UI (WPF)
services.AddSingleton<MainWindow>();
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
