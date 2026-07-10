# AldurPrice M1.4-fix — NU1201 + скрытые баги M1.3

Archive содержит delta-файлы для починки сборки после M1.4 commit-а.

## Что в архиве

6 изменённых файлов (новых нет):

| Файл | Изменение |
|---|---|
| `src/AldurPrice/AldurPrice.csproj` | TFM `net9.0-windows` → `net9.0-windows10.0.19041.0` + `SupportedOSPlatformVersion`. Fix NU1201. |
| `src/AldurPrice.Ocr/TesseractEngine.cs` | `Lazy<EngineContainer>.Value` unwrap + `rect.Y` → `rect.Y1`. Fix CS0185/CS1061. |
| `src/AldurPrice.Core/Translation/RussianOcrPostProcessor.cs` | `TrimStrayPunctuation` тримит whitespace + `CollapseWhitespace` схлопывает пробел после `\n`. Fix 3 failing теста. |
| `Directory.Build.props` | Комментарий TFM-карты обновлён. |
| `STATUS.md` | KI-014/015/016 (RESOLVED), KI-003 дополнен, build/test counts обновлены (165 total). |
| `CHANGELOG.md` | Fixed-секция под `[Unreleased]`. |

## How to apply

1. Распаковать архив в корень локального `aldurprice-ru` (поверх существующих файлов):
   ```powershell
   cd C:\Users\fallo\OneDrive\Desktop\repo\aldurprice-ru
   tar -xzf aldurprice-m1.4-fix.tar.gz
   ```
   На Windows 10 1809+ `tar` встроен. Альтернатива — 7-Zip.

2. Проверить структуру: архив хранит пути относительно корня репо (например `src/AldurPrice/AldurPrice.csproj`).

3. Сборка и тесты:
   ```powershell
   dotnet build AldurPrice.slnx
   dotnet test
   ```
   Ожидается: build 0 errors / 0 warnings; **165 passed, 0 failed** (140 Core.Tests + 25 Capture.Tests).

4. Если на Windows `dotnet test` покажет меньше 165 — проверить, что нет дополнительных failing тестов сверх перечисленных в KI-014/015/016. На Linux 1 тест `TryLocate_SecondCall_WhenCachedHwndBecomesInvalid_Rescans` падает с `DllNotFoundException: user32.dll` — это ожидаемо (P/Invoke Windows-only).

## Что починено

- **NU1201** при restore: главный проект не мог ссылаться на `AldurPrice.Ocr` из-за TFM-несовместимости. См. KI-014.
- **3 compile-error'а в TesseractEngine.cs** (CS0185 + 2× CS1061): код M1.3 никогда не компилировался — restore падал раньше. См. KI-015.
- **3 failing теста в RussianOcrPostProcessorTests**: тесты M1.3 никогда не запускались по той же причине. См. KI-016.

Подробности — в `CHANGELOG.md` → `[Unreleased]` → `Fixed — M1.4-fix` и в `STATUS.md` → `Known Issues` → KI-014/015/016.

## Точка остановки

- **Сделано:** NU1201 + 5 скрытых багов починены. Build 0 errors / 0 warnings. Unit-тесты 164/165 на Linux (1 ожидаемо падает на P/Invoke).
- **Не сделано (в следующей итерации):**
  1. **Windows runtime-верификация M0** — `dotnet run --project src/AldurPrice` → проверить тёмное окно «Hello AldurPrice» → tag `v0.1.0-alpha`.
  2. **Windows runtime-верификация M1.4** — запустить PoE2, проверить `Poe2WindowLocator.TryLocate()` через debug-лог. Если имя процесса отличается — обновить `DefaultProcessNames` (см. KI-013).
  3. (Опционально) Smoke-test `PrintWindowCapture.CaptureAsync` с регионом `{0,0,800,600}` — сохранить PNG, проверить, что не чёрный прямоугольник.
  4. **M1.5** — `Poe2ScoutClient`, `PoeNinjaClient` через `IHttpClientFactory`, WireMock.Net-тесты, загрузка `rus.ndjson` из Exiled Exchange 2 (4319 предметов) → `TranslationCache`.

См. `STATUS.md` → «Что в работе» / «Что дальше».
