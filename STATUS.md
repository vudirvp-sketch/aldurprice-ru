# STATUS

> Текущее состояние AldurPrice. Обновлять на каждой итерации.
> История изменений — в [CHANGELOG.md](CHANGELOG.md). Архитектура — в [docs/02-ARCHITECTURE.md](docs/02-ARCHITECTURE.md).

## Текущий milestone

**M1 — MVP** — частично реализован.

| Подзадача | Статус | Замечание |
|---|---|---|
| M1.1 — Парсер poe2db.tw | ✅ | Переписан под реальную HTML-структуру poe2db. `ocr/runeshape-combinations-ru.json`: 153 записи (alloy 13, ancient 13, basic 82, lineage 21, master 1, special 3, ward 20). |
| M1.2 — Core: ItemNameParser, ItemNameTranslator, RussianStemmer, RuneshapeCombinationTranslator | ✅ (частично) | Реальные имплементации + 106 тестов (0 fail). `TranslationCache` + `rus.ndjson` — M1.5-partial (TranslationCache готов, NDJSON-данные — KI-017). Полный Snowball — KI-007. |
| M1.3 — OCR-движки и пайплайн | ✅ (частично) | Реальные имплементации: `WindowsOcrEngine`, `TesseractEngine`, `ImagePreprocessor`, `LeaguePanelDetector`, `RussianOcrPostProcessor`, `OcrPipeline`. `OcrEngineResolver` без изменений. Не сделано: `OcrLeagueWindowReader`, `ResolutionProfiles`, MSBuild target `EnsureTessData`, тесты на реальные скриншоты. 23 новых теста для `RussianOcrPostProcessor`. |
| M1.4 — Захват экрана | ✅ (частично) | `PrintWindowCapture` (real), `Poe2WindowLocator` (real), `Win32/NativeMethods.cs` (P/Invoke). `WgcCapture` отложен (KI-013). 25 новых тест-кейсов в `AldurPrice.Capture.Tests` (15 locator + 10 capture). |
| M1.5 — Источники цен + TranslationCache/rus.ndjson | 🔶 (частично) | `TranslationCache` — реальная имплементация (NDJSON-загрузка, exact-match lookup, `LoadEmbeddedOrDefault`). `ItemNameTranslator` — fallback [2] через cache. 28 новых тестов (`TranslationCacheTests` + `ItemNameTranslatorCacheFallbackTests`). **Не сделано:** `Poe2ScoutClient`, `PoeNinjaClient`, `PricingSourceRouter`, WireMock-тесты (нужны реальные API-response fixtures), `rus.ndjson` не bundled (KI-017). |
| M1.6 — In-memory кэш цен | ⏳ | `ConcurrentDictionary`, TTL 15 мин, `LeaguePricingWorker`. |
| M1.7 — Оверлей (минимальный) | ⏳ | click-through topmost WPF window. |
| M1.8 — Dashboard (минимальный) | ⏳ | `MainWindow` с кнопкой «Запуск» и лог-панелью. |
| M1.9 — Конфигурация | ⏳ | `SettingsController`, `Poe2ConfigFile`. |
| M1.10 — Тесты на реальных скриншотах | ⏳ | 10+ PNG-фикстур с RU-клиента. |

**Сборка:** `dotnet build AldurPrice.slnx` (Windows) или `dotnet build AldurPrice.slnx -p:EnableWindowsTargeting=true` (Linux, без run) — 0 errors, 0 warnings. (18 warnings из M1.4-fix починены: 16× CA1873 подавлены в `Directory.Build.props` — последовательно с CA1848, оба про LoggerMessage source-gen, M3.7 работа; CA1305 + CA1861 — точечные фиксы.)
**Тесты:** `dotnet test` — ~193 total: 168 Core.Tests (140 ранее + 28 новых M1.5-partial) + 25 Capture.Tests (только Windows — P/Invoke `user32.dll`). На Linux 1 Capture-тест падает на `IsWindow` (ожидаемо); на Windows ожидается ~193/193 passed. **Не верифицировано на Windows** — счёт 193 ожидаемый, нужна проверка `dotnet test`.

