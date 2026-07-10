# Translations (Exiled Exchange 2)

Переводы базовых предметов PoE2 (4 319 шт. на язык) в NDJSON-формате.
Источник — [Exiled Exchange 2](https://github.com/Kvan7/Exiled-Exchange-2).

## Что здесь должно быть

| Файл | Назначение | Обязателен? |
|---|---|---|
| `rus.ndjson` | Русские переводы (name) → английские refName | **Да** (для RU-клиента) |
| `eng.ndjson` | Английский (name == refName, не загружается AldurPrice) | Нет |
| `LICENSE` | Копия лицензии Exiled Exchange 2 | Да (перед релизом) |

AldurPrice по умолчанию использует только `rus.ndjson`. Остальные языки — opt-in (M3.5).

## Как загрузить

```bash
python scripts/update-translations.py
```

Скрипт:
1. Shallow-clone `github.com/Kvan7/Exiled-Exchange-2`.
2. Копирует `*.ndjson` (8 языков) сюда.
3. Проверяет, что каждый файл содержит 4 319 строк.
4. Коммитит изменения (или `--dry-run` для проверки без commit).

После загрузки — `dotnet build` автоматически embed'ит `rus.ndjson` в `AldurPrice.Core`
(см. `src/AldurPrice.Core/AldurPrice.Core.csproj`, `Condition="Exists"`).

## Формат NDJSON

Одна JSON-запись на строку:

```json
{"name":"Подчиняющая порча","refName":"Abiding Hex","namespace":"GEM","icon":"https://web.poecdn.com/...","tags":[],"tradeTag":"abiding-hex","craftable":{"category":"Support Skill Gem"},"w":1,"h":1,"gem":{"awakened":false,"transfigured":false}}
```

AldurPrice использует только поля:
- `name` — локализованное имя (ключ).
- `refName` — английское reference name (значение, для поиска цен).

Записи где `name == refName` (нет перевода) или `name`/`refName` пустые — пропускаются
парсером `TranslationCache.LoadNdjson`.

## Почему не committen по умолчанию

- **Размер:** ~2 МБ на язык, ~16 МБ для всех 8 языков.
- **Лицензия:** нужно проверить лицензию Exiled Exchange 2 перед релизом и добавить
  копию в `ocr/translations/LICENSE` + атрибуцию в `README.md`/`LICENSE` корня.
- **Частота обновления:** еженедельно через CI (`.github/workflows/translations.yml`).

До загрузки `rus.ndjson` — `TranslationCache` пустой, перевод базовых предметов не работает
(только рунные комбинации через `runeshape-combinations-ru.json`). См. KI-017 в STATUS.md.
