# STATUS

> Текущее состояние AldurPrice. Обновлять на каждой итерации.
> История изменений — в [CHANGELOG.md](CHANGELOG.md). Архитектура — в [docs/02-ARCHITECTURE.md](docs/02-ARCHITECTURE.md).

## Текущий milestone

**M1 — MVP** — частично реализован.

| Подзадача | Статус | Замечание |
|---|---|---|
| M1.1 — Парсер poe2db.tw | ✅ | Переписан под реальную HTML-структуру poe2db. `ocr/runeshape-combinations-ru.json`: 153 записи (alloy 13, ancient 13, basic 82, lineage 21, master 1, special 3, ward 20). |
| M1.2 — Core: ItemNameParser, ItemNameTranslator, RussianStemmer, RuneshapeCombinationTranslator | ✅ (частично) | Реальные имплементации + 106 тестов (0 fail). Без TranslationCache/rus.ndjson — это M1.5. Полный Snowball — KI-007. |
| M1.3 — OCR-движки | ⏳ | WindowsOcrEngine, TesseractEngine, OcrPipeline, ImagePreprocessor, LeaguePanelDetector, RussianOcrPostProcessor. |
| M1.4 — Захват экрана | ⏳ | PrintWindowCapture, Poe2WindowLocator. |
| M1.5 — Источники цен + TranslationCache/rus.ndjson | ⏳ | Poe2ScoutClient, PoeNinjaClient, загрузка rus.ndjson из Exiled Exchange 2 (4319 предметов). Без этого ItemNameTranslator покрывает только рунные комбинации. |
| M1.6 — In-memory кэш цен | ⏳ | ConcurrentDictionary, TTL 15 мин, LeaguePricingWorker. |
| M1.7 — Оверлей (минимальный) | ⏳ | click-through topmost WPF window. |
| M1.8 — Dashboard (минимальный) | ⏳ | MainWindow с кнопкой «Запуск» и лог-панелью. |
| M1.9 — Конфигурация | ⏳ | SettingsController, Poe2ConfigFile. |
| M1.10 — Тесты на реальных скриншотах | ⏳ | 10+ PNG-фикстур с RU-клиента. |

**Сборка:** `dotnet build AldurPrice.slnx -p:EnableWindowsTargeting=true` — 0 warnings, 0 errors.
**Тесты:** `dotnet test tests/AldurPrice.Core.Tests` — 106 passed, 0 failed.

## Что в работе

- **M0 релиз**: запустить `dotnet run --project src/AldurPrice` на Windows 10/11, проверить тёмное окно с «Hello AldurPrice», поставить тег `v0.1.0-alpha`.
- **M1.3 (next)**: OCR-движки. Зависимости: `WindowsOcrEngine` (Windows.Media.Ocr, UWP API из net9.0-windows), `TesseractEngine` (Tesseract 5.2 + native DLL bootstrapper), `OcrPipeline` (windows-first → tesseract fallback), `ImagePreprocessor` (greyscale, contrast), `LeaguePanelDetector` (HSV-segmentation для панели рунешейпов), `RussianOcrPostProcessor` (исправление типовых OCR-искажений кириллицы).

## Что дальше (M1 — MVP)

Рекомендуемый порядок задач (по зависимости):

