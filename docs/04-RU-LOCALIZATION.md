# План русской локализации

> Документ описывает полную стратегию русификации AldurPrice: источники данных перевода, структуру маппингов, UI-локализацию через .resx, тестирование качества перевода, и обновление данных. Русификация — первый класс гражданства, не послеthought.

## 1. Обзор локализации

Русификация состоит из трёх независимых частей, каждая со своим источником данных и стратегией:

1. **Локализация предметных строк (OCR → цены)** — перевод распознанных русских названий предметов на английский для поиска цен. Источник: Exiled Exchange 2 (4319 базовых предметов, bundle в M1.5) + poe2db.tw (153 рунных комбинации, ✅ bundled).

2. **Локализация UI-строк** — перевод интерфейса приложения (кнопки, лейблы, диалоги, сообщения). Источник: ручной перевод, хранится в `.resx`-файлах.

3. **Локализация отображаемых цен** — форматирование валюты (chaos/divine/exalt) с поддержкой русских суффиксов («с», «д», «экс») или английских («c», «d», «ex»). Настраивается пользователем.

Каждая часть имеет свои сложности. Предметные строки — самая объёмная (4000+ записей) и требует обновления с каждым патчем игры. UI-строки — ~300 записей, относительно статичны. Форматирование цен — компактно, но требует учёта длины строки для layout оверлея.

## 2. Источники данных перевода

### 2.1. Exiled Exchange 2 — базовые предметы

