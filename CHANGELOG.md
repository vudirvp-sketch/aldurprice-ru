# Changelog

Все заметные изменения проекта AldurPrice документируются здесь.
Формат — [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версионирование — [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]

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

### Added — Тесты (M1.2)

- **`tests/AldurPrice.Core.Tests/RuneshapeCombinationTranslatorTests.cs`** — 13 тестов: загрузка embedded JSON, exact/stem/Levenshtein matching, edge-cases (null/empty, no match, too many edits), smoke на bundled данных.
- **`tests/AldurPrice.Core.Tests/ItemNameParserTests.cs`** — 17 тестов: quantity (leading/trailing/шт), level (lvl/ур./level/диапазоны), OCR normalization (Latin→Cyrillic в mixed-словах), whitespace, edge-cases.
- **`tests/AldurPrice.Core.Tests/ItemNameTranslatorTests.cs`** — 16 тестов: chain (runeshape known/unknown, eng passthrough, case/whitespace, падежи, OCR-искажения), RussianOcrDistortionTests (subset).
- **`tests/AldurPrice.Core.Tests/RussianStemmerTests.cs`** — расширен с 4 до 13 тестов: все падежи «руна» → «рун», «новость»/«новостью»/«новостях» → «новост», прилагательные (-ый/-ий/-ая/-ое/-ые/-ого/-ому/-ыми), -ский/-цкий серия, -ний серия.

**Итого тестов:** 106 (было 13), 0 failed. `dotnet test tests/AldurPrice.Core.Tests` — green.

### Changed

- `STATUS.md` — актуализирован под M1.1+M1.2 (partial). Добавлены KI-007 (RussianStemmer не Snowball), KI-008 (Bait Rune без перевода). KI-005 помечен как решён. AD-002 (embedded resource) — новый architecture deviation.
- `docs/04-RU-LOCALIZATION.md` — исправлены примеры имён: реальные имена poe2db — «Fire Rune», не «Rune of Fire». Уточнён объём: ~150 записей (было «80-120»).

### Known Issues

Подробности — в [STATUS.md](STATUS.md#known-issues):
- `CA1822` убирается в M1.3 (стабы заменены, анализатор больше не нужен).
- `CS1591`/`CA1848`/`CA1805` — остаются до M3.7/source-gen Options.
- `KI-007`: RussianStemmer — НЕ полный Snowball (намеренно, см. STATUS.md).
- `KI-008`: «Bait Rune» в poe2db без русского перевода.

## [0.1.0-alpha] — M0 Skeleton

### Added
- **Каркас решения `AldurPrice.slnx`** с 6 проектами: `AldurPrice.Core` (net9.0), `AldurPrice.Data` (net9.0), `AldurPrice.Ocr` (net9.0-windows), `AldurPrice.Capture` (net9.0-windows), `AldurPrice` (net9.0-windows, WPF), `AldurPrice.Core.Tests` (net9.0).
- **`Directory.Build.props`** + **`Directory.Packages.props`** — central package management.
- **`STATUS.md`** + **`CHANGELOG.md`** — tracking-документы.
- 13 smoke-тестов (RussianStemmerTests + LevenshteinTests).

### Known Issues
- `CA1822`/`CS1591`/`CA1848`/`CA1805` подавлены — см. STATUS.md → KI-001.
- `.slnx` формат не верифицирован на Visual Studio 2022 — KI-002.
- WPF-проекты не собираются на Linux без `EnableWindowsTargeting=true` — KI-003 (дизайн-решение).

## Источники данных

- [Exiled Exchange 2](https://github.com/Kvan7/Exiled-Exchange-2) — переводы базовых предметов (4319 шт.) — bundle в M1.5
- [poe2db.tw](https://poe2db.tw/ru/Runeshape_Combinations) — переводы рунных комбинаций (153 шт.) — ✅ bundled
- [poe2scout.com](https://poe2scout.com) — источник цен с 24-часовым усреднением — M1.5
- [poe.ninja](https://poe.ninja) — альтернативный источник цен — M1.5
- [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) — OCR-движок (fallback) — M1.3
