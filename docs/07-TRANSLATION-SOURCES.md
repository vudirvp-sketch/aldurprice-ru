# Источники данных перевода

> Краткий справочник по всем источникам данных перевода, используемым в AldurPrice: URL, формат, лицензия, частота обновления, скрипт для подтягивания.

## Сводная таблица

| Источник | Что берём | Объём | Формат | Лицензия | Обновление | Скрипт |
|---|---|---|---|---|---|---|
| **Exiled Exchange 2** | Перевод базовых предметов (4319 шт.) | ~2 МБ / язык | NDJSON | См. их LICENSE | Еженедельно (CI cron) | `scripts/update-translations.py` |
| **poe2db.tw** | Перевод рунных комбинаций (80+ шт.) | ~20 КБ | HTML → JSON | Fan-wiki, public | При релизе лиги / патче | `scripts/parse-poe2db-runeshapes.py` |
| **Tesseract tessdata_best** | Traineddata для OCR (eng, rus) | ~85 МБ | binary | Apache 2.0 | При первом build | MSBuild target `EnsureTessData` |
| **PoE2 BaseItemTypes.json** | (Опционально) самостоятельный датамайн | — | JSON | GGG, fair use | При патче | Manual |

---

## 1. Exiled Exchange 2

### URL
- Репозиторий: [github.com/Kvan7/Exiled-Exchange-2](https://github.com/Kvan7/Exiled-Exchange-2)
- Файлы переводов: `src/translations/items.{lang}.ndjson` (или аналогичный путь)

### Что берём
8 файлов NDJSON, по одному на язык:

| Файл | Язык | Строк |
|---|---|---|
| `eng.ndjson` | English | 4 319 |
| `rus.ndjson` | Русский | 4 319 |
| `deu.ndjson` | Deutsch | 4 319 |
| `fra.ndjson` | Français | 4 319 |
| `spa.ndjson` | Español | 4 319 |
| `por.ndjson` | Português | 4 319 |
| `kor.ndjson` | 한국어 | 4 319 |
| `chi_tra.ndjson` | 繁體中文 | 4 319 |

В AldurPrice по умолчанию bundled только `eng` + `rus`. Остальные — opt-in download через Settings.

### Формат данных

NDJSON (Newline-Delimited JSON), каждая строка — JSON-объект:

```json
{
  "name": "Подчиняющая порча",
  "refName": "Abiding Hex",
  "namespace": "GEM",
  "icon": "https://web.poecdn.com/gen/image/WzI1LDE0LHsi...",
  "tags": [],
  "tradeTag": "abiding-hex",
  "craftable": { "category": "Support Skill Gem" },
  "w": 1,
  "h": 1,
  "gem": { "awakened": false, "transfigured": false }
}
```

Поля:
- `name` — локализованное название (русский в `rus.ndjson`)
- `refName` — английский reference name (используется для поиска цен)
- `namespace` — `GEM`, `CURRENCY`, `ITEM`, `UNIQUE`, `ARMOUR`, `WEAPON`, и т.д.
- `icon` — URL иконки с poecdn.com (не используется в AldurPrice, полезно для debug)
- `craftable.category` — категория для tier-fallback
- `tradeTag` — тег для official trade API (если есть)

### Как генерируется
Exiled Exchange 2 генерирует эти файлы из `BaseItemTypes.json` PoE2 (датамайнинг из файлов игры при каждом патче). Скрипт генерации в их репозитории.

### Лицензия
Проверить в репозитории Exiled Exchange 2 перед релизом. AldurPrice добавляет:
- Копию их LICENSE в `ocr/translations/LICENSE`
- Атрибуцию в `README.md` и `LICENSE`
- Comment в `update-translations.py` со ссылкой

### Скрипт обновления
`scripts/update-translations.py` (см. полный код в `scripts/`):

```python
#!/usr/bin/env python3
"""Pull latest translations from Exiled Exchange 2."""

REPO_URL = "https://github.com/Kvan7/Exiled-Exchange-2.git"
TEMP_DIR = Path("_exiled-exchange-tmp")
DEST_DIR = Path("ocr/translations")
LANGS = ["eng", "rus", "deu", "fra", "spa", "por", "kor", "chi_tra"]
EXPECTED_LINES = 4319

# 1. Shallow clone
subprocess.run(["git", "clone", "--depth", "1", REPO_URL, TEMP_DIR], check=True)

# 2. Copy NDJSON files
for lang in LANGS:
    src = TEMP_DIR / "src" / "translations" / f"{lang}.ndjson"
    dst = DEST_DIR / f"{lang}.ndjson"
    shutil.copy2(src, dst)
    line_count = sum(1 for _ in open(dst, encoding="utf-8"))
    assert line_count == EXPECTED_LINES, f"{lang}: {line_count} lines"

# 3. Cleanup + commit
shutil.rmtree(TEMP_DIR)
subprocess.run(["git", "add", "ocr/translations/"])
subprocess.run(["git", "commit", "-m", "chore: update translations"])
```

### CI-задача
`.github/workflows/translations.yml`:

```yaml
name: Update translations
on:
  schedule:
    - cron: '0 3 * * 1'  # Every Monday 03:00 UTC
  workflow_dispatch:

jobs:
  update:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with: { python-version: '3.11' }
      - run: pip install requests lxml
      - run: python scripts/update-translations.py
      - run: python scripts/parse-poe2db-runeshapes.py
      - name: Create PR if changes
        uses: peter-evans/create-pull-request@v6
        with:
          title: 'chore: weekly translation update'
          branch: 'chore/translations-update'
          labels: 'localization, automated'
```

---

## 2. poe2db.tw — рунные комбинации

### URL
- Страница: [poe2db.tw/ru/Runeshape_Combinations](https://poe2db.tw/ru/Runeshape_Combinations)
- Английская версия: [poe2db.tw/us/Runeshape_Combinations](https://poe2db.tw/us/Runeshape_Combinations) (для сверки)

### Что берём
Маппинг английских и русских названий всех рунных комбинаций (ремнантов) лиги «Runes of Aldur». ~80–120 записей, включая:

- **Basic runes** (32): Rune of Fire → Руна огня, Rune of Cold → Руна холода, ...
- **Alloys** (2): Runefather's Alloy → Сплав Рунного отца, Runelord's Alloy → Сплав Повелителя рун
- **Lineage runes** (~25): Farrul's Chase Rune → Руна погони Фаррул, ...
- **Ward runes** (~6): Emptying Ward Rune → Барьерная руна опустевания, ...
- **Ancient runes** (~15): Ancient Witchcraft Rune → Древняя руна ведьмовства, ...
- **Special** (~5): Master Rune, Conducting Rune, Rune Diversion, ...

### Формат результата
`ocr/runeshape-combinations-ru.json`:

```json
{
  "version": 1,
  "source": "https://poe2db.tw/ru/Runeshape_Combinations",
  "fetched_at": "2025-07-10T12:00:00Z",
  "combinations": [
    {
      "en": "Rune of Fire",
      "ru": "Руна огня",
      "tier": "basic"
    },
    {
      "en": "Runefather's Alloy",
      "ru": "Сплав Рунного отца",
      "tier": "alloy"
    }
  ]
}
```

Поля:
- `en` — английское название (используется для поиска цен в poe2scout/poe.ninja)
- `ru` — русское название (как в RU-клиенте игры)
- `tier` — категория (`basic`, `alloy`, `lineage`, `ward`, `ancient`, `master`, `special`) — полезно для отладки

### Парсер
`scripts/parse-poe2db-runeshapes.py` (см. полный код в `scripts/`):

1. Скачивает RU и EN версии страницы
2. Извлекает названия рун из HTML (через `lxml`)
3. Сопоставляет RU↔EN по canonical key (lowercase, hyphenated)
4. Сохраняет в JSON

### Лицензия
poe2db.tw — fan-wiki, данные публично доступны. Атрибуция:
- В `ocr/runeshape-combinations-ru.json`: поле `"source": "https://poe2db.tw/ru/Runeshape_Combinations"`
- В `LICENSE`: упоминание источника
- В `README.md`: ссылка

### Когда обновлять
- При релизе новой лиги PoE2 (новые руны)
- При патче, добавляющем новые рунные комбинации
- При изменении переводов в существующих рунах (проверка раз в месяц)

### Риски
- **Вёрстка poe2db.tw может измениться** — парсер упадёт
  - Mitigation: тест парсера на сохранённой HTML-фикстуре, weekly CI-проверка
- **Перевод может быть неполным/некорректным** — фанатский перевод не официальный
  - Mitigation: сверка с actual RU-клиентом игры, ручная корректировка при необходимости
- **Новые руны могут не сразу появиться на poe2db.tw** — lag после патча
  - Mitigation: ручной ввод в `runeshape-combinations-ru.json` с пометкой `manual: true`

---

## 3. Tesseract tessdata_best

### URL
- Репозиторий: [github.com/tesseract-ocr/tessdata_best](https://github.com/tesseract-ocr/tessdata_best)
- Файлы: `eng.traineddata`, `rus.traineddata` (по 40–45 МБ каждый)

### Что берём
Traineddata для Tesseract OCR (fallback-движок). Только `eng` + `rus` bundled по умолчанию.

### Как скачивается
MSBuild target `EnsureTessData` в `AldurPrice.csproj`:

```xml
<Target Name="EnsureTessData" BeforeTargets="AssignTargetPaths">
  <PropertyGroup>
    <TessDataDir>$(MSBuildProjectDirectory)\ocr\tesseract</TessDataDir>
  </PropertyGroup>
  <MakeDir Directories="$(TessDataDir)" Condition="!Exists('$(TessDataDir)')" />
  <Exec Command="powershell -NoProfile -Command if (-not (Test-Path '$(TessDataDir)\eng.traineddata')) { Invoke-WebRequest -Uri 'https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata' -OutFile '$(TessDataDir)\eng.traineddata' }" />
  <Exec Command="powershell -NoProfile -Command if (-not (Test-Path '$(TessDataDir)\rus.traineddata')) { Invoke-WebRequest -Uri 'https://github.com/tesseract-ocr/tessdata_best/raw/main/rus.traineddata' -OutFile '$(TessDataDir)\rus.traineddata' }" />
  <ItemGroup>
    <EmbeddedResource Include="$(TessDataDir)\eng.traineddata" Visible="false" Link="ocr\tesseract\eng.traineddata" />
    <EmbeddedResource Include="$(TessDataDir)\rus.traineddata" Visible="false" Link="ocr\tesseract\rus.traineddata" />
  </ItemGroup>
</Target>
```

### Лицензия
Apache 2.0. Атрибуция в `LICENSE` и `README.md`.

### Альтернатива: tessdata_fast
Для уменьшения размера сборки можно использовать [tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast) (5–10 МБ на язык вместо 40 МБ), но качество распознавания хуже. Решение: оставить `tessdata_best` по умолчанию, opt-in на `tessdata_fast` в Settings.

---

## 4. PoE2 BaseItemTypes.json (опционально)

### Что это
Файл из датамайна PoE2, содержащий все определения базовых типов предметов. Source of truth для всех переводов.

### Зачем нужен
Exiled Exchange 2 уже генерирует `*.ndjson` из этого файла, поэтому прямого доступа не требуется. Но если нужна самостоятельная генерация (без зависимости от Exiled Exchange 2), можно датамайнить самостоятельно.

### Как получить
- Через [poe2db.tw Community](https://poe2db.tw/) → export data
- Через PoE2 Community API (если доступно)
- Через датамайн-репозитории на GitHub (неофициальные)

### Лицензия
GGG, fair use для community tools. Не для коммерческого использования.

### Решение
В AldurPrice используем Exiled Exchange 2 как прокси. Прямой датамайн — только если Exiled Exchange 2 перестанет обновляться.

---

## 5. Проверка качества переводов

### 5.1. Автоматическая проверка
CI-задача `translations-quality.yml`:

```yaml
- name: Check translation completeness
  run: |
    python scripts/check-translations.py
    # Проверяет:
    # - Все ключи Strings.resx есть в Strings.ru.resx
    # - rus.ndjson имеет 4319 строк
    # - runeshape-combinations-ru.json имеет > 80 записей
    # - Все EN имена в runeshape-combinations есть в poe2scout API
```

### 5.2. Ручная сверка
Перед релизом:

1. Сравнить `ocr/translations/rus.ndjson` с актуальным RU-клиентом (выборочно 20 предметов)
2. Сравнить `ocr/runeshape-combinations-ru.json` с панелью в игре (все руны)
3. Сравнить UI-строки с фактическим отображением в обоих языках

### 5.3. Bug report'ы
Если пользователь находит неправильный перевод:

1. Issue с шаблоном `translation_error.md`
2. Указать: английское название, русское название (в игре), русское название (в приложении), источник (OCR/log)
3. Fix в соответствующем JSON/.resx + тест

---

## 6. Резюме

| Действие | Частота | Автоматизация |
|---|---|---|
| Pull из Exiled Exchange 2 | Еженедельно | CI cron + PR |
| Pull из poe2db.tw | При релизе лиги | Manual |
| Download Tesseract traineddata | При первом build | MSBuild target |
| Проверка качества переводов | На каждый PR | CI job |
| Ручная сверка с игрой | Перед релизом | Manual |

Все источники — открытые и community-maintained. Зависимость от Exiled Exchange 2 — главный риск (если проект забросят, нужно будет самим генерировать из `BaseItemTypes.json`). Mitigation: bundled copy в репозитории, обновляется только при наличии интернета.
