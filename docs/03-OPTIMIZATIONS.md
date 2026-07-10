# Оптимизации AldurPrice

> Документ описывает ключевые технические оптимизации, заложенные в архитектуру с самого начала (а не "добавленные потом"). Каждая оптимизация имеет измеримую метрику и тест, доказывающий, что она работает.

## 1. Производительность

### 1.1. Frame-diffing через perceptual hash

**Проблема.** При фиксированном интервале сканирования 100 мс (10 Гц) OCR выполняется 10 раз в секунду, даже если битмап региона идентичен предыдущему (игрок не двигается, не скроллит). Это пустая трата CPU — типичный игровой сценарий "открыл панель, читаю" даёт 90%+ idle-кадров.

**Решение.** `FrameDiffer` вычисляет быстрый perceptual hash (pHash 8×8 = 64 бита) захваченного битмапа и сравнивает с предыдущим. Если хэш совпадает с tolerancy ≤2 бит (учёт минимального шума сжатия), OCR пропускается, оверлей остаётся с предыдущими ценами.

**Алгоритм pHash 8×8:**

1. Downscale битмапа до 32×32 (быстро, ~0.1 мс через `Graphics.DrawImage` с interpolation)
2. Преобразование в grayscale (0.299·R + 0.587·G + 0.114·B)
3. DCT (Discrete Cosine Transform) 32×32 → берём top-left блок 8×8 (low frequencies)
4. Вычисление median из 64 значений (исключая [0,0] — DC component)
5. Threshold: каждый бит = 1 если значение > median, иначе 0
6. Сравнение с предыдущим хэшем через Hamming distance

**Сложность:** O(N²) для DCT 32×32 = 1024 операции, ~0.3 мс на i5-12400. Пренебрежимо по сравнению с OCR (20–80 мс).

**Реализация:** `AldurPrice.Capture/FrameDiffer.cs`, ~120 строк, без внешних зависимостей (DCT реализован вручную, не через FFT — для 32×32 FFT даёт overhead больше чем gain).

**Измерение.** Benchmark `scripts/benchmark-ocr.py`: количество OCR-вызовов за 5 минут игрового сценария с периодами idle. Цель: 60–80% reduction (т.е. 2–4× меньше OCR-вызовов).

**Тесты.** `FrameDifferTests`:
- `IdenticalFrames_AreSkipped` — два одинаковых битмапа → `HasChanged` returns false
- `MinorNoise_Tolerated` — добавление 1% шума → `HasChanged` still false (tolerance 2 bits)
- `RealChange_Detected` — изменение одной строки текста → `HasChanged` returns true
- `PanelCloseOpen_Detected` — резкое изменение яркости → `HasChanged` returns true

**Риск.** pHash может пропустить мелкое изменение текста (например, обновление одной цифры количества). Mitigation: tolerance ≤2 бита консервативна, плюс детектор панели (`LeaguePanelDetector`) всё равно выполняется всегда — если панель закрылась/открылась, OCR запустится.

### 1.2. Адаптивный интервал сканирования

**Проблема.** Фиксированный интервал 100 мс даёт одинаковую нагрузку независимо от того, активно игрок двигается (быстрый отклик важен) или панель висит idle (отклик не важен, можно реже).

**Решение.** `AdaptiveScanIntervalController` — конечный автомат с 4 состояниями:

| Состояние | Условие | Интервал | Цель |
|---|---|---|---|
| **Aggressive** | CPU системы <20%, панель активна (изменилась за последние 2 сек) | 50 мс (20 Гц) | Максимальная отзывчивость при низкой нагрузке |
| **Normal** | CPU 20–50%, панель активна | 100 мс (10 Гц) | Базовый режим, как default |
| **Throttled** | CPU >50% | 150–200 мс (5–7 Гц) | Снижение нагрузки на слабых ПК |
| **Idle** | Панель не менялась 3 секунды | 500 мс (2 Гц) | Минимальная нагрузка, мгновенный возврат к Aggressive при изменении |

