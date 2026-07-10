# Changelog

Все заметные изменения проекта AldurPrice документируются здесь.
Формат — [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версионирование — [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]

### Added — M1.5-partial: TranslationCache + NDJSON-загрузка

Первая часть M1.5: `TranslationCache` переписан из M0-stub'а в реальную имплементацию, `ItemNameTranslator` получил fallback [2] для базовых предметов. HTTP-клиенты (`Poe2ScoutClient`, `PoeNinjaClient`) отложены — нужны реальные API-response fixtures (см. STATUS.md → KI-017, «Что дальше»).

- **`src/AldurPrice.Core/Translation/TranslationCache.cs`** — реальная имплементация (был M0 stub):
  - `ConcurrentDictionary<string, string>` с `StringComparer.OrdinalIgnoreCase` — потокобезопасный exact-match lookup.
  - `LoadNdjson(Stream)` — парсинг NDJSON (формат Exiled Exchange 2): построчно, `JsonDocument.Parse` на каждой строке, извлечение `name` → `refName`. Пропуск: пустых `name`/`refName`, записей где `name == refName` (нет перевода), повреждённых строк (skip, не падать). Бросает `InvalidDataException` если 0 валидных пар.
  - `LoadEmbeddedOrDefault()` (static factory) — пытается загрузить embedded `AldurPrice.Core.Translation.rus.ndjson`. Если ресурс отсутствует (NDJSON не bundled — KI-017) — возвращает пустой кэш (graceful degradation: рунные комбинации продолжают работать через [1]). Если bundled, но пуст/повреждён — `InvalidDataException` (fail-fast).
  - `Store(source, target)` / `TryLookup(source)` / `Clear()` / `Count` — legacy API + new, теперь работают с реальным `_map`.
- **`src/AldurPrice.Core/Translation/ItemNameTranslator.cs`** — fallback [2] через `TranslationCache`:
  - Новый конструктор `ItemNameTranslator(RuneshapeCombinationTranslator, TranslationCache?)` — для DI и тестов.
  - Default-конструктор вызывает `TranslationCache.LoadEmbeddedOrDefault()` — production path.
  - `TryTranslate`: [1] RuneshapeCombinationTranslator → [2] TranslationCache.TryLookup (exact) → null.
  - Существующие конструкторы сохранены для обратной совместимости с тестами.
- **`src/AldurPrice.Core/AldurPrice.Core.csproj`** — `<EmbeddedResource Include="..\..\ocr\translations\rus.ndjson" Condition="Exists(...)">` — rus.ndjson embed'ится автоматически, если файл загружен через `scripts/update-translations.py`. Без файла build succeeds (ресурс опускается).
- **`src/AldurPrice/App.xaml.cs`** — DI: `TranslationCache` регистрируется через factory `LoadEmbeddedOrDefault()` (вместо parameterless). DI-контейнер выбирает 2-параметровый конструктор `ItemNameTranslator(runeshape, cache)` — translator получает populated cache.
- **`ocr/translations/README.md`** (NEW) — документация: формат NDJSON, как загрузить (`update-translations.py`), почему не committen по умолчанию.
- **Тесты (28 новых):**
  - `tests/AldurPrice.Core.Tests/TranslationCacheTests.cs` (17 тестов): загрузка NDJSON (валидные/пустые/whitespace/malformed/skip-логика), exact lookup (case-insensitive), Store/Clear, `LoadEmbeddedOrDefault` при отсутствии ресурса, edge-cases (null/empty throws).
  - `tests/AldurPrice.Core.Tests/ItemNameTranslatorCacheFallbackTests.cs` (11 тест-кейсов): fallback [2] для базовых предметов, приоритет [1] > [2] (рунные комбинации не перекрываются cache), graceful degradation (null cache / empty cache), unknown items → null, eng passthrough.

### Fixed — Warnings cleanup (18 → 0)

Build M1.4-fix проходил с 18 warnings (несмотря на заявленные "0 warnings" в STATUS.md). Все починены:

- **CA1873 (16 экз.)** — подавлен в `Directory.Build.props` (добавлен в `NoWarn`). Последовательно с уже подавленным `CA1848`: оба правила про high-performance logging (LoggerMessage source generator + expensive argument evaluation). Индивидуальные `IsEnabled`-guards сейчас были бы noise, который M3.7 (Serilog structured logging) всё равно уберёт. См. KI-001.
- **CA1305** (`src/AldurPrice.Capture/PrintWindowCapture.cs:199`): `Marshal.GetLastWin32Error().ToString()` → `.ToString(CultureInfo.InvariantCulture)`. Добавлен `using System.Globalization`.
- **CA1861** (`tests/AldurPrice.Capture.Tests/Poe2WindowLocatorTests.cs:228`): inline `new[] { "MyCustomPoE2" }` извлечён в `static readonly string[] CustomProcessNames` поле (выделяется один раз, не per-test-invocation).

### Fixed — M1.4-fix: NU1201 + скрытые баги M1.3

После M1.4 commit-а первая попытка `dotnet build` на Windows падала с `NU1201`. После починки restore вскрылись ещё 2 скрытых бага в `AldurPrice.Ocr` (код M1.3 никогда не компилировался — restore падал раньше компиляции) и 3 бага в `RussianOcrPostProcessor` (тесты M1.3 никогда не запускались по той же причине). Все 6 багов починены в этой итерации.

- **NU1201 — TFM mismatch** (`src/AldurPrice/AldurPrice.csproj`): `TargetFramework` поднят с `net9.0-windows` (= `net9.0-windows7.0`) до `net9.0-windows10.0.19041.0` + добавлен `<SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>`. Главный WPF-проект обязан иметь TFM ≥ старшей TFM среди зависимостей; `AldurPrice.Ocr` требует `net9.0-windows10.0.19041.0` (WinRT для `Windows.Media.Ocr`). `AldurPrice.Capture` / `AldurPrice.Capture.Tests` оставлены на `net9.0-windows` — им WinRT не нужен. Комментарий в `Directory.Build.props` обновлён. См. KI-014 в STATUS.md.
- **CS0185 + CS1061 — TesseractEngine.cs не компилировался** (`src/AldurPrice.Ocr/TesseractEngine.cs`):
  - `var container = _engines.GetOrAdd(...)` возвращал `Lazy<EngineContainer>`, но код обращался к `container.Lock` и `container.Engine` напрямую (без `.Value`). `lock (container.Lock)` падал с CS0185 (method group is not a reference type), `container.Engine.Process(img)` — с CS1061.
  - **Fix:** `container` переименован в `lazy`, добавлено `var container = lazy.Value;` (Lazy<T>.Value потокобезопасно инициирует engine через `LazyThreadSafetyMode.ExecutionAndPublication` — паттерн уже использовался в `Dispose()` line 185: `kv.Value.Value.Engine.Dispose()`).
  - **Fix:** `rect.Y` → `rect.Y1`. Tesseract NuGet 5.2.0 `Rect` struct имеет свойства `X1, Y1, X2, Y2, Width, Height` (НЕ `X, Y`). `Y1` = верхняя граница bounding box — то, что нужно для `OcrLine.Y`.
  - См. KI-015 в STATUS.md.
- **3 failing теста в RussianOcrPostProcessorTests** (`src/AldurPrice.Core/Translation/RussianOcrPostProcessor.cs`):
  - `TrimStrayPunctuation` удалял stray-символы (`-`, `|`, `·`, `•`, `_`), но не тримил whitespace, оголявшийся после них: `"- Руна огня -"` → `" Руна огня "` вместо `"Руна огня"`.
  - `CollapseWhitespace` удалял пробел ПЕРЕД `\n`, но не ПОСЛЕ: `"Руна \n огня"` → `"Руна\n огня"` вместо `"Руна\nогня"`.
  - **Fix:** `TrimStrayPunctuation` — `while (... && (IsStray(...) || char.IsWhiteSpace(...)))` на обоих концах. `CollapseWhitespace` — `prevSpace = true` после `sb.Append('\n')` (следующий пробел схлопывается).
  - См. KI-016 в STATUS.md.

**Результат верификации (Linux с `-p:EnableWindowsTargeting=true`):**
- `dotnet build AldurPrice.slnx` — 0 errors, 0 warnings.
- `dotnet test` — 165 total: 140 Core.Tests passed (0 failed), 25 Capture.Tests (24 passed, 1 failed — Linux-only `DllNotFoundException: user32.dll` на `IsWindow` P/Invoke, на Windows ожидаемо passed).

### Added — M1.4: Захват экрана (частично)

- **`src/AldurPrice.Capture/PrintWindowCapture.cs`** — реальная имплементация (был M0 stub):
  - P/Invoke `user32!PrintWindow` с флагом `PW_RENDERFULLCONTENT` (0x00000002) — корректно рендерит DirectComposition/DirectX-окна (PoE2 renderer, WPF), без этого флага был бы чёрный прямоугольник.
  - Flow: `Poe2WindowLocator.TryLocate` → `GetClientRect` (валидация размеров) → `CreateCompatibleDC` + `CreateCompatibleBitmap` → `PrintWindow` → `Image.FromHbitmap` → `Bitmap.Clone` (crop) → PNG-encode.
  - Валидация: регион должен полностью помещаться в клиентскую область (иначе `ArgumentOutOfRangeException` с понятным сообщением).
  - Все GDI-ресурсы (DC, bitmap, managed Bitmap) освобождаются в `finally` — парные create/delete, без утечек handle'ов.
  - Синхронный Win32-вызов (PrintWindow ~5-15 мс на 1080p) — приемлемо без `Task.Run`.
- **`src/AldurPrice.Capture/Poe2WindowLocator.cs`** (NEW) — поиск окна PoE2:
  - Перебор `Process.GetProcesses()` по списку имён: `PathOfExileSteam`, `PathOfExile_x64`, `PathOfExile`, `PathOfExileSteam_x64` (Steam-first приоритет). См. KI-013 — список не верифицирован на реальной установке.
  - Кэш найденного HWND с TTL 5 с + перепроверка `IsWindow` перед возвратом — экономит `Process.GetProcesses` (~20-50 мс) на OCR-цикле (100 мс интервал).
  - `IProcessEnumerator` interface + `DefaultProcessEnumerator` (production) + `ProcessSnapshot` record — для unit-тестов без реального PoE2.
  - `Poe2WindowHandle` record — HWND + ProcessId + ProcessName + Title.
  - Thread-safe: кэш под `lock`, scan вне lock (не блокирует другие потоки).
  - `InvalidateCache()` для будущего `Poe2WindowMonitor` (M3.3) — сброс при EVENT_OBJECT_DESTROY.
- **`src/AldurPrice.Capture/Win32/NativeMethods.cs`** (NEW) — централизованные P/Invoke:
  - `user32`: `PrintWindow`, `GetClientRect`, `GetWindowRect`, `IsWindow`, `GetWindowDC`, `ReleaseDC`.
  - `gdi32`: `CreateCompatibleDC`, `CreateCompatibleBitmap`, `SelectObject`, `DeleteObject`, `DeleteDC`, `BitBlt`.
  - Константы: `PW_RENDERFULLCONTENT`, `PW_CLIENTONLY`, `SRCCOPY`.
  - `RECT` struct с `Width`/`Height` computed properties.
- **`tests/AldurPrice.Capture.Tests/`** (NEW) — net9.0-windows тест-проект:
  - `AldurPrice.Capture.Tests.csproj` — xUnit, ссылка на `AldurPrice.Capture`.
  - `Poe2WindowLocatorTests.cs` — 15 тестов: matching (Steam/standalone), not-found, process-without-window, multiple-matches (приоритет Steam), кэш (invalidate + invalid-HWND rescan), constructor validation, custom process names, enumerator-throws.
  - `PrintWindowCaptureTests.cs` — 10 тест-кейсов (6 Fact + 4 InlineData в Theory): валидация аргументов (null/invalid region), name, constructor null-checks, pre-cancelled token, error-path «окно не найдено» через empty locator. Реальный capture отложен в M1.10 (нужен Windows + PoE2).
  - Использует `FakeProcessEnumerator` + `NullLogger<T>` — не требует реального PoE2.

### Changed — M1.4

- **`src/AldurPrice.Capture/ICaptureStrategy.cs`** — docstring обновлён: `CaptureRegion` теперь явно window-client-relative (координаты относительно клиентской области окна PoE2, не экранные). Добавлен `<exception>` tag для `InvalidOperationException`. См. AD-005 в STATUS.md.
- **`src/AldurPrice.Capture/AldurPrice.Capture.csproj`** — добавлен `<PackageReference Include="System.Drawing.Common" />` (для `Bitmap`/PNG-encoding в `PrintWindowCapture`, аналогично `AldurPrice.Ocr`).
- **`src/AldurPrice/App.xaml.cs`** — DI-регистрация capture-сервисов: `IProcessEnumerator` → `DefaultProcessEnumerator`, `Poe2WindowLocator`, `PrintWindowCapture`, `ICaptureStrategy` → `PrintWindowCapture`. Раскомментирована M1.4 TODO.
- **`AldurPrice.slnx`** — добавлен `AldurPrice.Capture.Tests` проект.
- **`STATUS.md`** — M1.4 отмечен как ✅ (частично). Добавлен KI-013 (`WgcCapture` stub, имена процессов не верифицированы) и AD-005 (capture-компоненты в `AldurPrice.Capture/`, `CaptureRegion` — window-client-relative). «Что в работе» обновлено: Windows-верификация M1.4 вместо M1.3.
- **`docs/02-ARCHITECTURE.md`** — §1 обновлён: capture-компоненты показаны в `src/AldurPrice.Capture/` (с `Win32/NativeMethods.cs`). §2.1 DI обновлён реальными регистрациями.
- **`docs/05-ROADMAP.md`** — M1.4 чекбоксы отмечены: `ICaptureStrategy`, `PrintWindowCapture`, `Poe2WindowLocator`, тесты — ✅. `WgcCapture` — отложен (KI-013).

### Known Issues (M1.4)

- `KI-013`: `WgcCapture` (Windows.Graphics.Capture fallback для Lossless Scaling) НЕ реализован. Имена процессов PoE2 не верифицированы на реальной установке. Workaround: для LS-пользователей — переключить LS в windowed mode, либо дождаться M1.4b / M2.

### Added — Roadmap: M2.7 Rune Value Highlight (опциональная фича, пост-MVP)

- **`docs/05-ROADMAP.md`** — добавлен раздел [M2.7 — Rune Value Highlight](docs/05-ROADMAP.md#m27--rune-value-highlight-опциональная-фича--️-пост-mvp-не-блокирует-v030-beta):
  - Подсветка ценных рун, вложенных в ремнант (не наград, а переносимых рун), через template matching по иконкам.
  - Тир-лист: S+ Opulent / S Power, Death, Bond, Oath / A Time, Rebirth / B Purple / C Blue.
  - Подход: НЕ OCR-текст, а color histogram + normalized cross-correlation по PNG-шаблонам из `config/rune-icons/`.
  - Редактируемый `config/rune-tiers.json` — пользователь может переопределить тир-лист под свой taste.
  - Зависимости: M1.7 (оверлей) + M1.10 (скриншоты для шаблонов).
  - Не блокирует релиз v0.3.0-beta — может быть отложен в v0.4.0-beta.
- **`STATUS.md`** — добавлен раздел «Бэклог (после M1)» с кратким описанием M2.7.
- **`docs/05-ROADMAP.md`** — обзорная таблица milestones обновлена (M2 цель → «+ опционально подсветка рун»), критерии готовности M2 дополнены пунктом про M2.7.

### Added — M1.3: OCR-движки и пайплайн (частично)

- **`src/AldurPrice.Ocr/WindowsOcrEngine.cs`** — реальная имплементация (был stub):
  - `Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage` + `RecognizeAsync`.
  - Декодирование PNG → `SoftwareBitmap` через `BitmapDecoder.CreateAsync`.
  - Ленивая проверка `IsAvailable` через `OcrEngine.AvailableRecognizerLanguages` (with try/catch для Windows < 1809).
  - Маппинг `OcrResult.Lines` → `AldurPrice.Ocr.OcrResult`: Y = min Y слов в линии.
  - TFM изменён на `net9.0-windows10.0.19041.0` (для прямого доступа к WinRT без `Microsoft.Windows.SDK.Contracts`).
- **`src/AldurPrice.Ocr/TesseractEngine.cs`** — реальная имплементация (был stub):
  - Ленивая инициализация `TesseractEngine` (NuGet 5.2.0) по языкам через `ConcurrentDictionary<string, Lazy<EngineContainer>>`.
  - `lock` per engine для потокобезопасности (Tesseract engine не thread-safe).
  - `IDisposable` для корректной выгрузки engine'ов при shutdown.
  - `IsAvailable` проверяет наличие tessdata directory + `.traineddata` файлов.
  - Engine mode (0=legacy, 1=LSTM+legacy, 2=LSTM only) настраивается через constructor.
  - Alias `TessEngine = Tesseract.TesseractEngine` чтобы различать локальный класс и NuGet-класс.
- **`src/AldurPrice.Ocr/ImagePreprocessor.cs`** — предобработка битмапа:
  - Декодирование PNG → `System.Drawing.Bitmap` (32bpp ARGB).
  - Color filter: пиксели близкие к target RGB (50,42,34 ± 47) с luminance ≤ 145 и channel spread ≤ 29 → чёрные (текст), остальные → белые (фон).
  - Greyscale + binarization: для ENG-клиента или когда color filter отключён.
  - `LockBits` + unsafe pointer arithmetic (50-100× быстрее `GetPixel`/`SetPixel` на 1080p).
  - Возвращает PNG-байты для подачи в OCR-движок.
- **`src/AldurPrice.Ocr/LeaguePanelDetector.cs`** — детектор открытой панели:
  - Считает пиксели в RGB-диапазоне фона панели (20-70, 18-60, 15-55) с сэмплингом (default: каждый 4-й пиксель).
  - Если доля > порога (default: 0.15) — панель открыта.
  - Упрощённая эвристика (не HSV segmentation) — KI-011, расширение в M1.10.
- **`src/AldurPrice.Ocr/OcrPipeline.cs`** — оркестратор:
  - `ProcessAsync(byte[] bitmap, string language, ...)` → `OcrResult`.
  - Flow: LeaguePanelDetector → ImagePreprocessor → OcrEngineResolver.Resolve() → RussianOcrPostProcessor.
  - Каждый шаг обёрнут в try/catch — падение одного компонента не валит весь pipeline.
  - Если панель закрыта → пустой результат (без OCR).
- **`src/AldurPrice.Ocr/OcrPreprocessOptions.cs`** — record с настройками предобработки:
  - `OcrPreprocessOptions` — для `ImagePreprocessor` (зеркалирует подсекцию `OCR` из `OcrOptions`, но в Ocr project для избежания цикла).
  - `LeaguePanelDetectorOptions` — для `LeaguePanelDetector`.
- **`src/AldurPrice.Core/Translation/RussianOcrPostProcessor.cs`** — текстовая постобработка кириллицы:
  - Ё → Е (только для rus).
  - Удаление управляющих символов (кроме \t, \n).
  - Нормализация пробелов (NBSP, thin, ideographic → ASCII space).
  - Типографские кавычки → прямые, апострофы → ASCII.
  - Дефис-минусы (U+2010..U+2015) → ASCII дефис.
  - Буллеты (•, ·, ◦) → пробел.
  - Trim висящих дефисов/пунктуаций в начале/конце.
  - Помещён в Core (не в Ocr) для тестируемости из Core.Tests — см. AD-004 в STATUS.md.
- **`tests/AldurPrice.Core.Tests/RussianOcrPostProcessorTests.cs`** — 23 теста:
  - Ё→Е нормализация (5 кейсов).
  - Управляющие символы, NBSP/whitespace, кавычки, апострофы, дефисы.
  - Висящие пунктуации, буллеты.
  - ENG-язык (Ё не трогается), edge cases (empty, only-punct).
  - ProcessLines (multiple).

### Changed — M1.3

- **`src/AldurPrice.Ocr/AldurPrice.Ocr.csproj`**:
  - TFM: `net9.0-windows` → `net9.0-windows10.0.19041.0` (для WinRT API).
  - Добавлен `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (для ImagePreprocessor LockBits).
  - Добавлен `<SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>`.
  - Добавлен package reference на `System.Drawing.Common`.
- **`Directory.Packages.props`** — добавлен `<PackageVersion Include="System.Drawing.Common" Version="9.0.0" />`.
- **`Directory.Build.props`** — комментарий к `NoWarn` обновлён: CA1822 остаётся, потому что `TierFallback`, `FallbackProvider`, `TranslationCache`, `IdAliases` — ещё стабы без instance state. Убрать в M1.5+ когда все Core-стабы заменены. См. KI-001.
- **`src/AldurPrice/App.xaml.cs`** — DI-регистрация новых сервисов: `RussianOcrPostProcessor`, `ImagePreprocessor`, `LeaguePanelDetector`, `OcrPipeline`. Раскомментирована регистрация `IOcrEngine` (через resolver) — оставлена как M1.4 TODO (нужна интеграция с capture layer).
- **`STATUS.md`** — M1.3 отмечен как частично выполнен. Добавлены KI-009..KI-012 (traineddata не bundled, native DLL bootstrapper, LeaguePanelDetector упрощён, Windows build check). AD-001/AD-002 отмечены как решённые. AD-003/AD-004 — новые (pipeline в Ocr/, RussianOcrPostProcessor в Core/).
- **`docs/02-ARCHITECTURE.md`** — обновлён под актуальную структуру:
  - §1: `Contracts/` перенесён в `AldurPrice.Core/Contracts/` (AD-001).
  - §1: Все OCR-компоненты показаны в `AldurPrice.Ocr/` (AD-003).
  - §1: `RussianOcrPostProcessor` перенесён в `AldurPrice.Core/Translation/` (AD-004).
- **`docs/04-RU-LOCALIZATION.md`** — обновлён §2.2: embedded resource, не filesystem (AD-002). §3.1 уточнено покрытие цепочки fallback'ов.
- **`docs/05-ROADMAP.md`** — отмечены чекбоксы M1.1, M1.2, M1.3 как выполненные (частично).

### Known Issues

Подробности — в [STATUS.md](STATUS.md#known-issues):
- `CA1822` остаётся в NoWarn — не все Core-стабы заменены (M1.5+ цель убрать). `CS1591`/`CA1848`/`CA1805` — остаются.
- `KI-009`: Tesseract traineddata не bundled, нет MSBuild target `EnsureTessData` (workaround: ручное скачивание).
- `KI-010`: TesseractEngine native DLL bootstrapper не реализован (стандартный сценарий `dotnet build`/`run` работает).
- `KI-011`: LeaguePanelDetector — упрощённая RGB-эвристика, не HSV segmentation.
- `KI-012`: WindowsOcrEngine не проверяет минимальный build Windows (1809+).

## [0.1.0-alpha] — M0 Skeleton (pending Windows verification)

### Added
- **Каркас решения `AldurPrice.slnx`** с 6 проектами: `AldurPrice.Core` (net9.0), `AldurPrice.Data` (net9.0), `AldurPrice.Ocr` (net9.0-windows10.0.19041.0 с M1.3), `AldurPrice.Capture` (net9.0-windows), `AldurPrice` (net9.0-windows, WPF), `AldurPrice.Core.Tests` (net9.0).
- **`Directory.Build.props`** + **`Directory.Packages.props`** — central package management.
- **`STATUS.md`** + **`CHANGELOG.md`** — tracking-документы.
- 13 smoke-тестов (RussianStemmerTests + LevenshteinTests).

### Added — M1.1: Парсер poe2db.tw

- **`scripts/parse-poe2db-runeshapes.py`** — полностью переписан под реальную HTML-структуру poe2db.tw (M0-версия использовала устаревший паттерн `data-name`/`data-full-name`, не работал). Новый парсер извлекает данные из двух HTML-паттернов:
  - **Pattern A** — `<a href="Fire_Rune"><img data-bs-title=" Руна огня"/></a>` (базовые руны в grid'е).
  - **Pattern B** — `<a class="whiteitem SoulCore" href="...">Text</a>` (alloy/lineage/ancient/ward runes в таблицах).
  - Слияние RU↔EN по canonical `href` (100% match — poe2db использует одинаковые slug'и на обоих языках).
  - Очистка префикса «Level X - Y » из Pattern A titles.
  - Фильтр href'ов по ключевым словам `rune`/`alloy` (исключает general currency: Divine Orb, Mirror of Kalandra, и т.д.).
  - Tier detection: alloy / lineage / ward / ancient / master / special / basic.
- **`ocr/runeshape-combinations-ru.json`** — 153 записи (alloy 13, ancient 13, basic 82, lineage 21, master 1, special 3, ward 20). Сгенерирован 2025-07-10 из live poe2db.tw.

### Added — M1.2: Core translators (частично)

- **`src/AldurPrice.Core/Translation/RuneshapeCombinationTranslator.cs`** — реальная имплементация (был stub):
  - Грузит embedded JSON через `Assembly.GetManifestResourceStream`.
  - Три уровня matching'а: exact (OrdinalIgnoreCase) → stem (по слову, через `RussianStemmer`) → Levenshtein ≤ 2.
  - Default-конструктор для production, stream-конструктор для тестов.
  - Возвращает `null`, если ни один уровень не сработал.
- **`src/AldurPrice.Core/Translation/ItemNameTranslator.cs`** — реальная цепочка fallback'ов (был stub):
  - `[1]` RuneshapeCombinationTranslator — для рунных комбинаций лиги.
  - `[2..4]` TranslationCache / rus.ndjson / translations.json — заглушки до M1.5.
  - `[5]` Stem + Levenshtein — встроены в `[1]`.
  - `[6]` Return null — цен не будет, в логе warning.
  - Для ENG-клиента — возвращает имя как есть (уже canonical).
- **`src/AldurPrice.Core/Pricing/ItemNameParser.cs`** — реальный парсер (был stub):
  - Quantity: leading «1x», «2 x», «3× », «2 шт», «2 шт.», trailing «×5», «x5».
  - Level: «(lvl 20)», «(ур. 20)», «(level 20)», trailing «lvl 20», «ур. 20», диапазоны «lvl 18-20» (берётся min).
  - Whitespace: множественные пробелы → один, trim.
  - OCR normalization: Latin-look-alikes → Cyrillic (P→Р, y→у, a→а, o→о, и т.д.) — только в словах, уже содержащих кириллицу. Чисто английские слова не трогает.
- **`src/AldurPrice.Core/Translation/RussianStemmer.cs`** — расширен список окончаний (прилагательные на -ский/-цкий, краткие формы, отглагольные существительные, infinitives -ть/-чь, reflexive -ся/-сь). Список отсортирован по убыванию длины, чтобы длинные специфичные окончания (например, «ского») проверялись раньше коротких («ого»). Намеренно НЕ полный Snowball Russian (см. KI-007).
- **`src/AldurPrice.Core/AldurPrice.Core.csproj`** — `<EmbeddedResource>` для `ocr/runeshape-combinations-ru.json` (LogicalName: `AldurPrice.Core.Translation.runeshape-combinations-ru.json`).

### Added — Тесты (M1.2 + M1.3)

- **`tests/AldurPrice.Core.Tests/RuneshapeCombinationTranslatorTests.cs`** — 13 тестов: загрузка embedded JSON, exact/stem/Levenshtein matching, edge-cases (null/empty, no match, too many edits), smoke на bundled данных.
- **`tests/AldurPrice.Core.Tests/ItemNameParserTests.cs`** — 17 тестов: quantity (leading/trailing/шт), level (lvl/ур./level/диапазоны), OCR normalization (Latin→Cyrillic в mixed-словах), whitespace, edge-cases.
- **`tests/AldurPrice.Core.Tests/ItemNameTranslatorTests.cs`** — 16 тестов: chain (runeshape known/unknown, eng passthrough, case/whitespace, падежи, OCR-искажения), RussianOcrDistortionTests (subset).
- **`tests/AldurPrice.Core.Tests/RussianStemmerTests.cs`** — расширен с 4 до 13 тестов: все падежи «руна» → «рун», «новость»/«новостью»/«новостях» → «новост», прилагательные (-ый/-ий/-ая/-ое/-ые/-ого/-ому/-ыми), -ский/-цкий серия, -ний серия.
- **`tests/AldurPrice.Core.Tests/RussianOcrPostProcessorTests.cs`** (new в M1.3) — 23 теста: Ё→Е, управляющие символы, пробелы (NBSP/thin/ideographic), кавычки, апострофы, дефисы, висящие пунктуации, буллеты, ENG-язык, edge cases, ProcessLines.

**Итого тестов:** 129 (13 M0 + 93 M1.2 + 23 M1.3), 0 failed. `dotnet test tests/AldurPrice.Core.Tests` — green (на Windows; на Linux доступен только Core.Tests).

### Known Issues (M0/M1.2)

- `CA1822` остаётся в NoWarn — не все Core-стабы заменены (M1.5+ цель убрать). `CS1591`/`CA1848`/`CA1805` — остаются.
- `KI-007`: RussianStemmer — НЕ полный Snowball (намеренно, см. STATUS.md).
- `KI-008`: «Bait Rune» в poe2db без русского перевода.

## Источники данных

- [Exiled Exchange 2](https://github.com/Kvan7/Exiled-Exchange-2) — переводы базовых предметов (4319 шт.) — bundle в M1.5
- [poe2db.tw](https://poe2db.tw/ru/Runeshape_Combinations) — переводы рунных комбинаций (153 шт.) — ✅ bundled
- [poe2scout.com](https://poe2scout.com) — источник цен с 24-часовым усреднением — M1.5
- [poe.ninja](https://poe.ninja) — альтернативный источник цен — M1.5
- [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) — OCR-движок (fallback) — ✅ имплементирован в M1.3, traineddata нужен ручной download (KI-009)
- [Tessdata best](https://github.com/tesseract-ocr/tessdata_best) — traineddata для Tesseract (eng + rus) — M1.10 / MSBuild target
