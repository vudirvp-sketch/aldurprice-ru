# Дорожная карта AldurPrice

> Документ описывает milestones, задачи, оценку трудозатрат и критерии готовности. Roadmap построен итеративно: каждый milestone delivers рабочий билд, который можно использовать и тестировать. Оценки указаны в «идеальных часах» (без учёта переключений контекста, ревью, багфиксов).

## Обзор milestones

| # | Milestone | Длительность | Цель | Релиз |
|---|---|---|---|---|
| **M0** | Каркас проекта | 1 неделя | Структура, CI, скелет всех проектов | v0.1.0-alpha |
| **M1** | MVP: базовая русификация и OCR | 3 недели | Рабочий билд, RU OCR, рунные комбинации, цены | v0.2.0-alpha |
| **M2** | Полная локализация UI + WPF | 3 недели | WPF-дашборд, .resx, автоопределение языка | v0.3.0-beta |
| **M3** | Производительность и стабильность | 2 недели | Frame-diffing, SQLite, adaptive polling | v0.4.0-beta |
| **M4** | Релиз-готовность | 1 неделя | Установщик, автообновление, документация | v1.0.0 |

**Итого:** ~10 недель (2.5 месяца) при 20 часов/неделю.

---

## M0 — Каркас проекта (v0.1.0-alpha)

**Цель:** Создать структуру проекта с нуля, CI, и базовый скелет всех 6 подпроектов. К концу milestone'а решение компилируется, CI green, тестовый шаблон проходит. Реальной функциональности ещё нет.

### Задачи

#### M0.1 — Создание репозитория и структуры
- [ ] Создать GitHub-репозиторий `aldurprice` (public)
- [ ] Инициализировать с `README.md`, `LICENSE` (MIT), `.gitignore`
- [ ] Создать структуру директорий: `src/`, `tests/`, `docs/`, `ocr/`, `scripts/`, `config/`, `.github/`
- [ ] Добавить `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`
- [ ] Оценка: 4 часа

#### M0.2 — Создание 6 проектов
- [ ] `src/AldurPrice/` — основной WPF-проект (скелет `App.xaml`, `MainWindow.xaml`)
- [ ] `src/AldurPrice.Core/` — class library, скелет `Translation/`, `Pricing/`, `Contracts/`
- [ ] `src/AldurPrice.Data/` — class library, скелет `SqlitePricingCache.cs`
- [ ] `src/AldurPrice.Ocr/` — class library, скелет `IOcrEngine.cs`, `WindowsOcrEngine.cs`
- [ ] `src/AldurPrice.Capture/` — class library, скелет `ICaptureStrategy.cs`, `PrintWindowCapture.cs`
- [ ] `tests/AldurPrice.Core.Tests/` — xUnit проект с одним smoke-тестом
- [ ] Настроить `Directory.Build.props` (net9.0-windows, nullable, latest)
- [ ] Настроить `Directory.Packages.props` (central package management)
- [ ] Создать `AldurPrice.slnx` со всеми проектами и reference'ами
- [ ] Оценка: 8 часов

#### M0.3 — DI-хост и точки входа
- [ ] `App.xaml.cs` — `IHost` с `Microsoft.Extensions.Hosting`, регистрация всех сервисов (заглушки)
- [ ] `Program.cs` (если нужно для single-instance mutex) или в `App.xaml.cs`
- [ ] `MainWindow.xaml` — простое окно "Hello AldurPrice"
- [ ] Crash handlers: `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, `Dispatcher.UnhandledException`
- [ ] Оценка: 6 часов

#### M0.4 — CI/CD
- [ ] `.github/workflows/ci.yml` — build + test на Windows-latest при push/PR
- [ ] `.github/workflows/release.yml` — publish + GitHub Release при tag `v*`
- [ ] `.github/workflows/translations.yml` — еженедельное обновление переводов (cron)
- [ ] `.github/ISSUE_TEMPLATE/bug_report.md`, `feature_request.md`, `translation_error.md`
- [ ] `.github/PULL_REQUEST_TEMPLATE.md`
- [ ] Оценка: 4 часа

#### M0.5 — Базовые тесты и coverage
- [ ] Один smoke-тест в `AldurPrice.Core.Tests` (например, `RussianStemmerTests` с 3 кейсами)
- [ ] Coverlet + report generation
- [ ] README с бейджами CI
- [ ] Оценка: 3 часа

### Критерии готовности M0
- [ ] Репозиторий создан, 6 проектов компилируются
- [ ] `dotnet build` без warnings
- [ ] `dotnet test` — smoke-тест проходит
- [ ] CI green на GitHub Actions (Windows-latest)
- [ ] `MainWindow` открывается с "Hello AldurPrice"
- [ ] Тег `v0.1.0-alpha` с GitHub Release (source-only, без бинарников)

---

## M1 — MVP: базовая русификация и OCR (v0.2.0-alpha)

**Цель:** Рабочий билд, который распознаёт русский текст панели рунешейпов и показывает цены для рунных комбинаций. UI остаётся минимальным, но OCR и перевод работают с русским клиентом end-to-end.

### Задачи

#### M1.1 — Парсер poe2db.tw
- [ ] `scripts/parse-poe2db-runeshapes.py` — Python-скрипт парсинга
  - Скачать `https://poe2db.tw/ru/Runeshape_Combinations`
  - Извлечь все рунные комбинации (English + Russian)
  - Сохранить в `ocr/runeshape-combinations-ru.json`
  - CLI: `--output`, `--verbose`, `--dry-run`
