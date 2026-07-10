# Changelog

Все заметные изменения проекта AldurPrice документируются здесь.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/),
версионирование — [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]

### Added
- Создана структура репозитория и документация
- Планируется MVP с базовой русификацией (см. [docs/05-ROADMAP.md](docs/05-ROADMAP.md))

## [0.1.0-alpha] — планируется

### Added
- Каркас проекта: 6 подпроектов (Core, Data, Ocr, Capture, основной, Tests)
- WPF .NET 9 + CommunityToolkit.Mvvm
- Central package management
- CI/CD через GitHub Actions
- Парсер poe2db.tw для рунных комбинаций (`scripts/parse-poe2db-runeshapes.py`)
- Обновление переводов из Exiled Exchange 2 (`scripts/update-translations.py`)
- Полная документация на русском (7 документов в `docs/`)
- Конфигурация с русскими дефолтами + JSON-схема
- Шаблоны issues: bug report, feature request, translation error

### Планируется в M1 (v0.2.0-alpha)
- `RuneshapeCombinationTranslator` для рунных комбинаций
- `RussianStemmer` для падежей
- `RussianOcrPostProcessor` для нормализации кириллицы
- `WindowsOcrEngine` и `TesseractEngine`
- `LeaguePanelDetector` по яркости
- `ImagePreprocessor` с цветовой фильтрацией
- `Poe2ScoutClient` и `PoeNinjaClient`
- `InMemoryPricingCache` (без persistence)
- Минимальный WPF-оверлей с click-through
- Автоопределение языка клиента PoE2
- Тесты на реальных скриншотах

### Планируется в M2 (v0.3.0-beta)
- Полная локализация UI через .resx
- WPF-дашборд с темами (dark/light)
- Переключатель языка в Settings
- Setup overlay с визуальным рисованием региона
- UI-тесты (FlaUI)

### Планируется в M3 (v0.4.0-beta)
- Frame-diffing через perceptual hash
- SQLite persistence кэша цен
- Адаптивный интервал сканирования
- Pause-when-unfocused
- Serilog structured logging

### Планируется в M4 (v1.0.0)
- Inno Setup установщик
- Автообновление через GitHub Releases
- Bug report service
- Финальная документация и скриншоты

---

## Источники данных

- [Exiled Exchange 2](https://github.com/Kvan7/Exiled-Exchange-2) — переводы базовых предметов (4319 шт.)
- [poe2db.tw](https://poe2db.tw/ru/Runeshape_Combinations) — переводы рунных комбинаций
- [poe2scout.com](https://poe2scout.com) — источник цен с 24-часовым усреднением
- [poe.ninja](https://poe.ninja) — альтернативный источник цен
- [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) — OCR-движок (fallback)