1. ~~M1.1 — Парсер poe2db.tw~~ ✅
2. ~~M1.2 — Core: ItemNameParser, ItemNameTranslator, RussianStemmer, RuneshapeCombinationTranslator~~ ✅ (частично; полный Snowball — KI-007, TranslationCache + rus.ndjson — M1.5)
3. **M1.3 — OCR-движки**: `WindowsOcrEngine` через `Windows.Media.Ocr`, `TesseractEngine` через Tesseract 5.2 + native DLL bootstrapper. `OcrPipeline`, `ImagePreprocessor`, `LeaguePanelDetector`, `RussianOcrPostProcessor`.
4. M1.4 — Захват экрана: `PrintWindowCapture` через P/Invoke `user32!PrintWindow`, `Poe2WindowLocator`.
5. M1.5 — Источники цен + переводы базовых предметов: `Poe2ScoutClient`, `PoeNinjaClient` через `IHttpClientFactory`, WireMock.Net-тесты. Загрузка `rus.ndjson` из Exiled Exchange 2 (4319 предметов), `TranslationCache` (.dat или JSON).
6. M1.6 — In-memory кэш цен: `ConcurrentDictionary`-based, TTL 15 мин, `LeaguePricingWorker` (`BackgroundService`).
7. M1.7 — Оверлей (минимальный): click-through topmost WPF window через `WS_EX_TRANSPARENT`, `PriceRowLayout`, `PriceColorCalculator`.
8. M1.8 — Dashboard (минимальный): `MainWindow` с кнопкой «Запуск» и лог-панелью.
9. M1.9 — Конфигурация: `SettingsController` (чтение/запись `appsettings.json`), `Poe2ConfigFile` (автоопредел языка клиента), валидаторы.
10. M1.10 — Тесты на реальных скриншотах: собрать 10+ PNG-фикстур панели рунешейпов с RU-клиента.

## Known Issues

### KI-001: Подавлены анализаторы CA1822, CS1591, CA1848, CA1805

**Симптом:** `Directory.Build.props` содержит `<NoWarn>$(NoWarn);CA1822;CS1591;CA1848;CA1805</NoWarn>`.

**Причина:** `CS1591` (XML-документация на все public-члены) ещё не везде добавлена. `CA1848` (LoggerMessage source generator) — это M3.7 работа. `CA1805` (явная инициализация `bool = false`) намеренна в Options-классах. `CA1822` больше НЕ релевантна после M1.2 (стабы заменены на реальные имплементации, использующие instance state).

**План:** Убрать `CA1822` из NoWarn в следующей итерации (M1.3). `CS1591` — после прохождения по всем public API. `CA1848` — после M3.7. `CA1805` — оставить или убрать при переходе на source-gen Options.

### KI-002: .slnx формат не верифицирован на Windows

**Симптом:** `AldurPrice.slnx` использует строковые `Id` (например, `Id="AldurPrice.Core"`).

**Причина:** .NET 9 SDK принял этот формат (`dotnet build AldurPrice.slnx` на Linux прошёл). Visual Studio 2022 может требовать GUID-формат `Id="{}"` для некоторых операций.

**План:** Проверить открытие в Visual Studio 2022 17.10+. Если работает — закрыть issue. Если нет — регенерировать через `dotnet sln`.

### KI-003: WPF-проекты не собираются на Linux без `EnableWindowsTargeting`

**Симптом:** `dotnet build src/AldurPrice` на Linux падает с `NETSDK1100`.

**Причина:** Дизайн-решение. WPF требует Windows. `AldurPrice.Core` и `AldurPrice.Data` — `net9.0` (cross-platform), `AldurPrice.Ocr`, `AldurPrice.Capture`, `AldurPrice` — `net9.0-windows`.

**План:** Не баг. Для разработки Core-логики на Linux: `dotnet test tests/AldurPrice.Core.Tests`. Для полной сборки: `dotnet build AldurPrice.slnx -p:EnableWindowsTargeting=true`.

### KI-004: `appsettings.json` copy-on-build стратегия

**Симптом:** В `AldurPrice.csproj` есть `<None Update="..\..\config\appsettings.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`.

**План:** Пересмотреть в M1.9 (`SettingsController`).

### KI-005: ~~`ItemNameTranslator` инициализирует `RussianStemmer`, но не использует~~ ✅ РЕШЕНО в M1.2

