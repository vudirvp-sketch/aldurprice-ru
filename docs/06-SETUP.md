# Настройка окружения разработки

> Пошаговая инструкция для разработчиков: установка инструментов, клонирование, сборка, тестирование, отладка. Рассчитана на Windows 10/11 (основная платформа) с заметками для разработки на Linux/macOS (ограниченная — WPF Windows-only).

## 1. Требования к системе

### 1.1. Обязательные инструменты

| Инструмент | Версия | Назначение | Установка |
|---|---|---|---|
| **.NET SDK** | 9.0.x | Компиляция C#, WPF, тесты | [dot.net/download](https://dot.net/download) |
| **Git** | 2.40+ | Версионирование | [git-scm.com](https://git-scm.com) |
| **Visual Studio 2022** | 17.10+ | IDE (рекомендуется) или **Rider** | [visualstudio.com](https://visualstudio.com) |
| **Python** | 3.11+ | Скрипты парсинга переводов | [python.org](https://python.org) |

### 1.2. Компоненты Visual Studio

При установке VS 2022 отметить workload'ы:

- **.NET desktop development** — WPF, WinForms (для совместимости при отладке native interop)
- **.NET SDK 9.0** — отдельный компонент в Individual components

### 1.3. Для тестирования с игрой (опционально)

- **Path of Exile 2** с русским языковым пакетом
- **Windows 10 build 1809+** для Windows OCR
- **PyE2 beta-доступ** (если лига «Runes of Aldur» ещё в beta)

## 2. Клонирование и первый запуск

### 2.1. Клонирование

```powershell
# Через HTTPS
git clone https://github.com/<your-username>/aldurprice.git
cd aldurprice

# Или через SSH (рекомендуется для контрибьюторов)
git clone git@github.com:<your-username>/aldurprice.git
```

### 2.2. Восстановление зависимостей

```powershell
dotnet restore
```

Это скачает NuGet-пакеты согласно `Directory.Packages.props` (central package management). Первый запуск может занять 1–2 минуты.

### 2.3. Первая сборка

```powershell
# Debug build
dotnet build -c Debug

# Запуск
dotnet run --project src/AldurPrice -c Debug
```

При первом запуске:

1. MSBuild target `EnsureTessData` скачает `eng.traineddata` (~40 МБ) и `rus.traineddata` (~45 МБ) в `ocr/tesseract/`
2. MSBuild target `UpdateTranslations` (если не CI) запустит `scripts/update-translations.py` для подтягивания свежих `*.ndjson` из Exiled Exchange 2 (нужен интернет)
3. Создастся `config/appsettings.json` с дефолтными настройками (RU по умолчанию)
4. Приложение запустится — появится окно Dashboard

### 2.4. Hot-reload разработка

Для UI-изменений (WPF XAML) удобно использовать hot-reload:

```powershell
dotnet watch --project src/AldurPrice run
```

Изменения в XAML и code-behind применяются без перезапуска. Изменения в `Core/` проекте требуют перекомпиляции (hot-reload перезапускает приложение автоматически).

## 3. Структура решения

```
AldurPrice.slnx
├── src/
│   ├── AldurPrice/                  # Основное WPF-приложение
│   │   ├── App.xaml(.cs)            # Точка входа, DI-хост
│   │   ├── Configuration/           # Опции, SettingsController
│   │   ├── Contracts/               # Интерфейсы
│   │   ├── Capture/                 # Захват экрана
│   │   ├── OCR/                     # OCR-пайплайн (UI-зависимая часть)
│   │   ├── Pricing/                 # UI-зависимая часть pricing
│   │   ├── Overlay/                 # WPF-оверлей
│   │   ├── Dashboard/               # WPF-дашборд
│   │   ├── Localization/            # .resx, StringLocalizer
│   │   ├── Startup/                 # Crash logger, update checker
│   │   ├── App/                     # Фоновые воркеры
│   │   └── Resources/               # Иконки, темы, шрифты
│   │
│   ├── AldurPrice.Core/             # Чистая логика (no UI, no I/O)
│   ├── AldurPrice.Data/             # SQLite persistence
│   ├── AldurPrice.Ocr/              # OCR-движки
│   └── AldurPrice.Capture/          # Захват экрана
│
├── tests/
│   ├── AldurPrice.Core.Tests/
│   ├── AldurPrice.Ocr.Tests/
│   ├── AldurPrice.Data.Tests/
│   ├── AldurPrice.UI.Tests/
│   └── AldurPrice.Integration/
│
├── ocr/                             # Данные переводов
│   ├── translations/                # *.ndjson из Exiled Exchange 2
│   ├── tesseract/                   # traineddata (eng, rus)
│   ├── runeshape-combinations-ru.json
│   └── unique-category-map.json
│
├── scripts/                         # Python/PowerShell скрипты
├── config/                          # Дефолтный конфиг + JSON-схема
├── docs/                            # Эта документация
├── .github/                         # CI, issue templates
├── Directory.Build.props            # Общие настройки сборки
├── Directory.Packages.props         # Central package management
└── installer.iss                    # Inno Setup
```

## 4. Запуск тестов

### 4.1. Все тесты

```powershell
dotnet test
```

Запускает все тест-проекты в Release-конфигурации. Целевое время выполнения: ~30–60 секунд.

### 4.2. С покрытием кода

```powershell
dotnet test --collect:"XPlat Code Coverage"
```

Результаты в `tests/*/TestResults/*/coverage.cobertura.xml`. Для HTML-отчёта:

```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"tests/*/TestResults/*/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
start coverage-report/index.html
```

Целевое покрытие: Core — 90%+, OCR — 80%+, UI — 60%+.

### 4.3. Конкретный тест-проект

```powershell
dotnet test tests/AldurPrice.Core.Tests
dotnet test tests/AldurPrice.Integration --filter "Category=Ocr"
```

### 4.4. UI-тесты (FlaUI)

UI-тесты требуют интерактивной сессии (не работают в CI headless):

```powershell
dotnet test tests/AldurPrice.UI.Tests
```

## 5. Обновление переводов

### 5.1. Exiled Exchange 2 (базовые предметы)

```powershell
python scripts/update-translations.py
```

Скрипт:
1. Клонирует shallow-копию `github.com/Kvan7/Exiled-Exchange-2` во временную папку
2. Копирует `*.ndjson` в `ocr/translations/`
3. Проверяет, что количество строк = 4319 (sanity check)
4. Делает commit если есть изменения

CI-задача `.github/workflows/translations.yml` запускает это еженедельно (cron `0 3 * * 1`).

### 5.2. poe2db.tw (рунные комбинации)

```powershell
python scripts/parse-poe2db-runeshapes.py --output ocr/runeshape-combinations-ru.json
```

Скрипт:
1. Скачивает `https://poe2db.tw/ru/Runeshape_Combinations`
2. Парсит HTML, извлекает EN↔RU маппинг рун
3. Сохраняет в JSON

Запускать вручную при релизе новой лиги или добавлении новых рун.

### 5.3. Tesseract traineddata

При первом `dotnet build` MSBuild target `EnsureTessData` скачает:
- `ocr/tesseract/eng.traineddata` (~40 МБ) — с `tessdata_best`
- `ocr/tesseract/rus.traineddata` (~45 МБ) — с `tessdata_best`

Для других языков:

```powershell
# Скачать немецкий
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_best/raw/main/deu.traineddata" `
  -OutFile "ocr/tesseract/deu.traineddata"
```

## 6. Сборка релиза

### 6.1. Portable .zip

```powershell
dotnet publish src/AldurPrice -c Release -r win-x64 --self-contained
```

Результат в `src/AldurPrice/bin/Release/win-x64/publish/`:
- `AldurPrice.exe` — single-file executable (~120 МБ)
- `README.md` — копия
- `config/` — дефолтный конфиг

Упаковка в .zip:

```powershell
Compress-Archive -Path "src/AldurPrice/bin/Release/win-x64/publish/*" `
  -DestinationPath "bin/AldurPrice-v0.2.0-alpha.zip" -Force
```

### 6.2. Установщик (Inno Setup)

Требуется [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

Результат: `bin/AldurPrice-Installer-v0.2.0-alpha.exe`.

### 6.3. Полный релиз (CI-автоматизированный)

При push tag `v*`:

1. CI `.github/workflows/ci.yml` запускает `dotnet publish`
2. Запускает все тесты (блокирует релиз при падении)
3. Создаёт .zip (portable) и .exe (installer)
4. Создаёт GitHub Release с обоими артефактами
5. Обновляет `appsettings.json` → `UpdateChecker` будет указывать на новый релиз

## 7. Отладка

### 7.1. Логи

Логи пишутся в `logs/app-YYYYMMDD.txt` (Serilog rolling daily). Уровень лога настраивается в `appsettings.json` → `App.LogLevel`:

- `Trace` — максимально подробно (включая cache hit/miss, OCR timing)
- `Debug` — подробно (включая переводы, цена по предмету)
- `Information` — нормально (старт, остановка, обновление кэша)
- `Warning` — только предупреждения (fallback'и, OCR-ошибки)
- `Error` — только ошибки

Изменение уровня применяется мгновенно (hot-reload конфига).

### 7.2. Debug overlay

В Settings → Debug:

- **Show Debug Overlay** — красная рамка региона захвата + зелёные рамки строк + жёлтая панель
- **Save Debug Images** — сохранение захваченных битмапов в `debug-images/` (каждые 15 секунд)
- **Show OCR Inspector** — отдельное окно с пошаговым препроцессингом

### 7.3. Crash-логи

При необработанном исключении:
- Запись в `crash-reports/crash-YYYYMMDD-HHMMSS.txt`
- Диалог «Произошла ошибка» с кнопкой «Отправить отчёт»
- Bug report собирает: логи + crash-reports + debug-images + system info в .zip

### 7.4. Отладка Tesseract

Если Tesseract падает или не загружается:

1. Проверить `ocr/tesseract/` — есть ли `eng.traineddata` и `rus.traineddata`
2. Проверить native DLL в `native/tesseract50.dll` и `native/leptonica-1.82.0.dll`
3. Включить `LogLevel: "Trace"` — будет виден процесс загрузки
4. Запустить `TesseractBootstrapperTests` — тест распаковки native DLL

### 7.5. Профилирование

Для поиска узких мест производительности:

```powershell
# Visual Studio Performance Profiler
# Analyze → Performance Profiler → CPU Usage

# Или через dotnet-trace
dotnet tool install -g dotnet-trace
dotnet-trace collect --process-id <PID> --format speedscope
# Открыть .speedscope в https://speedscope.app
```

## 8. Разработка на Linux/macOS

WPF — Windows-only, но `Core`, `Data` и `Ocr` проекты можно разрабатывать и тестировать на Linux/macOS:

```bash
# Только Core + Data + тесты (без WPF)
dotnet test tests/AldurPrice.Core.Tests
dotnet test tests/AldurPrice.Data.Tests
```

Для полной разработки нужен Windows (физический ПК или VM). Альтернативы:

- **Parallels Desktop** на macOS с Windows 11 ARM
- **VMware Workstation Player** на Linux
- **GitHub Codespaces** с Windows runner (экспериментально)

Полная кроссплатформенность (Linux/macOS) запланирована в бэклоге через Avalonia UI (N6 в roadmap).

## 9. Частые проблемы

### 9.1. «Tesseract native DLL not found»

**Причина:** MSBuild target `EnsureTessData` не отработал, или NuGet-кэш поврежден.

**Решение:**

```powershell
dotnet restore --force
dotnet clean
dotnet build -c Debug
```

Если не помогло — вручную скопировать из `%USERPROFILE%\.nuget\packages\tesseract\5.2.0\x64\` в `native/`.

### 9.2. «Windows OCR not available»

**Причина:** Windows < 10 1809, или языковой пакет не установлен.

**Решение:**

- Settings → Time & Language → Language → Add a language → Russian → Speech → Install
- Или переключить `OcrBackend: "tesseract"` в `appsettings.json`

### 9.3. «No prices for known items»

**Причина:** Несовпадение лиги, или кэш пуст, или перевод не сработал.

**Решение:**

1. Проверить `Pricing.League` — должно совпадать с poe2scout.com
2. Включить `LogLevel: "Debug"` — посмотреть, какой английский name получен после перевода
3. Проверить в `ocr/runeshape-combinations-ru.json` — есть ли эта руна
4. Если нет — запустить `scripts/parse-poe2db-runeshapes.py`

### 9.4. «Overlay not visible»

**Причина:** Wrong region, или overlay disabled, или PoE2 в fullscreen.

**Решение:**

1. Settings → Debug → Show Debug Overlay (красная рамка должна быть видна)
2. Если рамки нет — re-run initial setup
3. Проверить, что PoE2 в «Окно без рамки» (Borderless Windowed)
4. Проверить `App.PricingOverlay: true` в конфиге

### 9.5. «Build fails after pull»

**Причина:** Изменилась структура проектов, NuGet-кэш устарел.

**Решение:**

```powershell
dotnet clean
rm -r bin/ obj/  # на каждом проекте
dotnet restore --force
dotnet build
```

## 10. Полезные команды

```powershell
# Watch-режим с авто-перезапуском
dotnet watch --project src/AldurPrice run --verbosity normal

# Запуск с конкретным конфигом
dotnet run --project src/AldurPrice -- --App:LogLevel=Trace

# Тест конкретного метода
dotnet test --filter "FullyQualifiedName~RuneshapeCombinationTranslatorTests.TryTranslate_RuneOfFire"

# Benchmark
python scripts/benchmark-ocr.py --duration 300 --output benchmarks/results-dev.json

# Обновить переводы
python scripts/update-translations.py --verbose

# Парсинг poe2db
python scripts/parse-poe2db-runeshapes.py --dry-run --verbose
```