- [ ] Тест парсера на сохранённой HTML-фикстуре
- [ ] Запустить парсер, закоммитить первый `runeshape-combinations-ru.json`
- [ ] Оценка: 8 часов

#### M1.2 — Core: ItemNameParser и ItemNameTranslator
- [ ] `Core/Pricing/ItemNameParser.cs` — парсинг "1x", "шт", trailing-числа, OCR-искажений
- [ ] `Core/Pricing/Levenshtein.cs` — алгоритм distance с early exit
- [ ] `Core/Pricing/FallbackProvider.cs` — fuzzy matching, диакритики
- [ ] `Core/Pricing/TierFallback.cs` — GREATER/PERFECT fallback
- [ ] `Core/Pricing/IdAliases.cs` — gcp, bauble, и т.д.
- [ ] `Core/Translation/RuneshapeCombinationTranslator.cs` — загрузка JSON, lookup, stem, Levenshtein
- [ ] `Core/Translation/RussianStemmer.cs` — портированный Snowball stemmer
- [ ] `Core/Translation/ItemNameTranslator.cs` — цепочка fallback'ов
- [ ] `Core/Translation/TranslationCache.cs` — in-memory LRU
- [ ] Тесты: `ItemNameParserTests`, `RuneshapeCombinationTranslatorTests`, `RussianStemmerTests`, `LevenshteinTests`, `RussianOcrDistortionTests`
- [ ] Оценка: 16 часов

#### M1.3 — OCR-движки и пайплайн
- [ ] `Ocr/IOcrEngine.cs` — интерфейс
- [ ] `Ocr/WindowsOcrEngine.cs` — обёртка над `Windows.Media.Ocr`
- [ ] `Ocr/TesseractEngine.cs` — обёртка над Tesseract 5.2
- [ ] `Ocr/OcrEngineResolver.cs` — выбор доступного движка
- [ ] `OCR/ImagePreprocessor.cs` — бинаризация + color filter RGB(50,42,34) tol 47
- [ ] `OCR/LeaguePanelDetector.cs` — детектор открытой панели по яркости
- [ ] `OCR/OcrTextPostProcessor.cs` — нормализация текста
- [ ] `OCR/RussianOcrPostProcessor.cs` — нормализация Ё, апострофов, слияние строк
- [ ] `OCR/OcrPipeline.cs` — оркестрация capture → preprocess → OCR → postprocess
- [ ] `OCR/OcrLeagueWindowReader.cs` — главный цикл чтения
- [ ] `OCR/ResolutionProfiles.cs` — профили под разрешение
- [ ] Тесты: `ImagePreprocessorTests`, `LeaguePanelDetectorTests`, `OcrTextPostProcessorTests`, `RussianOcrPostProcessorTests`
- [ ] Оценка: 20 часов

#### M1.4 — Захват экрана
- [ ] `Capture/ICaptureStrategy.cs` — интерфейс
- [ ] `Capture/PrintWindowCapture.cs` — Win32 PrintWindow (primary)
- [ ] `Capture/WgcCapture.cs` — Windows.Graphics.Capture (fallback для Lossless Scaling)
- [ ] `Capture/Poe2WindowLocator.cs` — поиск окна PoE2 по `Process.GetProcessesByName`
- [ ] Тесты: `PrintWindowCaptureTests` (mock), `Poe2WindowLocatorTests`
- [ ] Оценка: 8 часов

