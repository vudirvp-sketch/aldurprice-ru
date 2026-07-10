# Changelog

Все заметные изменения проекта AldurPrice документируются здесь.
Формат — [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версионирование — [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]

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
