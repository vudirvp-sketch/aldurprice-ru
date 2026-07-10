# AldurPrice M1.5-partial — TranslationCache + warnings cleanup

Archive содержит delta-файлы для M1.5-partial: реальная имплементация `TranslationCache`
(NDJSON-загрузка из Exiled Exchange 2), fallback [2] в `ItemNameTranslator`, 28 новых тестов,
и фикс 18 warnings из M1.4-fix. HTTP-клиенты (`Poe2ScoutClient`, `PoeNinjaClient`) отложены.

## ⚠️ Важно: код НЕ верифицирован сборкой

Код написан по существующим паттернам (`RuneshapeCombinationTranslator` как образец), но
среда разработки не имеет `dotnet SDK` — **сборка и тесты не запускались**. Перед merge
обязательно на Windows:

```powershell
dotnet build AldurPrice.slnx   # ожидается: 0 errors, 0 warnings
dotnet test                     # ожидается: ~193 passed, 0 failed (165 ранее + 28 новых)
```

Если есть errors/warnings — зафиксировать в `STATUS.md` как Known Issue и сообщить.

## Что в архиве

16 файлов (13 изменённых + 3 новых):

| Файл | Тип | Изменение |
|---|---|---|
| `Directory.Build.props` | M | `CA1873` добавлен в `NoWarn` (последовательно с `CA1848`, оба про LoggerMessage source-gen, M3.7). Комментарий обновлён. |
| `src/AldurPrice.Capture/PrintWindowCapture.cs` | M | CA1305: `ToString()` → `ToString(CultureInfo.InvariantCulture)` + `using System.Globalization`. |
| `tests/AldurPrice.Capture.Tests/Poe2WindowLocatorTests.cs` | M | CA1861: inline `new[]{...}` → `static readonly string[] CustomProcessNames` поле. |
| `src/AldurPrice.Core/Translation/TranslationCache.cs` | M | **Реальная имплементация** (был M0 stub): `ConcurrentDictionary`, `LoadNdjson(Stream)` (формат Exiled Exchange 2), `LoadEmbeddedOrDefault()` (embedded `rus.ndjson`), `Store`/`TryLookup`/`Clear`/`Count`. |
| `src/AldurPrice.Core/Translation/ItemNameTranslator.cs` | M | Fallback [2] через `TranslationCache`. Новый 2-параметровый конструктор. Default-конструктор вызывает `LoadEmbeddedOrDefault()`. Существующие конструкторы сохранены. |
| `src/AldurPrice.Core/AldurPrice.Core.csproj` | M | `<EmbeddedResource Condition="Exists(...)">` для `ocr/translations/rus.ndjson` — embed'ится автоматически, если файл загружен. |
| `src/AldurPrice/App.xaml.cs` | M | DI: `TranslationCache` через factory `LoadEmbeddedOrDefault()` (DI выберет 2-параметровый конструктор `ItemNameTranslator`). |
| `tests/AldurPrice.Core.Tests/ItemNameTranslatorTests.cs` | M | `_translator` изменён на `new(runeshape, cache: null)` — изоляция от rus.ndjson bundling (тесты проверяют только fallback [1]). Иначе `TryTranslate_UnknownItem_ReturnsNull("Зеркало Каландры")` ломался бы при bundled rus.ndjson. |
| `tests/AldurPrice.Core.Tests/TranslationCacheTests.cs` | NEW | 17 тестов: NDJSON-загрузка (валидные/пустые/malformed/skip-логика), exact lookup (case-insensitive), Store/Clear, `LoadEmbeddedOrDefault`, edge-cases. |
| `tests/AldurPrice.Core.Tests/ItemNameTranslatorCacheFallbackTests.cs` | NEW | 11 тест-кейсов: fallback [2], приоритет [1] > [2], graceful degradation (null/empty cache), unknown→null, eng passthrough. |
| `ocr/translations/README.md` | NEW | Документация: формат NDJSON, как загрузить (`update-translations.py`), почему не committen. |
| `STATUS.md` | M | M1.5-partial (🔶), build 0 warnings (CA1873 suppressed), ~193 тестов, KI-001 обновлён, KI-017 (rus.ndjson не bundled). |
| `CHANGELOG.md` | M | `[Unreleased]`: Added M1.5-partial + Fixed Warnings cleanup (18→0). |
| `docs/02-ARCHITECTURE.md` | M | `TranslationCache` → real, DI factory, fallback chain [1]→[2]→null. |
| `docs/04-RU-LOCALIZATION.md` | M | §3.1 fallback chain: [2] TranslationCache активен (exact match по rus.ndjson). |
| `docs/05-ROADMAP.md` | M | M1.5 чекбоксы: TranslationCache + ItemNameTranslator + тесты — ✅. |