Поле `_stemmer` теперь используется в `ItemNameTranslator` (через `RuneshapeCombinationTranslator`, который использует stemmer для stem-matching'а). Можно убрать suppress CA1806 если было.

### KI-006: `appsettings.json` copy strategy verification

**Симптом:** `<None Update>` для файла outside project dir может не сработать на Windows.

**План:** Проверить на Windows при первом запуске. Если файл не копируется — заменить `Update` на `Include`.

### KI-007: RussianStemmer — НЕ полный Snowball Russian

**Симптом:** `RussianStemmer` снимает окончания по списку, отсортированному по убыванию длины, без вычисления RV/R1/R2 регионов как в полном Snowball Russian.

**Причина:** Полный Snowball пере-стемит базовые слова предметных имён:
- Snowball: «руна» → «ру» (отрезает глагольное окончание «на»)
- AldurPrice: «руна» → «рун» (только падежные окончания)
- Snowball: «новости» → «новост» → «нов» (chain stemming)
- AldurPrice: «новости» → «новост» (одно окончание за вызов)

Для matching'а предметных имён важна **стабильность** stem'а (разные падежи → один stem), а не **агрессивность** (минимальная длина stem'а).

**План:** Рассмотреть порт полного Snowball с RV-регионами, но с дополнительным правилом: не снимать глагольные окончания для слов, где R1 пуст (т.е. нет гласной после первой гласной). Это компромисс между стабильностью и точностью. Отдельная итерация, низкий приоритет — текущий stemmer покрывает все кейсы M1.2 тестов.

### KI-008: poe2db.tw «Bait Rune» без русского перевода

**Симптом:** В `ocr/runeshape-combinations-ru.json` одна запись с `ru == en == "Bait Rune"`. poe2db.tw не имеет русского перевода для этого предмета.

**План:** Не баг парсера — данные на стороне poe2db. При обновлении JSON (через `scripts/parse-poe2db-runeshapes.py`) проверить, не появился ли перевод. Если не появился — оставить как есть (translator вернёт `null` для этого предмета при RU-входе).

## Architecture deviations

В этом разделе фиксируются отличия реализации от архитектурного документа `docs/02-ARCHITECTURE.md`. Любое отличие должно быть либо согласовано с архитектурой (тогда документ обновляется), либо перечислено здесь с обоснованием.

### AD-001: Shared-интерфейсы в `AldurPrice.Core/Contracts/`, а не в `AldurPrice/Contracts/`

**Документ говорит:** `docs/02-ARCHITECTURE.md` рисует `Contracts/` в основном проекте `AldurPrice/`.

**Реализация:** Все shared-интерфейсы для DI перенесены в `AldurPrice.Core/Contracts/`. В `AldurPrice/Contracts/` (когда появится в M1.7) будут жить только UI-only интерфейсы (`ILeagueWindowReader`, `IOverlayRenderer`).

**Обоснование:** Иначе циклическая зависимость: `AldurPrice.Data` реализует `IPricingCache`, но если `IPricingCache` в `AldurPrice`, то `Data → AldurPrice`, а `AldurPrice → Data` для регистрации в DI — цикл. Перенос интерфейсов в Core решает это: `Data → Core`, `AldurPrice → Core + Data`.

**План:** Обновить `docs/02-ARCHITECTURE.md` в следующей итерации (после проверки архитектуры на M1).

### AD-002: `RuneshapeCombinationTranslator` грузит JSON как embedded resource, а не из filesystem

**Документ говорит:** `docs/04-RU-LOCALIZATION.md` описывает загрузку JSON из `ocr/runeshape-combinations-ru.json` (filesystem path).

**Реализация:** JSON включён в `AldurPrice.Core.csproj` как `<EmbeddedResource>` с `LogicalName=AldurPrice.Core.Translation.runeshape-combinations-ru.json`. `RuneshapeCombinationTranslator` грузит его через `Assembly.GetManifestResourceStream`.

**Обоснование:** Embedded resource — самодостаточный, не требует файловых путей. Tests используют default-конструктор. Для обновления JSON — re-run парсера + rebuild (один commit). Для runtime override (если понадобится) — добавить stream-конструктор (уже есть для тестов).

**План:** Обновить `docs/04-RU-LOCALIZATION.md` в следующей итерации.

## Environment

- **.NET SDK:** 9.0.300 (проверено на Linux x64)
- **OS для dev:** Linux работает для `Core`/`Data`/`Core.Tests`; Windows нужен для `Ocr`/`Capture`/`AldurPrice` (WPF) и runtime-проверки
- **Целевая платформа:** .NET 9, WPF, net9.0-windows
- **Python:** 3.12 + `requests` + `lxml` (для `scripts/parse-poe2db-runeshapes.py`)
- **Лицензия:** MIT