**URL:** [github.com/Kvan7/Exiled-Exchange-2](https://github.com/Kvan7/Exiled-Exchange-2)

**Что берём:** Файлы `items.ndjson` для каждого языка из директории переводов репозитория Exiled Exchange 2. Эти файлы генерируются автоматически из `BaseItemTypes.json` PoE2 (датамайнинг из файлов игры), поэтому всегда актуальны.

**Формат:** NDJSON (Newline-Delimited JSON), каждая строка — JSON-объект:

```json
{"name": "Подчиняющая порча", "refName": "Abiding Hex", "namespace": "GEM", "icon": "https://web.poecdn.com/...", "tags": [], "craftable": {"category": "Support Skill Gem"}, "w": 1, "h": 1, "gem": {"awakened": false, "transfigured": false}}
```

- `name` — локализованное (русское) название
- `refName` — английский reference name (используется для поиска цен)
- `namespace` — `GEM`, `CURRENCY`, `ITEM`, `UNIQUE`, и т.д.
- `craftable.category` — категория (используется для tier-fallback)

**Объём:** 4 319 предметов на язык. Доступные языки: `eng`, `rus`, `deu`, `fra`, `spa`, `por`, `kor`, `chi_tra`. В AldurPrice по умолчанию bundled только `rus` + `eng` (~2 МБ), остальные — opt-in download.

**Лицензия:** Exiled Exchange 2 распространяется под своей лицензией — нужно проверить перед релизом. В AldurPrice добавляем явную атрибуцию и копируем LICENSE в `ocr/translations/LICENSE`.

**Обновление:** Скрипт `scripts/update-translations.py` клонирует shallow-копию Exiled Exchange 2, копирует `*.ndjson` в `ocr/translations/`, делает commit. Запускается:

- Вручную: `python scripts/update-translations.py`
- В CI: еженедельно по расписанию (cron в `.github/workflows/translations.yml`)
- Pre-build: MSBuild target перед `BeforeBuild`

### 2.2. poe2db.tw — рунные комбинации

**URL:** [poe2db.tw/ru/Runeshape_Combinations](https://poe2db.tw/ru/Runeshape_Combinations)

**Что берём:** Маппинг английских и русских названий всех рунных комбинаций (ремнантов) лиги «Runes of Aldur». Это динамический контент лиги, который отсутствует в `BaseItemTypes.json` и, соответственно, в Exiled Exchange 2.

**Формат страницы:** HTML с таблицами и JS-данными. Парсер `scripts/parse-poe2db-runeshapes.py` (Python, использует `requests` + `lxml`):

1. Скачивает страницу `https://poe2db.tw/ru/Runeshape_Combinations`
2. Извлекает все элементы с классами, относящимися к рунам и комбинациям
3. Для каждого элемента находит английский и русский варианты
4. Формирует JSON-маппинг и сохраняет в `ocr/runeshape-combinations-ru.json`

**Пример извлечённых данных:**

```json
{
  "version": 1,
  "source": "https://poe2db.tw/ru/Runeshape_Combinations",
  "fetched_at": "2025-07-10T12:00:00Z",
  "count": 153,
  "combinations": [
    { "en": "Fire Rune",        "ru": "Руна огня",         "tier": "basic" },
    { "en": "Cold Rune",        "ru": "Руна холода",       "tier": "basic" },
    { "en": "Adaptive Rune",    "ru": "Руна адаптации",    "tier": "basic" },
    { "en": "Masterwork Rune",  "ru": "Мастерская руна",   "tier": "master" },
    { "en": "Adaptive Alloy",   "ru": "Адаптивный сплав",  "tier": "alloy" },
    { "en": "Ward Rune",        "ru": "Руна барьера",      "tier": "ward" },
    { "en": "Ancient Rune of Witchcraft", "ru": "Древняя руна ведьмовства", "tier": "ancient" },
    { "en": "Farrul's Rune of the Chase", "ru": "Руна погони Фаррул",       "tier": "lineage" },
    { "en": "Charging Rune",    "ru": "Руна заряда",        "tier": "special" }
    // ... полный список — в ocr/runeshape-combinations-ru.json (153 записи)
  ]
}
```

**Имена рун в poe2db** используют форму «Adjective Rune» (Fire Rune, Cold Rune), а НЕ «Rune of Adjective» (Rune of Fire). Это актуальные имена из BaseItemTypes.json PoE2. Аналогично: lineage runes называются «Farrul's Rune of the Chase», а НЕ «Farrul's Chase Rune»; ward runes — «Ward Rune», а НЕ «Emptying Ward Rune». Имена могут меняться с патчами лиги — парсер нужно перезапускать после каждого крупного патча.

**Объём:** 153 записи по состоянию на 2025-07 (alloy 13, ancient 13, basic 82, lineage 21, master 1, special 3, ward 20). Объём меняется с патчами лиги — после каждого патча перезапускать `scripts/parse-poe2db-runeshapes.py`.

**Загрузка в runtime:** JSON embedded как resource в `AldurPrice.Core.csproj` (`<EmbeddedResource>` с `LogicalName=AldurPrice.Core.Translation.runeshape-combinations-ru.json`). `RuneshapeCombinationTranslator` грузит его через `Assembly.GetManifestResourceStream` — не требует файловых путей. Для тестов — stream-конструктор с inline JSON. См. AD-002 в STATUS.md.

**Лицензия:** poe2db.tw — fan-wiki, данные публично доступны. Атрибуция в `LICENSE` и в `ocr/runeshape-combinations-ru.json` через поле `"source"`.

### 2.3. unique-category-map.json — ключевые слова категорий

**Что это:** Маппинг многоязычных ключевых слов категорий предметов (оружие, броня, аксессуары) для fuzzy-поиска уников. Например, «ДВУРУЧНАЯ БУЛАВА» → категория «TWO HAND MACE», «КОЛЬЦО» → «RING».

**Источник:** Генерируется из `BaseItemTypes.json` PoE2 или вручную курируется. ~150 ключевых слов суммарно. Русских: ~50 (ДВУРУЧНАЯ БУЛАВА, ОДНОРУЧНЫЙ ТОПОР, ШЛЕМ, ПЕРЧАТКИ, КОЛЬЦО, и т.д.).

## 3. Архитектура перевода предметных строк

### 3.1. ItemNameTranslator — цепочка fallback'ов

```
Вход: русское название из OCR (например, "Руна огня")
  ↓
[1] RuneshapeCombinationTranslator.TryTranslate("Руна огня")
    → exact match в embedded runeshape-combinations-ru.json (OrdinalIgnoreCase)
    → если не exact: stem match (по слову, через RussianStemmer)
    → если не stem: Levenshtein distance ≤ 2
    → найдено: "Fire Rune" ✅ RETURN
  ↓ (если не найдено)
[2] TranslationCache.TryLookup("Руна огня")
    → exact match (OrdinalIgnoreCase) в ConcurrentDictionary
    → populated из embedded rus.ndjson (4 319 базовых предметов Exiled Exchange 2)
       через LoadEmbeddedOrDefault() при startup
    → если rus.ndjson не bundled (KI-017) — кэш пустой, fallback промахивается
    → если найдено: RETURN
  ↓ (если не найдено)
[3] Bundled translations.json: "Руна огня##rus"
    → fallback для валюты (100 предметов)
    → M1.5+, не реализовано в этой итерации
  ↓ (если не найдено)
[4] Return null
    → цен не будет, в логе warning
```

**Текущее покрытие (M1.5-partial):**
- `[1]` — рунные комбинации (~150 предметов) ✅
- `[2]` — базовые предметы из `rus.ndjson` (4 319) ✅ (после запуска `scripts/update-translations.py` — см. KI-017)
- `[3]` — translations.json (валюта) ⏳ не реализовано
- Stem/Levenshtein для базовых предметов — M1.10 (после калибровки на реальных скриншотах)

### 3.2. RuneshapeCombinationTranslator

```csharp
namespace AldurPrice.Core.Translation;

public sealed class RuneshapeCombinationTranslator
{
    private const string EmbeddedResourceName =
        "AldurPrice.Core.Translation.runeshape-combinations-ru.json";

    private readonly Dictionary<string, string> _ruToEn;       // exact, OrdinalIgnoreCase
    private readonly Dictionary<string, string> _stemKeyToEn;  // stem-of-ru → en
    private readonly List<KeyValuePair<string, string>> _ruEntries; // for Levenshtein
    private readonly RussianStemmer _stemmer = new();
    private readonly Levenshtein _levenshtein = new(maxDistance: 2);

    /// <summary>Production-конструктор: грузит embedded JSON через Assembly.GetManifestResourceStream.</summary>
    public RuneshapeCombinationTranslator()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found. " +
                "Rebuild AldurPrice.Core to embed the JSON.");
        LoadFromStream(stream);
    }

    /// <summary>Test-конструктор: грузит JSON из произвольного stream (inline JSON в тестах).</summary>
    public RuneshapeCombinationTranslator(Stream jsonStream)
    {
        ArgumentNullException.ThrowIfNull(jsonStream);
        LoadFromStream(jsonStream);
    }

    public string? TryTranslate(string russianName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(russianName);

        // 1. Exact match (OrdinalIgnoreCase)
        if (_ruToEn.TryGetValue(russianName, out var en))
            return en;

        // 2. Stem matching (по слову, через RussianStemmer) — ловит падежи
        var stemKey = ComputeStemKey(russianName);
        if (_stemKeyToEn.TryGetValue(stemKey, out en))
            return en;

        // 3. Levenshtein ≤2 (OCR-искажения кириллицы)
        foreach (var (ru, enVal) in _ruEntries)
        {
            if (_levenshtein.Distance(russianName, ru) <= 2)
                return enVal;
        }

        return null;
    }
}
```

JSON загружается как **embedded resource** в `AldurPrice.Core.csproj` (см. AD-002 в STATUS.md):
```xml
<ItemGroup>
  <EmbeddedResource Include="..\..\ocr\runeshape-combinations-ru.json">
    <LogicalName>AldurPrice.Core.Translation.runeshape-combinations-ru.json</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

Это самодостаточно: не требует файловых путей, работает в single-file publish, тесты используют stream-конструктор с inline JSON.

### 3.3. RussianStemmer

Консервативный русский stemmer (НЕ полный Snowball). Нормализует окончания: «Руну» → «Рун», «Руна» → «Рун», «Руной» → «Рун». Намеренно не использует RV/R1/R2 регионы полного Snowball, чтобы не пере-стемить базовые слова предметных имён («руна» → «ру» вместо «рун» в полном Snowball). См. KI-007 в STATUS.md.

Реализация: `AldurPrice.Core/Translation/RussianStemmer.cs`, ~120 строк. Список окончаний отсортирован по убыванию длины, покрывает существительные (падежи, мн.ч.), прилагательные (полные и краткие, на -ский/-цкий), причастия, глаголы прош. времени, инфинитивы. Тесты `RussianStemmerTests` покрывают 13 кейсов во всех падежах.

## 4. Локализация UI через .resx

### 4.1. Структура ресурсов

```
src/AldurPrice/Resources/
├── Strings.resx              # Нейтральная культура (en, fallback)
├── Strings.ru.resx           # Русская культура
└── Strings.de.resx           # (опционально, для будущих языков)
```

`Strings.resx` — ключи и английские значения:

```xml
<root>
  <data name="Dashboard_Title" xml:space="preserve">
    <value>Settings</value>
  </data>
  <data name="Dashboard_Language" xml:space="preserve">
    <value>Language</value>
  </data>
  <data name="Settings_OcrLanguage" xml:space="preserve">
    <value>OCR Language</value>
  </data>
  <data name="Settings_PricingSource" xml:space="preserve">
    <value>Pricing Source</value>
  </data>
  <data name="Error_Poe2NotFound" xml:space="preserve">
    <value>Path of Exile 2 window not found. Make sure the game is running.</value>
  </data>
  <!-- ... ~300 ключей -->
</root>
```

`Strings.ru.resx` — те же ключи, русские значения:

```xml
<root>
  <data name="Dashboard_Title" xml:space="preserve">
    <value>Настройки</value>
  </data>
  <data name="Dashboard_Language" xml:space="preserve">
    <value>Язык</value>
  </data>
  <data name="Settings_OcrLanguage" xml:space="preserve">
    <value>Язык OCR</value>
  </data>
  <data name="Settings_PricingSource" xml:space="preserve">
    <value>Источник цен</value>
  </data>
  <data name="Error_Poe2NotFound" xml:space="preserve">
    <value>Окно Path of Exile 2 не найдено. Убедитесь, что игра запущена.</value>
  </data>
  <!-- ... -->
</root>
```

### 4.2. Доступ к ресурсам в коде

В XAML через markup extension:

```xml
<Window xmlns:i18n="clr-namespace:AldurPrice.Localization">
  <TextBlock Text="{i18n:StringLocalizer Key=Dashboard_Title}"/>
  <Button Content="{i18n:StringLocalizer Key=Button_Save}"/>
</Window>
```

В code-behind / ViewModel через `IStringLocalizer<App>`:

```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly IStringLocalizer<App> _loc;

    [ObservableProperty] private string _title;
    [ObservableProperty] private string _languageLabel;

    public MainViewModel(IStringLocalizer<App> loc)
    {
        _loc = loc;
        RefreshStrings();
    }

    public void RefreshStrings()
    {
        Title = _loc["Dashboard_Title"];
        LanguageLabel = _loc["Dashboard_Language"];
    }
}
```

При смене языка в Settings — вызывается `RefreshStrings()` во всех ViewModel + `LocalizationService` обновляет `CurrentUICulture`.

### 4.3. Список UI-строк для перевода

| Категория | Файлов | Строк | Примеры |
|---|---|---|---|
| Dashboard (главное окно) | 1 | ~50 | "Настройки", "Запустить", "Остановить", "Статус", "Лога" |
| Settings dialog | 1 | ~80 | "Язык OCR", "Источник цен", "Лига", "Пороги цветов", "Интервал сканирования" |
| Changelog window | 1 | ~10 | "Список изменений", "Версия", "Закрыть" |
| Crash dialog | 1 | ~15 | "Произошла ошибка", "Отправить отчёт", "Игнорировать" |
| Setup overlay | 1 | ~20 | "Выделите регион панели", "Готово", "Отмена" |
| Banner (нет цены) | 1 | ~10 | "Нет данных о цене", "Новый предмет" |
| Tooltips | 1 | ~40 | "Время между сканированиями", "Цвет для дешёвых предметов" |
| Error messages | 1 | ~30 | "Окно PoE2 не найдено", "Не удалось загрузить цены", "Невалидный конфиг" |
| Update dialog | 1 | ~15 | "Доступна новая версия", "Скачать", "Позже" |
| Tray menu | 1 | ~10 | "Открыть", "Настройки", "Выход" |
| **Итого** | | **~280** | |

### 4.4. Форматирование цен

Форматирование цен с поддержкой локали через опцию `Pricing.DisplayCurrencySuffix` = `en` | `ru`:

- `en`: «1.5ex», «0.3d», «12c»
- `ru` (default): «1.5экс», «0.3д», «12с»

Реализация в `PriceQuoteFormatter`:

```csharp
public string Format(PriceQuote quote, decimal divine, decimal exalt,
                     string currency, string suffixLang)
{
    var value = CalculateValue(quote, divine, exalt, currency);
    var suffix = GetSuffix(currency, suffixLang);
    return $"{value:0.#}{suffix}";
}

private static string GetSuffix(string currency, string lang) => (currency, lang) switch
{
    ("exalt", "ru")  => "экс",
    ("divine", "ru") => "д",
    ("chaos", "ru")  => "с",
    ("exalt",  "en") => "ex",
    ("divine", "en") => "d",
    ("chaos",  "en") => "c",
    _ => "c"
};
```

Оверлей автоматически расширяется под длину суффикса (WPF auto-sizing).

## 5. Автоопределение языка клиента PoE2

### 5.1. Poe2ConfigFile

`AldurPrice/Configuration/Poe2ConfigFile.cs`:

```csharp
public static class Poe2ConfigFile
{
    public static string? DetectLanguage()
    {
        // 1. Чтение game.ini
        var gameIni = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Path of Exile 2", "game.ini");

        if (File.Exists(gameIni))
        {
            var lines = File.ReadAllLines(gameIni);
            foreach (var line in lines)
            {
                if (line.StartsWith("language =", StringComparison.OrdinalIgnoreCase))
                {
                    var lang = line["language =".Length..].Trim().Trim('"');
                    return lang switch
                    {
                        "english"   => "eng",
                        "russian"   => "rus",
                        "german"    => "deu",
                        "french"    => "fra",
                        "portuguese"=> "por",
                        "spanish"   => "spa",
                        "korean"    => "kor",
                        "chinese"   => "chi_tra",
                        _ => null
                    };
                }
            }
        }

        // 2. Fallback: реестр (для Steam-версии)
        // ...

        return null;
    }
}
```

### 5.2. Логика автоопределения

При первом запуске:

1. `Poe2ConfigFile.DetectLanguage()` → `"rus"`
2. `OcrOptions.Language = "rus"`
3. Уведомление в Dashboard: «Обнаружен русский клиент PoE2 — язык OCR переключён на русский»
4. Сохранение в `appsettings.json`

При смене языка в клиенте:

- При следующем запуске AldurPrice — повторное определение
- Если отличается от текущего `OcrOptions.Language` — уведомление + автообновление
- Override через Settings с пометкой «Вручную, игнорировать автоопределение»

## 6. Тестирование качества перевода

### 6.1. Unit-тесты

- `RuneshapeCombinationTranslatorTests` — все 80+ рунных комбинаций должны переводиться точно
- `RussianStemmerTests` — 50+ слов во всех падежах
- `RussianOcrDistortionTests` — 30+ вариантов OCR-искажений («Руну огня», «Рунаагня», «Руна огн»)
- `ItemNameTranslatorRuTests` — end-to-end: «Подчиняющая порча» → «Abiding Hex», «Руна огня» → «Fire Rune»

### 6.2. Integration-тесты

- `OcrPricingSimulatorRuTests` — прогон реальных битмапов с русским текстом через весь пайплайн, проверка цен
- `LocalizationTests` — все ключи из `Strings.resx` присутствуют в `Strings.ru.resx`, нет пустых, нет дубликатов

### 6.3. Визуальное тестирование

- Скриншоты Dashboard в обоих языках (ru/en) для regression
- Проверка длины русских строк (не ломают ли layout)
- Проверка шрифта (Cyrillic glyph coverage в выбранном шрифте)

## 7. Процесс обновления переводов

### 7.1. Exiled Exchange 2 (еженедельно)

```bash
python scripts/update-translations.py
# → клонирует shallow-копию Exiled Exchange 2
# → копирует ocr/translations/*.ndjson
# → git commit -m "chore: update translations from Exiled Exchange 2"
# → git push (или PR)
```

CI-задача `.github/workflows/translations.yml` запускается по cron `0 3 * * 1` (понедельник 3:00 UTC).

### 7.2. poe2db.tw (при релизе лиги)

```bash
python scripts/parse-poe2db-runeshapes.py
# → скачивает https://poe2db.tw/ru/Runeshape_Combinations
# → парсит маппинг рун
# → обновляет ocr/runeshape-combinations-ru.json
# → git commit -m "chore: update runeshape combinations from poe2db.tw"
```

Ручной запуск перед релизом или при добавлении новых рун в патче.

### 7.3. UI-строки (по мере разработки)

- Добавление новой UI-строки: добавляем ключ в `Strings.resx` (en) + `Strings.ru.resx` (ru) одновременно
- CI-проверка: `LocalizationTests` падает, если ключ есть в en, но отсутствует в ru
- Пропущенные переводы — warning в CI, не block (fallback на en)

## 8. Риски и митигация

| Риск | Вероятность | Влияние | Митигация |
|---|---|---|---|
| poe2db.tw меняет вёрстку | Средняя | Парсер падает, нет обновления рун | Тест парсера на сохранённой HTML, ручной fallback |
| Exiled Exchange 2 меняет формат ndjson | Низкая | Все переводы ломаются | Версионирование в файле, fallback на bundled copy |
| Steam-версия PoE2 хранит конфиг в другом месте | Средняя | Не определяется язык | Fallback на реестр + ручной выбор в Settings |
| Русский текст длиннее английского, ломает layout | Высокая | UI некрасивый | WPF auto-sizing, ручная проверка всех диалогов, TextWrapping=Wrap |
| OCR плохо распознаёт кириллицу | Средняя | Часть рун не переводится | Tesseract `rus.traineddata` (best quality), postprocessor для типичных ошибок |
| Игрок использует mix языков (RU клиент + ENG item names) | Низкая | Перевод не работает | Автоопределение + fallback на eng, уведомление в логах |

## 9. Чек-лист готовности русификации

Локализация считается завершённой когда:

- [ ] `ocr/runeshape-combinations-ru.json` содержит 100% рун с poe2db.tw
- [ ] `ocr/translations/rus.ndjson` обновлён до последнего Exiled Exchange 2
- [ ] `Resources/Strings.ru.resx` содержит 100% ключей из `Strings.resx`
- [ ] Автоопределение языка клиента работает на 3+ тестовых конфигах
- [ ] Все unit-тесты перевода проходят
- [ ] Integration-тест `OcrPricingSimulatorRuTests` проходит на 10+ реальных скриншотах
- [ ] Визуальная проверка UI в обоих языках, layout не сломан
- [ ] Документация (README, Settings tooltips) переведена
- [ ] Чейнджлог переведён (или генерируется из структурированных данных)

Подробный чек-лист по milestone'ам — в [05-ROADMAP.md](05-ROADMAP.md).