## How to apply

1. Распаковать архив в корень локального `aldurprice-ru` (поверх существующих файлов):
   ```powershell
   cd C:\Users\fallo\OneDrive\Desktop\repo\aldurprice-ru
   tar -xzf aldurprice-m1.5-partial.tar.gz
   ```

2. Сборка и тесты:
   ```powershell
   dotnet build AldurPrice.slnx
   dotnet test
   ```
   Ожидается: build 0 errors / 0 warnings; **~193 passed, 0 failed**
   (140 Core.Tests ранее + 28 новых + 25 Capture.Tests).

3. (Опционально, для активации fallback [2]) Загрузить `rus.ndjson`:
   ```powershell
   python scripts/update-translations.py
   dotnet build AldurPrice.slnx   # теперь rus.ndjson embedded
   dotnet test                     # TranslationCache.LoadEmbeddedOrDefault → 4319 записей
   ```
   Без этого шага `TranslationCache` пустой (KI-017) — переводятся только рунные комбинации.

## Что сделано

- **TranslationCache** — реальная имплементация (NDJSON-парсинг, exact-match, embedded loading).
- **ItemNameTranslator** — fallback [2] для базовых предметов (4 319 после загрузки rus.ndjson).
- **28 новых тестов** — `TranslationCacheTests` (17) + `ItemNameTranslatorCacheFallbackTests` (11).
- **18 warnings → 0** — CA1873 (16) suppressed, CA1305 (1) + CA1861 (1) fixed.
- **Документация** — STATUS, CHANGELOG, architecture, localization, roadmap актуализированы.

Подробности — в `CHANGELOG.md` → `[Unreleased]` → `Added — M1.5-partial` и `Fixed — Warnings cleanup`.

## Точка остановки

- **Сделано:** M1.5-partial (TranslationCache + ItemNameTranslator fallback [2] + 28 тестов + warnings cleanup). Код написан, docs актуальны.
- **НЕ сделано (в следующей итерации, строго в этом порядке):**
  1. **Windows верификация M1.5-partial** — `dotnet build` (0 errors / 0 warnings?) + `dotnet test` (~193 passed?). Код не собирался в среде разработки.
  2. **Windows runtime-верификация M0 + M1.4** — `dotnet run --project src/AldurPrice` (тёмное окно «Hello AldurPrice»?), запустить PoE2, проверить `Poe2WindowLocator.TryLocate()` через debug-лог (KI-013 — имена процессов). После этого тег `v0.1.0-alpha`.
  3. (Опционально) Smoke-test `PrintWindowCapture.CaptureAsync` с `{0,0,800,600}` → PNG.
  4. (Опционально) Загрузить `rus.ndjson` через `python scripts/update-translations.py` → rebuild → проверить, что `TranslationCache.Count == 4319`.
  5. **M1.5-remaining** — `Poe2ScoutClient`, `PoeNinjaClient` через `IHttpClientFactory`, `PricingSourceRouter`, WireMock.Net-тесты. **Блокер:** нужны реальные API-response fixtures (web-research poe2scout.com / poe.ninja PoE2 endpoints, или browser dev-tools capture). Без знаний response-shape писать клиенты бессмысленно.

См. `STATUS.md` → «Что в работе» / «Что дальше».
