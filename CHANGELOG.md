# Changelog

Все заметные изменения проекта AldurPrice документируются здесь.
Формат — [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версионирование — [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]

Планируется: M1 — MVP (базовая русификация и OCR). См. [docs/05-ROADMAP.md](docs/05-ROADMAP.md#m1--mvp-базовая-русификация-и-ocr-v020-alpha).

## [0.1.0-alpha] — M0 Skeleton

### Added
- **Каркас решения `AldurPrice.slnx`** с 6 проектами:
  - `src/AldurPrice.Core/` — чистая логика (net9.0, cross-platform): `RussianStemmer`, `Levenshtein`, `ItemNameParser`, `TierFallback`, `FallbackProvider`, `IdAliases`, `ItemNameTranslator` (stub), `TranslationCache` (stub), `RuneshapeCombinationTranslator` (stub). Records: `PriceQuote`, `PricingSnapshot`, `ParsedDetectedItem`, `LeagueWindowSnapshot`. Интерфейсы: `IPricingCache`, `IPricingSource`, `IItemNameTranslator`, `ISystemClock`.
  - `src/AldurPrice.Data/` — persistence-слой (net9.0): `SqlitePricingCache` (stub, реализация в M3.4).
  - `src/AldurPrice.Ocr/` — OCR-движки (net9.0-windows): `WindowsOcrEngine` (stub, M1.3), `TesseractEngine` (stub, M1.3), `OcrEngineResolver`, `IOcrEngine`/`OcrResult`/`OcrLine`.
  - `src/AldurPrice.Capture/` — захват экрана (net9.0-windows): `PrintWindowCapture` (stub, M1.4), `ICaptureStrategy`/`CaptureRegion`.
  - `src/AldurPrice/` — основной WPF-проект (net9.0-windows): `App.xaml(.cs)` с `IHost`, single-instance guard (`Mutex`), crash handlers (`AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, `DispatcherUnhandledException`), `MainWindow.xaml` «Hello AldurPrice», `Configuration/` (`AppOptions`, `OcrOptions`, `PricingOptions`, `TranslationOptions`, `WindowOptions`), `app.manifest` (PerMonitorV2 DPI, supportedOS Windows 10/11).
  - `tests/AldurPrice.Core.Tests/` — xUnit-проект (net9.0): `RussianStemmerTests` (5 кейсов), `LevenshteinTests` (4 кейса). 13 тестов, 0 fail.
- **`Directory.Build.props`** — общие настройки сборки (nullable, latest C#, analyzers), TargetFramework задаётся индивидуально в каждом .csproj.
- **`Directory.Packages.props`** — central package management, добавлены `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`.
- **`STATUS.md`** — tracking-документ: текущее состояние, Known Issues, что в работе, что дальше.
- **Структура директорий**: `ocr/translations/`, `ocr/tesseract/`, `benchmarks/` с `.gitkeep`.

### Changed
- `Directory.Build.props` — `TargetFramework` убран из глобального; каждый проект указывает свой TFM (`net9.0` для Core/Data/Tests, `net9.0-windows` для Ocr/Capture/AldurPrice).
- `Directory.Packages.props` — добавлены `Microsoft.Extensions.Hosting.Abstractions` 9.0.0, `Microsoft.Extensions.Configuration` 9.0.0, `Microsoft.Extensions.Options` 9.0.0, `Microsoft.Extensions.Options.DataAnnotations` 9.0.0, `Microsoft.Extensions.Logging.Abstractions` 9.0.0.

### Known Issues
Подробности — в [STATUS.md](STATUS.md#known-issues):
- `CA1822`/`CS1591`/`CA1848`/`CA1805` подавлены до M1 (stubs не используют instance state, нет LoggerMessage source generator).
- `.slnx` формат не верифицирован на Visual Studio 2022 — если будут проблемы, регенерировать через `dotnet sln`.
- WPF-проекты не собираются на Linux без `EnableWindowsTargeting=true` (дизайн-решение, не баг).

## Источники данных

- [Exiled Exchange 2](https://github.com/Kvan7/Exiled-Exchange-2) — переводы базовых предметов (4319 шт.)
- [poe2db.tw](https://poe2db.tw/ru/Runeshape_Combinations) — переводы рунных комбинаций
- [poe2scout.com](https://poe2scout.com) — источник цен с 24-часовым усреднением
- [poe.ninja](https://poe.ninja) — альтернативный источник цен
- [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) — OCR-движок (fallback)