Переходы:
- Idle → Aggressive: мгновенно при первом изменённом кадре (не ждём 500 мс)
- Aggressive → Idle: через 3 секунды без изменений
- Normal ↔ Throttled: при переходе CPU через порог 50% (с гистерезисом ±5%)

**Измерение.** Benchmark: средний CPU% за 5 минут игрового сценария. Цель: ≤8% CPU на i5-12400 (vs 10–15% при фиксированном 100 мс).

**Тесты.** `AdaptiveScanIntervalTests`:
- `LowCpu_ActivePanel_AggressiveInterval`
- `HighCpu_ThrottledInterval`
- `IdlePanel_ReturnsToAggressiveOnFirstChange`
- `Hysteresis_PreventsFlapping`

### 1.3. Pause-when-unfocused

**Проблема.** Когда пользователь Alt-Tab'нул в браузер, продолжать сканировать экран бесполезно — игрок не видит оверлей.

**Решение.** `Poe2WindowMonitor` через `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` отслеживает foreground-окно. Если foreground ≠ PoE2:
- OCR-цикл ставится на паузу
- Оверлей скрывается (не висит поверх браузера)
- CPU экономится полностью

При возврате в игру — мгновенный resume, оверлей появляется.

**Измерение.** CPU% в фоне должен быть ~0% (только timer wait). RAM не растёт.

**Тесты.** Имитация Alt-Tab через mock `Poe2WindowMonitor`:
- `PoE2Foreground_PipelineRuns`
- `BrowserForeground_PipelinePaused_OverlayHidden`
- `ReturnToPoE2_PipelineResumes`

**Риск.** Если игрок использует Lossless Scaling или оконный режим с overlay-инструментами, foreground может переключаться. Mitigation: настройка `App.PauseWhenUnfocused` (default true), пользователь может отключить.

### 1.4. SQLite persistence кэша цен

**Проблема.** In-memory кэш цен теряется при перезапуске. Первый запуск = 2–5 секунд без цен, пока 5 HTTP-запросов к poe2scout не завершатся. Если API недоступен при запуске — цены отсутствуют до следующего 15-минутного цикла.

**Решение.** `SqlitePricingCache` поверх `Microsoft.Data.Sqlite`:

```sql
-- Schema (migration 001_init.sql)
CREATE TABLE prices (
  key TEXT PRIMARY KEY,
  chaos_value REAL NOT NULL,
  divine_value REAL,
  exalt_value REAL,
  quantity INTEGER DEFAULT 1,
  league TEXT NOT NULL,
  updated_at INTEGER NOT NULL  -- Unix timestamp
);

CREATE TABLE unique_ranges (
  category TEXT NOT NULL,
  league TEXT NOT NULL,
  min_value REAL NOT NULL,
  max_value REAL NOT NULL,
  updated_at INTEGER NOT NULL,
  PRIMARY KEY (category, league)
);

CREATE INDEX idx_prices_league ON prices(league);
CREATE INDEX idx_prices_updated ON prices(updated_at);
```

**Особенности:**

- **WAL-mode** (`PRAGMA journal_mode=WAL`) — параллельное чтение во время записи
- **UPSERT** (`INSERT ... ON CONFLICT(key) DO UPDATE SET ...`) — атомарная запись
- **Batch-запись** — все цены одного snapshot'а в одной транзакции (~50 мс на 1000 записей)
- **Stale fallback** — если API недоступен, кэш используется с пометкой "stale" (TTL 60 мин, настраивается). После TTL — цены скрываются (лучше ничего, чем устаревшие)
- **Compact** — `VACUUM` при shutdown если размер >50 МБ

**Измерение.** Время старта до первого отображения цен. Цель: <200 мс (vs 2–5 сек без persistence).

**Тесты.** `SqlitePricingCacheTests`:
- `CRUD_AddGetUpdateDelete`
- `TTL_ExpiredEntries_ReturnNull`
- `Concurrency_ParallelReadsAndSingleWrite`
- `Restart_PricesPersisted`
- `StaleFallback_ReturnsOldValues_WhenApiUnavailable`
- `Vacuum_ReducesFileSize_WhenManyDeletes`