#### M1.5 — Источники цен
- [ ] `Pricing/Poe2ScoutClient.cs` — HTTP к `api.poe2scout.com/poe2`
- [ ] `Pricing/PoeNinjaClient.cs` — HTTP к `poe.ninja/poe2/api/economy/`
- [ ] `Pricing/PricingSourceRouter.cs` — маршрутизация между источниками
- [ ] HTTP mock через WireMock.Net для тестов
- [ ] Тесты: `Poe2ScoutClientTests`, `PoeNinjaClientTests`, `PricingSourceRouterTests`
- [ ] Оценка: 10 часов

#### M1.6 — Кэш цен (in-memory, без persistence)
- [ ] `Pricing/InMemoryPricingCache.cs` — `ConcurrentDictionary`-based, TTL 15 мин
- [ ] `App/LeaguePricingWorker.cs` — `BackgroundService` с таймером 15 мин
- [ ] Тесты: `InMemoryPricingCacheTests`, `LeaguePricingWorkerTests`
- [ ] Оценка: 6 часов

#### M1.7 — Оверлей (минимальный)
- [ ] `Overlay/OverlayWindow.xaml(.cs)` — click-through topmost WPF window (Win32 interop)
- [ ] `Overlay/PriceRowLayout.cs` — раскладка строк по Y
- [ ] `Overlay/PriceColorCalculator.cs` — пороги → color
- [ ] `Overlay/SetupOverlayWindow.xaml(.cs)` — рисование региона мышью
- [ ] Тесты: `PriceRowLayoutTests`, `PriceColorCalculatorTests`
- [ ] Оценка: 12 часов

#### M1.8 — Dashboard (минимальный)
- [ ] `Dashboard/MainWindow.xaml(.cs)` — простое окно с кнопкой "Запуск" и лог-панелью
- [ ] `Dashboard/MainViewModel.cs` — MVVM
- [ ] `Dashboard/SettingsWindow.xaml(.cs)` — базовые настройки (язык OCR, источник цен, лига)
- [ ] Оценка: 8 часов

#### M1.9 — Конфигурация
- [ ] `Configuration/AppOptions.cs`, `OcrOptions.cs`, `PricingOptions.cs`, `WindowOptions.cs`
- [ ] `Configuration/SettingsController.cs` — чтение/запись `appsettings.json`, hot-reload через `FileSystemWatcher`
- [ ] `Configuration/Poe2ConfigFile.cs` — чтение `game.ini` PoE2, автоопределение языка
- [ ] `Configuration/Validators/` — `IValidateOptions<T>` для каждого блока
- [ ] `config/appsettings.json` + `config/appsettings.schema.json`
- [ ] Тесты: `AppOptionsTests`, `OcrOptionsTests`, `Poe2ConfigFileTests`
- [ ] Оценка: 8 часов

#### M1.10 — Тестирование на реальных скриншотах
- [ ] Собрать 10+ скриншотов панели рунешейпов с русским клиентом (1080p, 1440p, 4K)
- [ ] `tests/fixtures/screenshots/` — сохранение фикстур
- [ ] `tests/AldurPrice.Integration/OcrPipelineRealScreenshotsTests.cs`
- [ ] Тест: каждый скриншот → ожидаемые предметы + цены
- [ ] Оценка: 6 часов

### Критерии готовности M1
- [ ] Парсер poe2db.tw работает, JSON содержит 80+ рун
- [ ] `RuneshapeCombinationTranslator` переводит 100% рунных комбинаций
- [ ] OCR-постпроцессор обрабатывает кириллицу (Ё, апострофы, слияние)
- [ ] Автоопределение языка клиента работает на 3+ тестовых конфигах
- [ ] Конфиг по умолчанию — русский
- [ ] End-to-end: запустил игру → открыл панель → видишь цены на руны
- [ ] Integration-тесты на 10+ реальных скриншотах проходят
- [ ] Тестовое покрытие Core — 85%+
- [ ] Тег `v0.2.0-alpha` с бинарным релизом (portable .zip)

---

## M2 — Полная локализация UI + WPF (v0.3.0-beta)

**Цель:** Полностью русифицированный WPF-интерфейс. Дашборд, настройки, диалоги, сообщения — всё на русском. Английский доступен как fallback.

