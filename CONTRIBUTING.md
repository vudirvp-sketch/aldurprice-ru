# Контрибьюшн в AldurPrice

Спасибо за интерес к проекту! Этот документ описывает, как эффективно помогать развитию AldurPrice. Проект открыт к контрибьюторам любого уровня — от перевода строк до архитектурных улучшений.

## С чего начать

### 1. Окружение разработки

Перед началом работы установите:

- **.NET 9 SDK** — [dot.net/download](https://dot.net/download)
- **Visual Studio 2022 17.10+** или **JetBrains Rider**
- **Git** 2.40+
- **Python 3.11+** (для скриптов переводов)

Подробная инструкция по настройке — в [docs/06-SETUP.md](docs/06-SETUP.md).

### 2. Найдите задачу

- **Good first issues** — задачи с label `good-first-issue`, подходят для новичков
- **Help wanted** — label `help-wanted`, нужны контрибьюторы
- **Локализация** — label `localization`, перевод строк или проверка качества
- **Тестирование** — label `testing`, прогон на разных конфигурациях
- **OCR** — label `ocr`, улучшение распознавания
- **UI** — label `ui`, WPF-дизайн

Если не нашли подходящую задачу — создайте issue с описанием, что хотите сделать, и мы обсудим.

### 3. Fork и branch

```bash
# Fork репозитория на GitHub (через UI)
git clone https://github.com/<your-username>/aldurprice.git
cd aldurprice
git remote add upstream https://github.com/<main-repo>/aldurprice.git

# Создайте branch для вашей задачи
git checkout -b M1/add-rune-translation
```

Branch naming: `{milestone}/{краткое-описание}`. Примеры:
- `M1/add-rune-translation`
- `M2/wpf-dashboard-localization`
- `M3/sqlite-cache-persistence`
- `fix/ocr-russian-stemming`

## Процесс разработки

### 1. Создайте issue (если ещё нет)

Опишите проблему или предложение. Maintainer'ы обсудят и поставят label. Для bugs используйте шаблон `bug_report.md`, для features — `feature_request.md`.

### 2. Обсудите подход

Для крупных изменений (более 200 строк) — сначала обсудите в issue, как будете делать. Это сэкономит время на ревью. Для мелких исправлений (опечатки, перевод строк) можно сразу PR.

### 3. Пишите код

Следуйте стилю кода:

- **C# 13** features welcome (record structs, primary constructors, collection expressions)
- **Nullable reference types** включены — `nullable enable` во всех файлах
- **File-scoped namespaces** — `namespace Foo;` вместо `namespace Foo { ... }`
- **Implicit usings** включены — не пишите `using System;` явно
- **Имена**: `PascalCase` для public, `_camelCase` для private fields, `IFoo` для интерфейсов
- **Async**: `async Task<T>`, не `async void` (кроме event handlers)
- **DI**: все зависимости через constructor injection, не service locator

Пример:

```csharp
namespace AldurPrice.Core.Translation;

public sealed class RuneshapeCombinationTranslator(IOptionsMonitor<TranslationOptions> options, ILogger<RuneshapeCombinationTranslator> logger)
{
    private readonly Dictionary<string, string> _ruToEn = new(StringComparer.OrdinalIgnoreCase);

    public string? TryTranslate(string russianName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(russianName);

        if (_ruToEn.TryGetValue(russianName, out var english))
        {
            logger.LogTrace("Translated '{Ru}' -> '{En}' (exact match)", russianName, english);
            return english;
        }

        return null;
    }
}
```

### 4. Пишите тесты

Каждый новый публичный метод — с тестом. Целевое покрытие: Core 90%+, OCR 80%+, UI 60%+.

```csharp
public class RuneshapeCombinationTranslatorTests
{
    [Fact]
    public void TryTranslate_RuneOfFire_ReturnsEnglish()
    {
        var translator = new RuneshapeCombinationTranslator("fixtures/runeshape-combinations-ru.json");
        var result = translator.TryTranslate("Руна огня");
        Assert.Equal("Rune of Fire", result);
    }

    [Theory]
    [InlineData("Руну огня")]      // Accusative
    [InlineData("Руна огне")]      // OCR typo
    [InlineData("Рунаагня")]       // OCR merge
    public void TryTranslate_DistortedVariants_ReturnsEnglish(string distorted)
    {
        var translator = new RuneshapeCombinationTranslator("fixtures/runeshape-combinations-ru.json");
        var result = translator.TryTranslate(distorted);
        Assert.Equal("Rune of Fire", result);
    }
}
```

### 5. Проверьте локально

```bash
# Build
dotnet build

# Все тесты
dotnet test

# С покрытием
dotnet test --collect:"XPlat Code Coverage"

# Конкретный тест-класс
dotnet test --filter "FullyQualifiedName~RuneshapeCombinationTranslatorTests"
```

Перед PR убедитесь, что:
- [ ] `dotnet build` без warnings (кроме suppressed)
- [ ] `dotnet test` — все тесты green
- [ ] Нет `console.log` / `Debug.WriteLine` в продакшн-коде (используйте `ILogger`)
- [ ] Добавлены/обновлены тесты для вашей логики
- [ ] Обновлена документация, если нужно

### 6. Commit'ы

Следуйте [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

Типы:
- `feat` — новая функция
- `fix` — исправление бага
- `refactor` — рефакторинг без изменения поведения
- `test` — добавление/изменение тестов
- `docs` — документация
- `chore` — сборка, CI, зависимости
- `i18n` — локализация
- `perf` — производительность

Примеры:

```
feat(translation): add RuneshapeCombinationTranslator for poe2db mapping

Implements RU→EN translation for runeshape combinations using the
mapping from poe2db.tw. Falls back to RussianStemmer for case variants
and Levenshtein distance ≤2 for OCR distortions.

Closes #42
```

```
fix(ocr): handle Ё→Е normalization in RussianOcrPostProcessor

Windows OCR sometimes returns 'Е' instead of 'Ё' in Russian text.
Add optional normalization (enabled by default) to improve matching
against rus.ndjson lookup keys.

Fixes #87
```

### 7. Push и Pull Request

```bash
git push origin M1/add-rune-translation
```

Создайте PR через GitHub UI. Заполните шаблон:

```markdown
## Что изменено

[Краткое описание]

## Почему

[Ссылка на issue / обоснование]

## Как протестировано

- [ ] `dotnet test` проходит
- [ ] Добавлены unit-тесты
- [ ] Проверено вручную на [описание сценария]

## Скриншоты (если UI)

[Для UI-изменений — скриншоты до/после]

## Чек-лист

- [ ] Код следует стилю проекта
- [ ] Тесты добавлены и проходят
- [ ] Документация обновлена
- [ ] Локализация добавлена (если UI-строки)
- [ ] CHANGELOG обновлён (если user-facing изменение)
```

## Ревью

Maintainer'ы ревьют PR в течение 2–5 дней. Ревью фокусируется на:

1. **Корректность** — не сломало ли существующее поведение (тесты должны это поймать)
2. **Архитектура** — соответствует ли [02-ARCHITECTURE.md](docs/02-ARCHITECTURE.md)
3. **Производительность** — не добавило ли alloc'ов в hot path
4. **Читаемость** — понятны ли имена, нужны ли комментарии
5. **Тесты** — покрывают ли edge-case'ы

Не обижайтесь на замечания — ревью про код, не про вас. Все замечания можно обсудить в комментариях.

## Специфичные контрибьюшены

### Перевод UI-строк

1. Откройте `src/AldurPrice/Resources/Strings.resx` (en) и `Strings.ru.resx` (ru)
2. Найдите пустые значения или `TODO` в `Strings.ru.resx`
3. Добавьте перевод
4. Запустите `dotnet test --filter "LocalizationTests"` — проверит, что все ключи переведены
5. PR с типом `i18n`

### Добавление рунных комбинаций

Если в игре появилась новая руна, которой нет в `ocr/runeshape-combinations-ru.json`:

1. Найдите её английское и русское название (в игре или на poe2db.tw)
2. Добавьте запись в JSON с `"manual": true` (пометка ручного ввода)
3. Добавьте тест в `RuneshapeCombinationTranslatorTests`
4. PR с типом `feat(translation)` или `i18n`

### Добавление тестовых скриншотов

Для тестов OCR нужны реальные скриншоты панели рунешейпов:

1. Скриншот в PNG (без чувствительной информации — можно обрезать персонажа)
2. Разрешение в имени файла: `runeshape-panel-1080p-russian-01.png`
3. Файл в `tests/fixtures/screenshots/`
4. В `tests/AldurPrice.Integration/OcrPipelineRealScreenshotsTests.cs` — добавить кейс с ожидаемыми предметами и ценами
5. PR

### Bug report'ы

Используйте шаблон `.github/ISSUE_TEMPLATE/bug_report.md`:

- Версия приложения (из Settings → About)
- Версия Windows
- Язык клиента PoE2
- Скриншот проблемы (если применимо)
- Логи (Settings → 🐛 → Bug report → прикрепить .zip)
- Воспроизводимость (всегда / иногда / один раз)

## Code of Conduct

Будьте уважительны. Проект открыт для всех независимо от уровня, языка, пола, ориентации. Неконструктивная критика, оскорбления, дискриминация — недопустимы и приведут к бану.

Полный текст — в [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Лицензия контрибьюций

Внося вклад, вы соглашаетесь, что ваш код будет распространяться под [MIT лицензией](LICENSE) проекта. Все права на ваши контрибьюции передаются проекту без ограничений.

## Вопросы

- **GitHub Discussions** — для вопросов и обсуждений
- **GitHub Issues** — для багов и feature request'ов

Спасибо, что помогаете делать PoE2 удобнее для русскоязычных игроков!