### 1.5. Оптимизация размера сборки

**Цель:** ~120 МБ (vs типичные 180+ МБ для self-contained WPF + OCR).

**Методы:**

1. **Tesseract traineddata** — только `eng` + `rus` bundled (~85 МБ). Остальные 6 языков (deu, fra, spa, por, kor, chi_tra) — opt-in download через Settings ("Скачать языковой пакет"). Реализация: `LanguagePackDownloader` с прогресс-баром.
2. **Trim mode `partial`** — безопасный trim без reflection-нарушений. WPF/WinForms assemblies не триммируются (есть reflection), но System.Text.Json, Microsoft.Extensions, our Core — триммятся.
3. **`ReadyToRun`** — precompiled IL → native, ускорение JIT на 30%, увеличение размера на ~10%. Net benefit для cold start.
4. **`EnableCompressionInSingleFile=true`** — сжатие bundle, уменьшает размер на ~30%.
5. **`SatelliteResourceLanguages=en`** — только английские ресурсы .NET runtime (не тащим локализации ASP.NET и т.д.)

**Измерение.** Размер `.exe` и время старта. Цель: ~120 МБ, старт <1.5 сек.

## 2. Качество русского перевода

### 2.1. Маппинг рунных комбинаций (ремнантов)

**Проблема.** В `rus.ndjson` из Exiled Exchange 2 нет рунных комбинаций лиги «Runes of Aldur», потому что это динамический контент лиги, не базовый тип предмета. OCR распознаёт «Руна огня», но переводчик не знает, что это «Rune of Fire», и возвращает как-is → цен нет.