### Задачи

#### M2.1 — Локализация через .resx
- [ ] Создать `Resources/Strings.resx` (en, нейтральная культура)
- [ ] Создать `Resources/Strings.ru.resx` (ru)
- [ ] `Localization/StringLocalizer.cs` — `IStringLocalizer<App>` реализация
- [ ] `Localization/StringLocalizerExtension.cs` — markup extension для XAML
- [ ] `Localization/LocalizationService.cs` — смена культуры + refresh ViewModels
- [ ] Перевести 280+ UI-строк (см. [04-RU-LOCALIZATION.md](04-RU-LOCALIZATION.md#43-список-ui-строк-для-перевода))
- [ ] Тест: `LocalizationTests` — все ключи из `Strings.resx` есть в `Strings.ru.resx`
- [ ] Оценка: 16 часов

#### M2.2 — Полноценный WPF-дашборд
- [ ] `Dashboard/MainWindow.xaml` — redesign с темой, навигацией, статус-баром
- [ ] `Dashboard/SettingsWindow.xaml` — все настройки в tab'ах (App, Pricing, OCR, Translation, Window)
- [ ] `Dashboard/ChangelogWindow.xaml` — окно чейнджлога с Markdown rendering
- [ ] `Dashboard/ViewModels/` — `MainViewModel`, `SettingsViewModel`, `ChangelogViewModel`
- [ ] MVVM через `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`)
- [ ] Переключатель языка в Settings: Авто / Русский / English
- [ ] Оценка: 20 часов

#### M2.3 — Стилизация WPF
- [ ] `Resources/Themes/Dark.xaml` — тёмная тема (default, как PoE2)
- [ ] `Resources/Themes/Light.xaml` — светлая тема (опционально)
- [ ] Векторные иконки (руны, кнопки) через `<Path Data="..."/>`
- [ ] Шрифт: Inter (Latin) + PT Sans (Cyrillic), bundled
- [ ] Анимации для оверлея (плавное появление/исчезновение цен)
- [ ] PerMonitorV2 DPI через app.manifest
- [ ] Оценка: 12 часов

#### M2.4 — Setup overlay (визуальный)
- [ ] `Overlay/SetupOverlayWindow.xaml` — fullscreen click-through с инструкцией
- [ ] Рисование региона мышью (drag rectangle)
- [ ] Превью захваченного региона с примером распознавания
- [ ] Сохранение региона в `WindowOptions`
- [ ] Оценка: 8 часов

#### M2.5 — Banner (нет цены)
- [ ] `Overlay/BannerWindow.xaml` — баннер "Новые предметы без цены" для скилл-гемов
- [ ] Логика: если предмет не найден в кэше и это GEM — показать в баннере
- [ ] Оценка: 4 часа

#### M2.6 — UI-тесты (FlaUI)
- [ ] `tests/AldurPrice.UI.Tests/` — FlaUI UIA3 WPF UI Automation
- [ ] Тест: открытие Settings, смена языка, проверка обновления UI
- [ ] Тест: переключение источника цен, проверка обновления кэша
- [ ] Тест: setup overlay (рисование региона)
- [ ] Скриншот-тесты: сравнение с эталоном в обоих языках
- [ ] Оценка: 10 часов

### Критерии готовности M2
- [ ] Весь UI на WPF, modern look с темами
- [ ] 280+ UI-строк переведено, тесты локализации проходят
- [ ] Переключатель языка работает без перезапуска
- [ ] Setup overlay работает (рисование региона, превью)
- [ ] UI-тесты (FlaUI) проходят
- [ ] Тег `v0.3.0-beta` с бинарным релизом

---

## M3 — Производительность и стабильность (v0.4.0-beta)

**Цель:** Снижение CPU/RAM, persistence кэша, адаптивный polling. Бенчмарки доказывают improvement.

### Задачи

#### M3.1 — Frame-diffing
- [ ] `Capture/FrameDiffer.cs` — perceptual hash 8×8 (DCT)
- [ ] Интеграция в `OcrPipeline` — пропуск OCR при identical frame
- [ ] Тесты: `FrameDifferTests` (identical, minor noise, real change, panel close/open)
- [ ] Benchmark: `scripts/benchmark-ocr.py` — измерение reduction OCR-вызовов
- [ ] Оценка: 8 часов

#### M3.2 — Адаптивный интервал сканирования
- [ ] `App/AdaptiveScanIntervalController.cs` — 4 состояния (Aggressive/Normal/Throttled/Idle)
- [ ] Интеграция в `OcrLeagueWindowReader`
- [ ] Тесты: `AdaptiveScanIntervalTests`
- [ ] Benchmark: CPU% за 5 минут игрового сценария
- [ ] Оценка: 6 часов

#### M3.3 — Pause-when-unfocused
- [ ] `Capture/Poe2WindowMonitor.cs` — `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`
- [ ] Пауза OCR-цикла когда foreground ≠ PoE2
- [ ] Скрытие оверлея при паузе
- [ ] Тесты: имитация Alt-Tab
- [ ] Оценка: 4 часа

#### M3.4 — SQLite persistence кэша цен
- [ ] `Data/Migrations/001_init.sql` — схема `prices`, `unique_ranges`
- [ ] `Data/Migrations/002_indexes.sql` — индексы
- [ ] `Data/SqlitePricingCache.cs` — `IPricingCache` реализация
- [ ] WAL-mode, UPSERT, batch-запись
- [ ] Stale cache при недоступности API (TTL 60 мин)
- [ ] Замена `InMemoryPricingCache` на `SqlitePricingCache` в DI
- [ ] Тесты: `SqlitePricingCacheTests` (CRUD, TTL, concurrency, restart, stale)
- [ ] Оценка: 12 часов

#### M3.5 — Оптимизация размера сборки
- [ ] Tesseract traineddata: только `eng` + `rus` в bundled
- [ ] Opt-in download остальных 6 языков через Settings ("Скачать языковой пакет")
- [ ] `TrimMode=partial`, `ReadyToRun=true`
- [ ] `EnableCompressionInSingleFile=true`
- [ ] `SatelliteResourceLanguages=en`
- [ ] Benchmark: размер .exe, время старта
- [ ] Оценка: 6 часов

#### M3.6 — Расширенный debug overlay
- [ ] Зелёные рамки вокруг распознанных строк с Y-координатами
- [ ] Жёлтая рамка вокруг детектированной панели
- [ ] Текст: распознанное имя → перевод → цена
- [ ] Отдельное окно «OCR Pipeline Inspector» (`OcrInspectorWindow.xaml`)
- [ ] Оценка: 8 часов

#### M3.7 — Логирование через Serilog
- [ ] Sinks: File (rolling daily), Debug, Memory (кольцевой буфер 1000)
- [ ] Structured logging во всех сервисах
- [ ] Dashboard log panel с фильтром по уровню
- [ ] Оценка: 6 часов

### Критерии готовности M3
- [ ] CPU ≤8% при активной панели — benchmark
- [ ] CPU ~0% в фоне (Alt-Tab)
- [ ] Время старта до цен <200 мс (со 2-го запуска)
- [ ] Размер .exe ~120 МБ
- [ ] RAM ≤180 МБ
- [ ] Все benchmark'и сохранены в `benchmarks/results-0.4.0.json`
- [ ] Тег `v0.4.0-beta` с бинарным релизом

---

## M4 — Релиз-готовность (v1.0.0)

**Цель:** Полировка, установщик, автообновление, финальная документация. Стабильный релиз для широкого использования.

### Задачи

#### M4.1 — Inno Setup установщик
- [ ] `installer.iss` под имя продукта `AldurPrice`
- [ ] Пункт в Пуске, деинсталлятор, ассоциация конфига
- [ ] Тест установки на чистой Windows 10/11
- [ ] Оценка: 4 часа

#### M4.2 — Автообновление
- [ ] `Startup/UpdateChecker.cs` — проверка GitHub Releases
- [ ] Скачивание .zip, распаковка во temp, restart
- [ ] Migration конфига между версиями (если схема изменилась)
- [ ] Тест автообновления на mock-сервере
- [ ] Оценка: 8 часов

#### M4.3 — Bug report service
- [ ] `App/BugReportService.cs` — собирать логи, crash-reports, debug images, system info
- [ ] Кнопка 🐛 в Dashboard
- [ ] Авто-загрузка на GitHub Gist (опционально, с токеном пользователя)
- [ ] Оценка: 6 часов

#### M4.4 — Документация для пользователей
- [ ] Двуязычный README (ru основной + en краткое)
- [ ] Скриншоты в обоих языках
- [ ] FAQ в `docs/FAQ.md`
- [ ] Видео-инструкция (опционально)
- [ ] Перевести tooltips и help text в Settings
- [ ] Оценка: 8 часов

#### M4.5 — Финальное тестирование
- [ ] Smoke-тесты на чистой Windows 10 1809+, Windows 11, Windows 7 (Tesseract fallback)
- [ ] Тест с Lossless Scaling
- [ ] Тест с разными разрешениями (1080p, 1440p, 4K, ultrawide)
- [ ] Тест с разными DPI (100%, 125%, 150%)
- [ ] Тест автообновления v0.4.0 → v1.0.0
- [ ] Stochastic UI stress (1 час без crash)
- [ ] Оценка: 10 часов

### Критерии готовности M4
- [ ] Установщик работает на Windows 10/11
- [ ] Автообновление работает
- [ ] Документация полная и переведена
- [ ] Smoke-тесты на 5+ конфигурациях прошли
- [ ] 0 crashes за 1 час stress-теста
- [ ] Тег `v1.0.0` с финальным релизом (.zip + installer.exe)

---

## Бэклог (после v1.0.0)

Идеи для будущих версий, не входящие в roadmap к v1.0.0:

### N1 — История цен и графики
- Локальное хранение истории цен (SQLite extension)
- Графики в Dashboard (LiveCharts2)
- Экспорт в CSV

### N2 — Hotkeys
- Глобальные hotkeys для управления оверлеем (toggle, hide, refresh prices)
- Настройка в Settings

### N3 — Звуковые уведомления
- Beep при обнаружении предмета дороже порога (например, >5ex)
- Настройка порога и звука

### N4 — Мульти-лига
- Одновременная работа с несколькими лигами
- Переключение между лигами без перезапуска

### N5 — Альтернативные OCR-движки
- PaddleOCR (лучше для китайского/корейского)
- EasyOCR
- Сравнение качества в benchmark'ах

### N6 — Linux/macOS поддержка
- Через Avalonia UI (вместо WPF)
- Tesseract-only (Windows OCR недоступен)
- Захват экрана через X11 / ScreenCaptureKit

### N7 — Telegram-бот для уведомлений
- Push при появлении дорогого предмета в панели
- Webhook для remote-мониторинга

### N8 — Web Dashboard
- Локальный HTTP-сервер (Kestrel) с dashboard
- Доступ с телефона по локальной сети
- Графики, история, настройки

### N9 — Plugin-система
- Динамическая загрузка источников цен
- Кастомные переводчики
- Третий OCR-движок

---

## Риски и митигация

| Риск | Вероятность | Влияние | Митигация |
|---|---|---|---|
| poe2db.tw меняет вёрстку, парсер ломается | Средняя | Блок M1.1 | Тест парсера на сохранённой HTML, ручной fallback |
| WPF click-through overlay сложнее WinForms | Средняя | Блок M1.7 | Win32 interop через `WS_EX_TRANSPARENT`, протестировать early в M1 |
| SQLite native dependency проблемы на Windows 7 | Низкая | Блок M3.4 | Fallback на in-memory cache если SQLite недоступен |
| Windows OCR недоступен на старых системах | Средняя | Блок M1.3 | Tesseract fallback, явное уведомление пользователю |
| Мало тестировщиков с RU-клиентом | Высокая | Качество OCR | Сбор скриншотов через issue template, beta-тестирование |
| poe2scout API меняется без warning | Средняя | Блок M1.5 | WireMock тесты на сохранённых fixture'ах, retry logic |
| .NET 9 → .NET 10 migration после релиза | Низкая | Maintenance | Stay на .NET 9 LTS, миграция в v2.0 |

---

## Как отслеживать прогресс

- **GitHub Projects board** — колонки: Backlog / In Progress / Review / Done
- **Milestones в GitHub Issues** — каждый milestone как GitHub milestone
- **Labels** — `M0`, `M1`, `M2`, `M3`, `M4`, `bug`, `enhancement`, `localization`, `performance`, `ocr`, `ui`
- **Weekly status** — каждый понедельник update в `docs/STATUS.md` (что сделано, что в прогрессе, блокеры)

Коммиты и PR должны ссылаться на issue: `Fixes #123`, `Refs #456`. Branch naming: `M1/runeshape-translator`, `M2/wpf-dashboard`, и т.д.