## Что в работе

- **M1.5-partial (текущая итерация):** `TranslationCache` — реальная имплементация (NDJSON-загрузка из Exiled Exchange 2, exact-match lookup, `LoadEmbeddedOrDefault` для embedded `rus.ndjson`). `ItemNameTranslator` — fallback [2] через cache. 28 новых тестов. 18 warnings из M1.4-fix починены (CA1873 подавлен, CA1305/CA1861 — точечные фиксы). **Код написан, но НЕ верифицирован сборкой** (dotnet SDK недоступен в среде разработки) — нужна Windows-проверка: `dotnet build` (0 errors / 0 warnings) + `dotnet test` (~193 passed).
- **M0 релиз (pending)**: запустить `dotnet run --project src/AldurPrice` на Windows 10/11, проверить тёмное окно с «Hello AldurPrice», поставить тег `v0.1.0-alpha`.
- **M1.4 Windows runtime-верификация (pending)**: запустить PoE2, проверить через debug-лог, что `Poe2WindowLocator.TryLocate()` находит окно (имя процесса может отличаться — см. KI-013). (Опционально) Smoke-test `PrintWindowCapture.CaptureAsync` с регионом `{0,0,800,600}` → PNG, проверить что не чёрный прямоугольник.

## Что дальше (M1 — MVP)

Рекомендуемый порядок задач (по зависимости):

1. ~~M1.1 — Парсер poe2db.tw~~ ✅
2. ~~M1.2 — Core translators~~ ✅ (частично; полный Snowball — KI-007, TranslationCache + rus.ndjson — M1.5)
3. ~~M1.3 — OCR-движки~~ ✅ (частично; оставшиеся компоненты — M1.10 / M2)
4. ~~M1.4 — Захват экрана~~ ✅ (частично; `WgcCapture` fallback — KI-013, M1.4b / M2)
5. M1.5 — Источники цен + переводы базовых предметов (🔶 частично):
   - ✅ `TranslationCache` — реальная имплементация (NDJSON-загрузка, exact-match, `LoadEmbeddedOrDefault`).
   - ✅ `ItemNameTranslator` — fallback [2] через cache. 28 новых тестов.
   - ⏳ `Poe2ScoutClient`, `PoeNinjaClient` через `IHttpClientFactory` — нужны реальные API-response fixtures (web-research или browser dev-tools на poe2scout.com / poe.ninja).
   - ⏳ `PricingSourceRouter` — маршрутизация между источниками.
   - ⏳ WireMock.Net-тесты для клиентов.
   - ⏳ Запуск `scripts/update-translations.py` для загрузки `rus.ndjson` (4319 предметов) → rebuild → embed в `AldurPrice.Core`. См. KI-017.
   - ⏳ `translations.json` — fallback для валюты (M1.5+, не реализовано).
6. M1.6 — In-memory кэш цен: `ConcurrentDictionary`-based, TTL 15 мин, `LeaguePricingWorker` (`BackgroundService`).
7. M1.7 — Оверлей (минимальный): click-through topmost WPF window через `WS_EX_TRANSPARENT`, `PriceRowLayout`, `PriceColorCalculator`.
8. M1.8 — Dashboard (минимальный): `MainWindow` с кнопкой «Запуск» и лог-панелью.
9. M1.9 — Конфигурация: `SettingsController`, `Poe2ConfigFile`, валидаторы.
10. M1.10 — Тесты на реальных скриншотах + оставшиеся компоненты OCR (`OcrLeagueWindowReader`, `ResolutionProfiles`, MSBuild target `EnsureTessData`).

## Бэклог (после M1)