**Решение.** Скрипт `scripts/parse-poe2db-runeshapes.py` парсит [poe2db.tw/ru/Runeshape_Combinations](https://poe2db.tw/ru/Runeshape_Combinations), извлекает маппинг English↔Russian для всех рунных комбинаций (80–120 записей), сохраняет в `ocr/runeshape-combinations-ru.json`:

```json
{
  "version": 1,
  "source": "https://poe2db.tw/ru/Runeshape_Combinations",
  "fetched_at": "2025-07-10T12:00:00Z",
  "combinations": [
    { "en": "Rune of Fire", "ru": "Руна огня", "tier": "basic" },
    { "en": "Rune of Cold", "ru": "Руна холода", "tier": "basic" },
    { "en": "Runefather's Alloy", "ru": "Сплав Рунного отца", "tier": "alloy" },
    { "en": "Farrul's Chase Rune", "ru": "Руна погони Фаррул", "tier": "lineage" },
    { "en": "Ancient Witchcraft Rune", "ru": "Древняя руна ведьмовства", "tier": "ancient" }
  ]
}
```

`RuneshapeCombinationTranslator` загружает этот файл в `Dictionary<string, string>` (RU→EN, case-insensitive) и используется как **первый** fallback в `ItemNameTranslator.ToEnglish()` — перед `rus.ndjson`.

**Измерение.** Тест `RuneshapeCombinationTranslatorTests` — все 100+ записей должны переводиться. Integration-тест: OCR-симулятор с битмапом «Руна огня» → цена находится. Покрытие: 100% рунных комбинаций из poe2db.tw.

### 2.2. Падежи и OCR-искажения кириллицы

**Проблема.** Русский язык склоняется: «Руна огня» (Nominative) vs «Руну огня» (Accusative, встречается в UI «Выберите Руну огня»). OCR может выдать «Руну» вместо «Руна» — переводчик не найдёт точного совпадения. Также OCR часто путает похожие кириллические буквы: Ш↔Щ, И↔Й, Д↔Л, Ы↔И.

**Решение.** Двухуровневый fallback в `RuneshapeCombinationTranslator`:

1. **Точное совпадение** — `Dictionary<string, string>` с `StringComparer.OrdinalIgnoreCase`
2. **Stem-matching** — нормализация окончаний (-а, -я, -у, -ю, -ой, -ею → базовая форма). Реализация через `RussianStemmer` (портированный Snowball stemmer, public domain)
3. **Levenshtein distance ≤2** — fuzzy matching для OCR-искажений (Ш↔Щ, и т.д.)

**Пример работы:**

| Вход OCR | Шаг 1 (точный) | Шаг 2 (stem) | Шаг 3 (Levenshtein) | Результат |
|---|---|---|---|---|
| «Руна огня» | ✅ найдено | — | — | Rune of Fire |
| «Руну огня» (падеж) | ❌ | ✅ stem=«РУН ОГН» | — | Rune of Fire |
| «Руна огне» (OCR typo) | ❌ | ❌ | ✅ dist=1 | Rune of Fire |
| «Рунаагня» (OCR merge) | ❌ | ❌ | ✅ dist=2 | Rune of Fire |

**Измерение.** Тест `RussianOcrDistortionTests` — 50+ вариантов искажённых названий рун должны переводиться.

**Риск.** Stemmer может дать ложные срабатывания (разные руны с одним корнем). Mitigation: stem-matching используется только если точное совпадение не найдено, и результат валидируется через существование цены в кэше (если цены нет — не показываем, лучше ничего, чем неправильная цена).

### 2.3. Полная локализация UI

**Проблема.** Захардкоженные английские строки в code-behind — плохой UX для RU-пользователей.

**Решение.**

- `Resources/Strings.resx` (en, нейтральная культура) + `Resources/Strings.ru.resx` (ru)
- `IStringLocalizer<App>` в DI через `Microsoft.Extensions.Localization`
- В XAML — markup extension `{i18n:StringLocalizer Key=Dashboard_Title}`
- В code-behind — `_localizer["Dashboard_Title"]`
- Культура определяется `App.Language` (default `auto` = из культуры ОС, override в Settings)
- Все сообщения об ошибках, логи для пользователя, tooltips, кнопки, лейблы — в .resx

Оценка: ~300 строк для перевода в Dashboard + Settings + Overlay + Changelog + Crash dialog.

**Измерение.** Тест `LocalizationTests` — все ключи из `Strings.resx` присутствуют в `Strings.ru.resx`, нет пустых значений. Визуальная проверка: скриншоты UI в обоих языках.

**Риск.** Русский текст длиннее английского на 15–30% — может ломать layout. Mitigation: WPF с `TextBlock.TextWrapping="Wrap"` и `Grid` с auto-sizing, ручная проверка всех диалогов.

### 2.4. Автоопределение языка клиента PoE2

**Проблема.** Пользователь ставит RU-клиент, но забывает переключить `OCR.Language` в Settings. OCR пытается распознать кириллицу английским traineddata → garbage output.

**Решение.** `Poe2ConfigFile.cs`:

- Чтение `%USERPROFILE%\Documents\My Games\Path of Exile 2\game.ini`
- Парсинг `language = "russian"` / `"english"` / `"german"` / ...
- Автоустановка `OcrOptions.Language` при первом запуске и при обнаружении смены языка клиента
- Уведомление в Dashboard: «Обнаружен русский клиент PoE2 — язык OCR переключён на русский»
- Override доступен в Settings с предупреждением

**Измерение.** Тест `Poe2ConfigFileTests` — парсинг реальных game.ini из тестовых фикстур. Integration-тест: имитация смены языка клиента → автообновление OCR.Language.

## 3. Стабильность OCR

### 3.1. Улучшенный постпроцессинг кириллицы

**Проблема.** Стандартный OCR-постпроцессор удаляет "мусорные" символы через regex, но кириллица имеет специфичные проблемы: «Ё» часто распознаётся как «Е», «Й» как «И» + апостроф, твёрдый знак «Ъ» как мягкий «Ь».

**Решение.** `RussianOcrPostProcessor`:

- Нормализация «Ё»→«Е» (опционально, по конфигу `Ocr.NormalizeYoo`)
- Удаление висячих апострофов после согласных («И'» → «И», но «Д'Артаньян» сохраняется)
- Замена «Ь»→«Ъ» в известных словах (через lookup)
- Слияние разбитых строк: если строка заканчивается согласной, а следующая начинается с гласной — это одно слово (OCR разбил по ошибке)

**Измерение.** Тест `RussianOcrPostProcessorTests` — 30+ edge-case'ов из реальных OCR-выводов.

### 3.2. Graceful degradation

**Проблема.** Если Tesseract native DLL не загрузилась (антивирус, permissions), приложение падает. Если Windows OCR недоступен (Windows 7), fallback на Tesseract может тоже не работать.

**Решение.** `OcrEngineResolver`:

- При старте проверяет доступность обоих движков
- Если оба недоступны — показывает dialog с инструкцией и предлагает запустить без OCR (только кэш цен, без оверлея)
- Если один упал в runtime — переключается на другой с warning в лог
- Логирует версию движка (`Windows.Media.Ocr` version, Tesseract 5.2.0) для bug reports

### 3.3. Better debug overlay

**Возможности:**

- Красная рамка региона захвата
- Зелёные рамки вокруг распознанных строк с Y-координатами
- Жёлтая рамка вокруг детектированной панели
- Текст: распознанное имя → перевод → цена (для каждой строки)
- Отдельное окно «OCR Pipeline Inspector» с пошаговым препроцессингом (original → grayscale → binarized → color-filtered)
- Переключение через Settings → Debug → «Show OCR Inspector»

## 4. Архитектурные оптимизации

### 4.1. Разбиение на проекты (см. [02-ARCHITECTURE.md](02-ARCHITECTURE.md#1-структура-решения))

6 проектов: `Core` (no UI), `Data` (persistence), `Ocr`, `Capture`, основной `AldurPrice` (WPF), `Tests`. Central package management через `Directory.Packages.props`.

### 4.2. JSON-схема конфига и валидация

`config/appsettings.schema.json` — JSON Schema draft-07, описывает все поля и ограничения. Подхватывается VS Code / Rider для автокомплита в редакторе. `IValidateOptions<T>` для каждого блока. При невалидном конфиге — warning в лог + fallback на дефолт.

### 4.3. Central Package Management

`Directory.Packages.props` с central package management — все версии NuGet-пакетов в одном файле, нельзя получить version mismatch между проектами.

### 4.4. Логирование через Serilog

Sinks:

- `File` (rolling daily, 7-day retention) → `logs/app-YYYYMMDD.txt`
- `Debug` (для VS Debug output)
- `Memory` (кольцевой буфер 1000 записей для Dashboard log panel)

Structured logging: `Log.Information("Translated {RuName} to {EnName} via {Source}", ru, en, "runeshape-combinations")` — позволяет фильтровать логи по полям.

### 4.5. Single-instance guard

`SingleInstanceGuard` через `Mutex(initiallyOwned: true, name: "Global\\AldurPrice_SingleInstance", ...)`. При попытке второго запуска — activate существующее окно и exit. Решает проблему случайного двойного запуска (особенно при автообновлении).

## 5. Метрики успеха

По завершении всех работ (Milestone 3), целевые метрики:

| Метрика | Цель | Измерение |
|---|---|---|
| CPU% при активной панели | ≤8% | Benchmark на i5-12400, 1080p |
| CPU% в фоне (Alt-Tab) | ~0% | Тот же benchmark |
| Время старта до цен | <200 мс | Stopwatch от запуска до первого overlay |
| Размер .exe | ~120 МБ | File size |
| Распознавание RU рун | 100% | Тест на 100+ комбинациях |
| UI-локализация RU | 100% | Все строки в .resx |
| Тестовое покрытие | 80%+ (с UI) | `dotnet test --coverage` |
| Память (RAM) | ≤180 МБ | Process Explorer на 5 мин |
| Crashes за 1 час стресс-теста | 0 | Stochastic UI тесты |

Benchmark-скрипт `scripts/benchmark-ocr.py` автоматизирует измерения и сохраняет результаты в `benchmarks/results-{version}.json` для сравнения между версиями.
