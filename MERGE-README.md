# AldurPrice M1.3 — OCR Engines Package

This archive contains the **delta files** for the M1.3 iteration of AldurPrice.

## How to apply

1. Extract this archive in your local `aldurprice-ru` repository root (overwrites existing files):
   ```powershell
   cd C:\path\to\aldurprice-ru
   tar -xzf aldurprice-m1.3-ocr-engines.tar.gz
   ```
   On Windows: use 7-Zip or `tar` (built into Windows 10 1809+).

2. Verify the structure: the archive contains files at their repo-relative paths,
   e.g. `src/AldurPrice.Ocr/OcrPipeline.cs` should land in `aldurprice-ru/src/AldurPrice.Ocr/OcrPipeline.cs`.

3. Build and test:
   ```powershell
   dotnet build AldurPrice.slnx -p:EnableWindowsTargeting=true
   dotnet test tests/AldurPrice.Core.Tests
   ```
   Expected: build OK with only CS1591 warnings; 129 tests pass.

4. (Optional) Download Tesseract traineddata for fallback OCR (see KI-009 in STATUS.md):
   ```powershell
   mkdir ocr\tesseract -Force
   Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata" -OutFile "ocr/tesseract/eng.traineddata"
   Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_best/raw/main/rus.traineddata" -OutFile "ocr/tesseract/rus.traineddata"
   ```

## What's new in M1.3

- **New files** (6):
  - `src/AldurPrice.Core/Translation/RussianOcrPostProcessor.cs` — text post-processing (Ё→Е, quotes, etc.)
  - `src/AldurPrice.Ocr/ImagePreprocessor.cs` — bitmap preprocessing (color filter + binarization)
  - `src/AldurPrice.Ocr/LeaguePanelDetector.cs` — panel-open detection (RGB heuristic)
  - `src/AldurPrice.Ocr/OcrPipeline.cs` — orchestrator (panel-detect → preprocess → OCR → postprocess)
  - `src/AldurPrice.Ocr/OcrPreprocessOptions.cs` — config records for preprocessing
  - `tests/AldurPrice.Core.Tests/RussianOcrPostProcessorTests.cs` — 23 tests

- **Modified files** (11):
  - `src/AldurPrice.Ocr/WindowsOcrEngine.cs` — real impl (was stub)
  - `src/AldurPrice.Ocr/TesseractEngine.cs` — real impl with lazy init + thread safety (was stub)
  - `src/AldurPrice.Ocr/AldurPrice.Ocr.csproj` — TFM changed to net9.0-windows10.0.19041.0, +System.Drawing.Common, +AllowUnsafeBlocks
  - `src/AldurPrice/App.xaml.cs` — DI registrations for new services
  - `Directory.Build.props` — updated NoWarn comment (CA1822 stays, see KI-001)
  - `Directory.Packages.props` — added System.Drawing.Common 9.0.0
  - `STATUS.md`, `CHANGELOG.md`, `docs/02-ARCHITECTURE.md`, `docs/04-RU-LOCALIZATION.md`, `docs/05-ROADMAP.md`

See CHANGELOG.md and STATUS.md for full details.

## Stopping point

See end of this README and STATUS.md → "Что в работе" / "Что дальше" for the next iteration plan.

**Made in M1.3:**
- M1.3 OCR engines (Windows + Tesseract) — real implementations
- ImagePreprocessor, LeaguePanelDetector, OcrPipeline
- RussianOcrPostProcessor with 23 tests
- Updated docs (AD-001, AD-002 resolved; AD-003, AD-004 added)
- KI-009..KI-012 added

**Not done (intentionally deferred):**
- Windows verification of M0 (run dotnet run on Windows, tag v0.1.0-alpha)
- OcrLeagueWindowReader, ResolutionProfiles — deferred to M1.10
- OcrTextPostProcessor (language-agnostic version) — RussianOcrPostProcessor covers base case
- MSBuild target EnsureTessData — deferred to M1.10 (manual download as workaround)
- OCR tests on net9.0-windows test project — deferred to M1.10
- Real screenshot fixtures — deferred to M1.10

**Next iteration:**
1. Windows verification: `dotnet build` + `dotnet test tests/AldurPrice.Core.Tests` (expect 129 pass)
2. M1.4 — Capture layer (PrintWindowCapture, Poe2WindowLocator)
3. Then M1.5 (pricing sources + rus.ndjson) — biggest unblocker for end-to-end