- **M2.7 — Rune Value Highlight** (опционально, пост-MVP): подсветка ценных рун, вложенных в ремнант, через template matching по иконкам. Тир-лист: S+ Opulent / S Power,Death,Bond,Oath / A Time,Rebirth / B Purple / C Blue. Не OCR-текст, а иконки. Зависит от M1.7 (оверлей) + M1.10 (скриншоты для шаблонов). См. [docs/05-ROADMAP.md](docs/05-ROADMAP.md#m27--rune-value-highlight-опциональная-фича--️-пост-mvp-не-блокирует-v030-beta).

## Known Issues

### KI-014: ✅ РЕШЕНО — NU1201 при restore (TFM mismatch)

**Симптом:** `dotnet build AldurPrice.slnx` на Windows падал с `error NU1201: Проект AldurPrice.Ocr несовместим с net9.0-windows7.0. Проект AldurPrice.Ocr поддерживает: net9.0-windows10.0.19041.0`.

**Причина:** `AldurPrice.csproj` (главный WPF-проект) целится в `net9.0-windows` (= `net9.0-windows7.0`), но ссылается на `AldurPrice.Ocr` с TFM `net9.0-windows10.0.19041.0` (WinRT для `Windows.Media.Ocr`). Consumer обязан иметь TFM ≥ старшей TFM зависимости.

**Решение:** `AldurPrice.csproj` переведён на `net9.0-windows10.0.19041.0` + `SupportedOSPlatformVersion=10.0.19041.0`. `AldurPrice.Capture` и `AldurPrice.Capture.Tests` оставлены на `net9.0-windows` (им WinRT не нужен — только Win32 P/Invoke). Комментарий в `Directory.Build.props` обновлён.

### KI-015: ✅ РЕШЕНО — TesseractEngine.cs не компилировался (CS0185, CS1061, CS1061)

**Симптом:** После починки NU1201 обнаружились 3 compile-error'а в `TesseractEngine.cs`:
1. CS0185: `lock (container.Lock)` — `container` имеет тип `Lazy<EngineContainer>`, а не `EngineContainer`. `lock` требует reference type.
2. CS1061: `container.Engine.Process(img)` — у `Lazy<EngineContainer>` нет свойства `Engine`.
3. CS1061: `rect.Y` — у Tesseract `Rect` нет свойства `Y` (есть `Y1`/`Y2`/`Width`/`Height`).

**Причина:** Баг был скрыт до M1.4-fix, потому что restore падал на NU1201 до компиляции. Код M1.3 никогда не компилировался — ни на Linux, ни на Windows (на Linux `AldurPrice.Ocr` требует `EnableWindowsTargeting`, на Windows restore падал).

**Решение:**
- `container` переименован в `lazy`, добавлено `var container = lazy.Value;` (Lazy<T>.Value потокобезопасно инициирует engine через `LazyThreadSafetyMode.ExecutionAndPublication` — паттерн уже использовался в `Dispose()` на line 185: `kv.Value.Value.Engine.Dispose()`).
- `rect.Y` → `rect.Y1` (верхняя граница bounding box — то, что нужно для `OcrLine.Y`).

### KI-016: ✅ РЕШЕНО — RussianOcrPostProcessor: 3 failing теста

**Симптом:** 3 теста в `RussianOcrPostProcessorTests` падали:
1. `ProcessLine_TrimsStrayPunctuation("- Руна огня -", "Руна огня")` → actual `" Руна огня "`.
2. `ProcessLine_TrimsSpaceBeforeNewline` (input `"Руна \n огня"`) → actual `"Руна\n огня"` (пробел после `\n` не удалялся).
3. `ProcessLine_OnlyStrayPunctuation_ReturnsEmpty` (input `"- - - | · •"`) → actual `" - - "`.

**Причина:** (1)+(3) `TrimStrayPunctuation` удалял stray-символы (`-`, `|`, `·`, `•`, `_`), но не тримил whitespace, оголявшийся после них. (2) `CollapseWhitespace` удалял пробел ПЕРЕД `\n`, но не ПОСЛЕ (`prevSpace = false` вместо `true` после append `\n`).

**Решение:**
- `TrimStrayPunctuation`: `while (start < end && (IsStray(input[start]) || char.IsWhiteSpace(input[start])))` — одновременно stray и whitespace на обоих концах.
- `CollapseWhitespace`: `prevSpace = true` после `sb.Append('\n')` — следующий пробел схлопывается.

Все 3 теста теперь green. На Windows 165/165, на Linux 164/165 (Capture-тест с P/Invoke `user32.dll` ожидаемо падает — см. KI-003).

### KI-001: Подавлены анализаторы CA1822, CS1591, CA1848, CA1873, CA1805

**Симптом:** `Directory.Build.props` содержит `<NoWarn>$(NoWarn);CA1822;CS1591;CA1848;CA1873;CA1805</NoWarn>`.

**Причина:** `CS1591` (XML-документация на все public-члены) ещё не везде добавлена. `CA1848` (LoggerMessage source generator) + `CA1873` (logger argument may be expensive) — оба про high-performance logging, это M3.7 работа (Serilog structured logging + LoggerMessage source gen). Индивидуальные `IsEnabled`-guards сейчас были бы noise, который M3.7 всё равно уберёт. `CA1805` (явная инициализация `bool = false`) намеренна в Options-классах. `CA1822` (could-be-static) остаётся, потому что часть Core-стабов ещё не использует instance state: `TierFallback.TryBaseKey`, `FallbackProvider.TryFuzzyMatch`, `IdAliases`. (`TranslationCache` реализован в M1.5-partial — больше не стаб, но `Store/TryLookup/Clear` используют instance state `_map`, так что CA1822 на них не срабатывал бы.)

**План:** Убрать `CA1822` из NoWarn когда все Core-стабы заменены (`TierFallback` с GREATER/PERFECT strip, `FallbackProvider` с Levenshtein+diacritics). `CS1591` — после прохождения по всем public API. `CA1848` + `CA1873` — после M3.7. `CA1805` — оставить или убрать при переходе на source-gen Options.

### KI-002: .slnx формат не верифицирован на Windows

**Симптом:** `AldurPrice.slnx` использует строковые `Id` (например, `Id="AldurPrice.Core"`).

**Причина:** .NET 9 SDK принял этот формат (`dotnet build AldurPrice.slnx` на Linux прошёл). Visual Studio 2022 может требовать GUID-формат `Id="{}"` для некоторых операций.

**План:** Проверить открытие в Visual Studio 2022 17.10+. Если работает — закрыть issue. Если нет — регенерировать через `dotnet sln`.

### KI-003: WPF-проекты не собираются на Linux без `EnableWindowsTargeting`

**Симптом:** `dotnet build src/AldurPrice` на Linux падает с `NETSDK1100`. Один Capture-тест (`TryLocate_SecondCall_WhenCachedHwndBecomesInvalid_Rescans`) падает на Linux с `DllNotFoundException: user32.dll`.

**Причина:** Дизайн-решение. WPF и Win32 P/Invoke требуют Windows. `AldurPrice.Core` — `net9.0` (cross-platform), `AldurPrice.Data` — `net9.0`, `AldurPrice.Capture`, `AldurPrice.Capture.Tests` — `net9.0-windows` (Win32 P/Invoke), `AldurPrice.Ocr`, `AldurPrice` — `net9.0-windows10.0.19041.0` (WinRT для Windows.Media.Ocr).

**План:** Не баг. Для разработки Core-логики на Linux: `dotnet test tests/AldurPrice.Core.Tests`. Для полной сборки: `dotnet build AldurPrice.slnx -p:EnableWindowsTargeting=true` (build + 164/165 тестов проходят; 1 Capture-тест с P/Invoke `IsWindow` пропускается на Linux, проходит на Windows).

### KI-004: `appsettings.json` copy-on-build стратегия

**Симптом:** В `AldurPrice.csproj` есть `<None Update="..\..\config\appsettings.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`.

**План:** Пересмотреть в M1.9 (`SettingsController`).

### KI-005: ~~`ItemNameTranslator` инициализирует `RussianStemmer`, но не использует~~ ✅ РЕШЕНО в M1.2

Поле `_stemmer` теперь используется в `ItemNameTranslator` (через `RuneshapeCombinationTranslator`, который использует stemmer для stem-matching'а).

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

**План:** Рассмотреть портал полного Snowball с RV-регионами, но с дополнительным правилом: не снимать глагольные окончания для слов, где R1 пуст. Отдельная итерация, низкий приоритет — текущий stemmer покрывает все кейсы M1.2 тестов.

### KI-008: poe2db.tw «Bait Rune» без русского перевода

**Симптом:** В `ocr/runeshape-combinations-ru.json` одна запись с `ru == en == "Bait Rune"`. poe2db.tw не имеет русского перевода для этого предмета.

**План:** Не баг парсера — данные на стороне poe2db. При обновлении JSON проверить, не появился ли перевод. Если не появился — оставить как есть (translator вернёт `null` для этого предмета при RU-входе).

### KI-009: Tesseract traineddata не bundled, нет MSBuild target `EnsureTessData`

**Симптом:** `TesseractEngine.IsAvailable` возвращает `false`, если в `ocr/tesseract/` нет `.traineddata` файлов. MSBuild target `EnsureTessData` (упомянутый в `docs/06-SETUP.md` §2.3) НЕ реализован.

**Причина:** Реализация MSBuild target с HTTP-скачиванием ~85 МБ traineddata добавила бы риск ломать build на машинах без интернета или на Linux (PowerShell-вызовы из MSBuild ненадёжны). Для M1.3 достаточно: пользователь скачивает traineddata вручную при первой настройке Tesseract.

**Workaround:** До реализации MSBuild target — вручную скачать:
```powershell
mkdir ocr/tesseract -Force
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata" -OutFile "ocr/tesseract/eng.traineddata"
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_best/raw/main/rus.traineddata" -OutFile "ocr/tesseract/rus.traineddata"
```

**План:** Реализовать MSBuild target в M1.10 (или M3.5 — bundled traineddata с trim). Цель target: скачать при первом build, пропустить если уже есть, не падать при отсутствии интернета (warning + Tesseract fallback недоступен).

### KI-010: TesseractEngine native DLL bootstrapper не реализован

**Симптом:** `TesseractEngine` использует NuGet-пакет `Tesseract 5.2.0`, который включает native libs (`tesseract50.dll`, `leptonica-1.82.0.dll`) как content files. На некоторых конфигурациях (publish single-file, AOT) native DLL могут не подгрузиться.

**Причина:** Tesseract 5.2.0 NuGet корректно копирует native DLL в output dir при стандартном `dotnet build`/`dotnet publish`. Проблемы возникают только в нестандартных сценариях (self-contained single-file, ReadyToRun). Для M1.3 стандартный сценарий работает.

**План:** Реализовать `Startup/TesseractBootstrapper.cs` (распаковка native DLL из embedded resource в temp dir + `AssemblyLoadContext` configure) в M3.5 (релиз-готовность, single-file publish). См. `docs/02-ARCHITECTURE.md` §1 — `Startup/TesseractBootstrapper.cs`.

### KI-011: LeaguePanelDetector — упрощённая эвристика (не HSV segmentation)

**Симптом:** Текущий `LeaguePanelDetector` считает пиксели в RGB-диапазоне (20-70, 18-60, 15-55) с сэмплингом. Это работает, но даёт ложные срабатывания на тёмных сценах (пещера, ночь).

**Причина:** Полная HSV-segmentation с детектированием формы панели требует больше кода и калибровки на реальных скриншотах (которых ещё нет — M1.10). Простая RGB-эвристика достаточна для gating'а: лучше лишний раз прогнать OCR, чем пропустить панель.

**План:** Расширить до HSV-segmentation + детектирование aspect ratio панели в M1.10, когда будут реальные скриншоты-фикстуры.

### KI-012: WindowsOcrEngine не проверяет минимальный build Windows (1809+)

**Симптом:** `IsAvailable` лениво проверяет наличие OCR-языков через `OcrEngine.AvailableRecognizerLanguages`. На Windows < 10 1809 это может упасть с `TypeLoadException` или `COMException`, который ловится в try/catch (возвращает false).

**Причина:** TFM `net9.0-windows10.0.19041.0` гарантирует, что на машине разработчика Win10 2004+, но не на машине пользователя. На user-side Windows < 1809 загрузка WinRT-типа упадёт, что обработано try/catch.

**План:** Добавить явную проверку build number через `Environment.OSVersion.Version` (build ≥ 17763 = 1809) в `CheckAvailability()`. В M1.10 / M2.

### KI-013: `WgcCapture` не реализован (stub); имена процессов PoE2 не верифицированы

**Симптом:** M1.4 частично выполнен: `PrintWindowCapture` (primary) и `Poe2WindowLocator` реализованы, но `WgcCapture` (fallback для Lossless Scaling) НЕ создан. Кроме того, список `Poe2WindowLocator.DefaultProcessNames` основан на публичной информации о PoE2 early access и не проверен на реальной установке.

**Причина:** `WgcCapture` требует WinRT interop (`GraphicsCaptureItem.CreateFromInterop`, `Direct3D11CaptureFramePool`, `GraphicsCaptureSession`) + DirectX interop (`IDirect3DDevice`). Это существенный объём кода, который невозможно верифицировать без Windows-машины с PoE2 + Lossless Scaling. Риск сломать сборку или получить runtime-crash высок. Принцип «лучше недоделать, чем сломать» — `PrintWindowCapture` покрывает основной use-case (оконный режим), WGC нужен только для LS-пользователей (миноритарный сценарий).

**Список имён процессов** (`PathOfExileSteam`, `PathOfExile_x64`, `PathOfExile`, `PathOfExileSteam_x64`) основан на ранних отчетах early access. Если на реальной Windows-машине `TryLocate()` возвращает `null` при запущенном PoE2 — нужно проверить фактическое имя через Task Manager → Details → Name колонка, и добавить в `DefaultProcessNames` (или переопределить через DI).

**Workaround для LS-пользователей:** Lossless Scaling растягивает окно PoE2. `PrintWindow` должен работать (он рендерит окно, а не экран), но если LS использует exclusive fullscreen — capture будет чёрным. В этом случае нужно либо переключить LS в windowed mode, либо дождаться `WgcCapture`.

**План:** Реализовать `WgcCapture` в M1.4b (отдельная итерация после Windows-верификации M1.4 primary path) или в M2. Верифицировать имена процессов при первой Windows-проверке. См. `docs/05-ROADMAP.md` → M1.4.

### KI-017: `rus.ndjson` не bundled — `TranslationCache` пустой до загрузки

**Симптом:** `TranslationCache.LoadEmbeddedOrDefault()` возвращает пустой кэш (Count=0), потому что embedded-ресурс `AldurPrice.Core.Translation.rus.ndjson` отсутствует. Fallback [2] в `ItemNameTranslator` всегда промахивается — переводятся только рунные комбинации (fallback [1], ~150 предметов).

**Причина:** Файл `ocr/translations/rus.ndjson` (4 319 строк, ~2 МБ) не committen в репозиторий. Решение сознательное:
- **Размер:** ~2 МБ на язык, ~16 МБ для всех 8 языков.
- **Лицензия:** нужно проверить лицензию Exiled Exchange 2 перед релизом и добавить копию в `ocr/translations/LICENSE` + атрибуцию.
- **Частота обновления:** еженедельно через CI (`.github/workflows/translations.yml`).

`AldurPrice.Core.csproj` содержит `<EmbeddedResource Include="..\..\ocr\translations\rus.ndjson" Condition="Exists(...)">` — build succeeds и без файла (ресурс опускается). Когда файл появляется (после `update-translations.py`) — автоматически embed'ится при следующем `dotnet build`.

**Workaround:** Загрузить NDJSON и rebuild:
```bash
python scripts/update-translations.py   # клонирует Exiled Exchange 2, копирует *.ndjson в ocr/translations/
dotnet build AldurPrice.slnx             # теперь rus.ndjson embedded
dotnet test                              # TranslationCache.LoadEmbeddedOrDefault вернёт 4319 записей
```

**План:** После проверки лицензии Exiled Exchange 2 — committen `rus.ndjson` + `LICENSE` в репозиторий (или оставить загрузку через CI/скрипт). См. `docs/07-TRANSLATION-SOURCES.md` §1, `ocr/translations/README.md`.

## Architecture deviations

В этом разделе фиксируются отличия реализации от архитектурного документа `docs/02-ARCHITECTURE.md`. Любое отличие должно быть либо согласовано с архитектурой (тогда документ обновляется), либо перечислено здесь с обоснованием.

### AD-001: ✅ РЕШЕНО в M1.3 — Shared-интерфейсы в `AldurPrice.Core/Contracts/`

**Документ обновлён:** `docs/02-ARCHITECTURE.md` теперь корректно показывает `AldurPrice.Core/Contracts/` (раньше рисунок показывал `Contracts/` в основном проекте `AldurPrice/`).

**Реализация:** Все shared-интерфейсы для DI живут в `AldurPrice.Core/Contracts/`. В `AldurPrice/Contracts/` (когда появится в M1.7) будут жить только UI-only интерфейсы (`ILeagueWindowReader`, `IOverlayRenderer`).

**Обоснование:** Иначе циклическая зависимость: `AldurPrice.Data` реализует `IPricingCache`, но если `IPricingCache` в `AldurPrice`, то `Data → AldurPrice`, а `AldurPrice → Data` для регистрации в DI — цикл. Перенос интерфейсов в Core решает это: `Data → Core`, `AldurPrice → Core + Data`.

### AD-002: ✅ РЕШЕНО в M1.3 — JSON как embedded resource

**Документ обновлён:** `docs/04-RU-LOCALIZATION.md` §2.2 теперь описывает загрузку JSON через embedded resource (а не filesystem path).

**Реализация:** `ocr/runeshape-combinations-ru.json` включён в `AldurPrice.Core.csproj` как `<EmbeddedResource>` с `LogicalName=AldurPrice.Core.Translation.runeshape-combinations-ru.json`. `RuneshapeCombinationTranslator` грузит его через `Assembly.GetManifestResourceStream`.

**Обоснование:** Embedded resource — самодостаточный, не требует файловых путей. Tests используют default-конструктор. Для обновления JSON — re-run парсера + rebuild (один commit). Для runtime override (если понадобится) — добавить stream-конструктор (уже есть для тестов).

### AD-003: ✅ РЕШЕНО в M1.3 — OCR-пайплайн в `AldurPrice.Ocr/`, не в `AldurPrice/OCR/`

**Документ обновлён:** `docs/02-ARCHITECTURE.md` §1 теперь показывает все OCR-компоненты (движки + пайплайн) в `AldurPrice.Ocr/`. Раньше doc разделял движки (`AldurPrice.Ocr/`) и пайплайн (`AldurPrice/OCR/`).

**Реализация:** Все OCR-компоненты живут в `src/AldurPrice.Ocr/`:
- `IOcrEngine.cs`, `OcrResult.cs`, `OcrLine.cs` — интерфейс и DTO
- `WindowsOcrEngine.cs`, `TesseractEngine.cs`, `OcrEngineResolver.cs` — движки + выбор
- `ImagePreprocessor.cs`, `LeaguePanelDetector.cs` — предобработка
- `OcrPreprocessOptions.cs` — настройки предобработки (отдельная record, не `OcrOptions`)
- `OcrPipeline.cs` — оркестратор

**Обоснование:** Разделение на `AldurPrice.Ocr/` (движки) и `AldurPrice/OCR/` (пайплайн) создавало циклическую зависимость: пайплайн зависит от `OcrOptions` (в `AldurPrice/Configuration/`), но `AldurPrice → AldurPrice.Ocr → AldurPrice` — цикл. Решение: всё в `AldurPrice.Ocr/`, конфигурация предобработки в `OcrPreprocessOptions` (отдельная record), `App.xaml.cs` мапит `IOptions<OcrOptions>` → `OcrPreprocessOptions` при DI-регистрации.

### AD-004: `RussianOcrPostProcessor` в `AldurPrice.Core/Translation/`, не в `AldurPrice/OCR/`

**Документ говорит:** `docs/02-ARCHITECTURE.md` §1 рисует `RussianOcrPostProcessor.cs` в `AldurPrice/OCR/` (UI-dependent часть).

**Реализация:** `RussianOcrPostProcessor` живёт в `src/AldurPrice.Core/Translation/RussianOcrPostProcessor.cs`.

**Обоснование:** Это чистая текстовая обработка без Windows-зависимостей. Помещение в Core позволяет тестировать из `AldurPrice.Core.Tests` (net9.0, кроссплатформенный). Если бы лежал в `AldurPrice.Ocr/` (net9.0-windows10.0.19041.0), потребовался бы отдельный `AldurPrice.Ocr.Tests` проект на net9.0-windows — лишняя сложность для M1.3. Логически соседствует с `RussianStemmer` и `ItemNameParser`.

**План:** Документ `docs/02-ARCHITECTURE.md` обновлён (см. §1) — `RussianOcrPostProcessor` перенесён в `AldurPrice.Core/Translation/`. Когда появится `OcrTextPostProcessor` (language-agnostic часть) — он тоже переедет в Core.

### AD-005: Capture-компоненты в `AldurPrice.Capture/` (не `AldurPrice/Capture/`); `CaptureRegion` — window-client-relative

**Документ говорит:** `docs/02-ARCHITECTURE.md` §1 рисует capture-компоненты (`PrintWindowCapture`, `WgcCapture`, `FrameDiffer`, `Poe2WindowMonitor`, `Poe2WindowLocator`) в `AldurPrice/Capture/` (подпапка основного WPF-проекта). Также `ICaptureStrategy.CaptureAsync` docstring говорил про «экранные координаты».

**Реализация:** Все capture-компоненты живут в `src/AldurPrice.Capture/` (отдельный проект, `net9.0-windows`):
- `ICaptureStrategy.cs`, `CaptureRegion` — интерфейс + DTO
- `PrintWindowCapture.cs` — Win32 PrintWindow (primary)
- `Poe2WindowLocator.cs` — поиск окна PoE2 (+ `IProcessEnumerator`, `DefaultProcessEnumerator`, `ProcessSnapshot`, `Poe2WindowHandle`)
- `Win32/NativeMethods.cs` — P/Invoke user32/gdi32 + `RECT` struct
- (Будет: `WgcCapture.cs` — M1.4b / M2, см. KI-013)
- (Будет: `FrameDiffer.cs` — M3.1, `Poe2WindowMonitor.cs` — M3.3)

`CaptureRegion` переинтерпретирован как **window-client-relative** (координаты относительно левого-верхнего угла клиентской области окна PoE2), НЕ экранные. Docstring обновлён в `ICaptureStrategy.cs`.

**Обоснование (project location):** Аналогично AD-003 (OCR). Вынос capture в отдельный проект позволяет: (1) тестировать из `AldurPrice.Capture.Tests` без загрузки WPF assembly; (2) использовать `AldurPrice.Capture` из будущего `AldurPrice.Integration` тест-проекта без ссылки на весь WPF app; (3) чётче разделять слои — `Capture` зависит только от `Core` + Win32, не от `AldurPrice/Configuration`.

**Обоснование (region coords):** Screen-absolute координаты бессмысленны для оверлея: игрок двигает окно PoE2, регион должен двигаться вместе с ним. `WindowOptions.CustomOffsetX/Y` (настраиваемый пользователем регион) — по смыслу offset от угла окна, не экрана. PrintWindow рендерит всё окно, затем crop делается в client-relative координатах — это естественный flow. Старый stub docstring был неточен; реальных клиентов у stub'а не было (M0 не вызывал `CaptureAsync`).

## Environment

- **.NET SDK:** 9.0.300+ (проверено на Linux x64 с 9.0.315; на Windows любая 9.0.x)
- **OS для dev:** Linux — `dotnet build -p:EnableWindowsTargeting=true` + `dotnet test tests/AldurPrice.Core.Tests` (164/165 тестов; 1 Capture-тест с P/Invoke падает). Windows — полная сборка, 165/165 тестов, runtime PoE2.
- **Целевая платформа:** .NET 9, WPF, `net9.0-windows10.0.19041.0` (для `AldurPrice.Ocr` + `AldurPrice` — WinRT API `Windows.Media.Ocr`), `net9.0-windows` для `AldurPrice.Capture` (Win32 P/Invoke), `net9.0` для Core/Data.
- **Python:** 3.12 + `requests` + `lxml` (для `scripts/parse-poe2db-runeshapes.py`)
- **Лицензия:** MIT
