# STATUS

> Текущее состояние AldurPrice. Обновлять на каждой итерации.
> История изменений — в [CHANGELOG.md](CHANGELOG.md). Архитектура — в [docs/02-ARCHITECTURE.md](docs/02-ARCHITECTURE.md).

## Текущий milestone

**M0 — Каркас проекта** (v0.1.0-alpha) — **✅ ГОТОВ** (кроме релиз-тега)

Критерии готовности M0 (из [docs/05-ROADMAP.md](docs/05-ROADMAP.md#критерии-готовности-m0)):

| Критерий | Статус | Замечание |
|---|---|---|
| 6 проектов компилируются | ✅ | Проверено на Linux + `EnableWindowsTargeting=true`: 0 warnings, 0 errors |
| `dotnet build` без warnings | ✅ | Подавлены `CA1822`/`CS1591`/`CA1848`/`CA1805` — см. Known Issues |
| `dotnet test` — smoke-тест проходит | ✅ | 13 тестов (RussianStemmerTests + LevenshteinTests), 0 fail |
| CI green на GitHub Actions | ⏳ | CI настроен (`ci.yml`), но не запускался — нужен push |
| `MainWindow` открывается с «Hello AldurPrice» | ⏳ | Код готов, нужна проверка на Windows |
| Тег `v0.1.0-alpha` с GitHub Release | ⏳ | Делается вручную после проверки на Windows |

## Что в работе

- **Проверка на Windows**: запустить `dotnet run --project src/AldurPrice` на Windows 10/11 — должна открыться тёмная窗口 с надписью «Hello AldurPrice» и версией `v0.1.0-alpha · M0 Skeleton`.
- **Первый push**: после слияния архива сделать `git push origin main`, дождаться зелёного CI, затем `git tag v0.1.0-alpha && git push origin v0.1.0-alpha` для релиза.

## Что дальше (M1 — MVP)

Следующий milestone — [M1: MVP, базовая русификация и OCR](docs/05-ROADMAP.md#m1--mvp-базовая-русификация-и-ocr-v020-alpha).

Рекомендуемый порядок задач (по зависимости):

1. **M1.1 — Парсер poe2db.tw**: запустить `scripts/parse-poe2db-runeshapes.py`, закоммитить `ocr/runeshape-combinations-ru.json`. Без него `RuneshapeCombinationTranslator` (M1.2) не сможет работать.
2. **M1.2 — Core: ItemNameParser, ItemNameTranslator, RussianStemmer (полный Snowball), Levenshtein-цепочка**: заменить стабы на реальные имплементации. Тесты `RuneshapeCombinationTranslatorTests`, `RussianStemmerTests` (расширенный), `RussianOcrDistortionTests`.
3. **M1.3 — OCR-движки**: `WindowsOcrEngine` через `Windows.Media.Ocr`, `TesseractEngine` через Tesseract 5.2 + native DLL bootstrapper. `OcrPipeline`, `ImagePreprocessor`, `LeaguePanelDetector`.
4. **M1.4 — Захват экрана**: `PrintWindowCapture` через P/Invoke `user32!PrintWindow`, `Poe2WindowLocator`.
5. **M1.5 — Источники цен**: `Poe2ScoutClient`, `PoeNinjaClient` через `IHttpClientFactory`, WireMock.Net-тесты.
6. **M1.6 — In-memory кэш цен**: `ConcurrentDictionary`-based, TTL 15 мин, `LeaguePricingWorker` (`BackgroundService`).
7. **M1.7 — Оверлей (минимальный)**: click-through topmost WPF window через `WS_EX_TRANSPARENT`, `PriceRowLayout`, `PriceColorCalculator`.
8. **M1.8 — Dashboard (минимальный)**: `MainWindow` с кнопкой «Запуск» и лог-панелью.
9. **M1.9 — Конфигурация**: `SettingsController` (чтение/запись `appsettings.json`), `Poe2ConfigFile` (автоопределение языка клиента), валидаторы.
10. **M1.10 — Тесты на реальных скриншотах**: собрать 10+ PNG-фикстур панели рунешейпов с RU-клиента.

## Known Issues

### KI-001: Подавлены анализаторы CA1822, CS1591, CA1848, CA1805

**Симптом:** `Directory.Build.props` содержит `<NoWarn>$(NoWarn);CA1822;CS1591;CA1848;CA1805</NoWarn>`.

**Причина:** В M0 многие методы — заглушки, не использующие instance state (`CA1822`). XML-документация есть на классах, но не на всех членах (`CS1591`). `LoggerMessage` source generator — это M3.7 работа (`CA1848`). Явная инициализация `bool = false` в Options-классах намеренна для ясности (`CA1805`).

**План:** В M1, когда стабы заменятся на реальные имплементации (с инжекцией зависимостей через конструктор), убрать `CA1822` из NoWarn. После добавления XML-документации на все public-члены — убрать `CS1591`. После M3.7 (Serilog structured logging + LoggerMessage) — убрать `CA1848`. `CA1805` можно оставить или убрать, когда Options-классы будут генерироваться через source-gen.

### KI-002: .slnx формат не верифицирован на Windows

**Симптом:** `AldurPrice.slnx` использует строковые `Id` (например, `Id="AldurPrice.Core"`).

**Причина:** .NET 9 SDK принял этот формат (`dotnet build AldurPrice.slnx` на Linux прошёл). Но Visual Studio 2022 может требовать GUID-формат `Id="{}"` для некоторых операций (например, конвертация в .sln). Если будут проблемы в VS — пересоздать через `dotnet sln -s slnx AldurPrice.slnx` на Windows.

**План:** Проверить открытие в Visual Studio 2022 17.10+. Если работает — закрыть issue. Если нет — регенерировать через `dotnet sln`.

### KI-003: WPF-проекты не собираются на Linux без `EnableWindowsTargeting`

**Симптом:** `dotnet build src/AldurPrice` на Linux падает с `NETSDK1100: To build a project targeting Windows on this operating system, set the EnableWindowsTargeting property to true`.

**Причина:** Это ожидаемое поведение — WPF требует Windows. `AldurPrice.Ocr`, `AldurPrice.Capture`, `AldurPrice` имеют `TargetFramework=net9.0-windows`. `AldurPrice.Core` и `AldurPrice.Data` — `net9.0` (cross-platform).

**План:** Не баг, дизайн-решение. Для разработки Core-логики на Linux/macOS: `dotnet test tests/AldurPrice.Core.Tests`. Для полной сборки нужен Windows.

### KI-004: `appsettings.json` копируется из `config/` в output

**Симптом:** В `AldurPrice.csproj` есть `<None Update="..\..\config\appsettings.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`.

**Причина:** Конфиг живёт в `config/appsettings.json` (для редактирования пользователем), но приложению нужно его в output-директории при запуске.

**План:** В M1.9 (`SettingsController`) — пересмотреть стратегию: либо оставляем copy-on-build, либо `SettingsController` сам ищет `config/appsettings.json` относительно `AppContext.BaseDirectory`. Решение — после M1.9.

### KI-005: `ItemNameTranslator` инициализирует `RussianStemmer`, но не использует

**Симптом:** В `ItemNameTranslator.cs` есть `private readonly RussianStemmer _stemmer = new();`, но поле не используется (stub возвращает `null`).

**Причина:** Stub для M0. В M1.2 `_stemmer` будет использоваться в chain fallback'ов (stem-matching для падежей).

**План:** Убрать после реализации M1.2.

### KI-006: `appsettings.json` не копируется в `AldurPrice/bin` через `dotnet build` (только через `<None Update>`)

**Симптом:** При `dotnet build` файл должен копироваться в output. Если нет — приложение упадёт при старте с «file not found».

**Причина:** `<None Update>` работает только для файлов внутри проекта. `..\..\config\appsettings.json` — outside project dir, может требовать `<None Include>` вместо `<None Update>`.

**План:** Проверить на Windows при первом запуске. Если файл не копируется — заменить `Update` на `Include` или использовать `appsettings.json` рядом с `.csproj` (символическая ссылка или копия).

## Architecture deviations

В этом разделе фиксируются отличия реализации от архитектурного документа `docs/02-ARCHITECTURE.md`. Любое отличие должно быть либо согласовано с архитектурой (тогда документ обновляется), либо перечислено здесь с обоснованием.

### AD-001: Интерфейсы `IPricingCache`, `IPricingSource`, `IOcrEngine`, `ICaptureStrategy`, `IItemNameTranslator`, `ISystemClock` живут в `AldurPrice.Core/Contracts/`, а не в `AldurPrice/Contracts/`

**Документ говорит:** `docs/02-ARCHITECTURE.md` рисует `Contracts/` в основном проекте `AldurPrice/`.

**Реализация:** Все shared-интерфейсы для DI перенесены в `AldurPrice.Core/Contracts/`. В `AldurPrice/Contracts/` (когда появится в M1.7) будут жить только UI-only интерфейсы (`ILeagueWindowReader`, `IOverlayRenderer`).

**Обоснование:** Иначе циклическая зависимость: `AldurPrice.Data` реализует `IPricingCache`, но если `IPricingCache` в `AldurPrice`, то `Data → AldurPrice`, а `AldurPrice → Data` для регистрации в DI — цикл. Перенос интерфейсов в Core решает это: `Data → Core`, `AldurPrice → Core + Data`.

**План:** Обновить `docs/02-ARCHITECTURE.md` в следующей итерации (после проверки архитектуры на M1).

## Environment

- **.NET SDK:** 9.0.315 (проверено на Linux x64)
- **OS для dev:** Linux работает для `Core`/`Data`/`Core.Tests`; Windows нужен для `Ocr`/`Capture`/`AldurPrice` (WPF)
- **Целевая платформа:** .NET 9, WPF, net9.0-windows
- **Лицензия:** MIT
