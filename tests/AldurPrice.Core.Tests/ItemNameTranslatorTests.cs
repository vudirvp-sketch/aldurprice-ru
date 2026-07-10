using AldurPrice.Core.Pricing;
using AldurPrice.Core.Translation;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Тесты <see cref="ItemNameTranslator"/> — цепочки fallback'ов.
/// Покрывает: runeshape combinations (через bundled JSON), русский↔английский
/// path, OCR-искажения (RussianOcrDistortionTests как subset).
/// </summary>
public sealed class ItemNameTranslatorTests
{
    // cache:null — изолирует тесты от TranslationCache (rus.ndjson может быть bundled
    // или нет, KI-017). Эти тесты проверяют ТОЛЬКО fallback [1] (runeshape).
    // Поведение с cache тестируется в ItemNameTranslatorCacheFallbackTests.
    private readonly ItemNameTranslator _translator =
        new(new RuneshapeCombinationTranslator(), cache: null);

    // ---- Eng client: имя возвращается как есть ----

    [Fact]
    public void TryTranslate_EngLanguage_ReturnsAsIs()
    {
        Assert.Equal("Exalted Orb", _translator.TryTranslate("Exalted Orb", "eng"));
        Assert.Equal("Rune of Fire", _translator.TryTranslate("Rune of Fire", "eng"));
    }

    // ---- Runeshape combinations (through bundled JSON) ----

    [Theory]
    [InlineData("Руна огня",        "Fire Rune")]
    [InlineData("Руна адаптации",   "Adaptive Rune")]
    [InlineData("Мастерская руна",  "Masterwork Rune")]
    [InlineData("Адаптивный сплав", "Adaptive Alloy")]
    public void TryTranslate_KnownRuneshapeRu_ReturnsEn(string ru, string expectedEn)
    {
        Assert.Equal(expectedEn, _translator.TryTranslate(ru, "rus"));
    }

    [Theory]
    [InlineData("РУНА ОГНЯ",       "Fire Rune")]      // upper case
    [InlineData("руна огня",       "Fire Rune")]      // lower case
    [InlineData("  Руна огня  ",  "Fire Rune")]      // whitespace
    public void TryTranslate_CaseAndWhitespace(string input, string expectedEn)
    {
        Assert.Equal(expectedEn, _translator.TryTranslate(input, "rus"));
    }

    [Theory]
    [InlineData("Руной огня",  "Fire Rune")]      // instrumental case
    [InlineData("Руну огня",   "Fire Rune")]      // accusative case
    [InlineData("Руной Огня",  "Fire Rune")]      // mixed case + instrumental
    public void TryTranslate_WithRussianCase_Endings_StrippedByStem(string input, string expectedEn)
    {
        Assert.Equal(expectedEn, _translator.TryTranslate(input, "rus"));
    }

    // ---- OCR distortion (RussianOcrDistortion subset) ----

    [Theory]
    [InlineData("Руна агня",  "Fire Rune")]      // о→а substitution (1 edit)
    [InlineData("Рунай огня", "Fire Rune")]      // й insertion (1 edit)
    public void TryTranslate_OcrDistortion_RecoveredByLevenshtein(string input, string expectedEn)
    {
        Assert.Equal(expectedEn, _translator.TryTranslate(input, "rus"));
    }

    [Theory]
    [InlineData("Pyна огня",  "Fire Rune")]      // Latin P (1 edit) — Levenshtein catches
    [InlineData("Рунa огня",  "Fire Rune")]      // Latin a (1 edit) — Levenshtein catches
    public void TryTranslate_LatinLookalikes_SingleEdit_RecoveredByLevenshtein(string input, string expectedEn)
    {
        // ItemNameTranslator НЕ вызывает ItemNameParser автоматически (это ответственность
        // OCR-pipeline в M1.3). Однако Levenshtein fallback ловит одиночные Latin→Cyrillic
        // замены (distance=1 ≤ 2). Для 2+ замен calling code должен использовать ItemNameParser.
        Assert.Equal(expectedEn, _translator.TryTranslate(input, "rus"));
    }

    [Fact]
    public void TryTranslate_LatinLookalikes_MultipleEdits_ReturnsNull()
    {
        // 3 Latin look-alikes (y, a, o) → distance=3 > 2 → null.
        // Demonstrates the limit of Levenshtein fallback without ItemNameParser.
        Assert.Null(_translator.TryTranslate("Рyнa oгня", "rus"));
    }

    [Fact]
    public void TryTranslate_AfterParserNormalization_Matches()
    {
        // Демонстрация корректного flow: parser → translator.
        var parser = new ItemNameParser();
        var parsed = parser.Parse("Pyна огня", "rus");  // Latin P → Cyrillic Р
        Assert.Equal("Руна огня", parsed.Name);
        Assert.Equal("Fire Rune", _translator.TryTranslate(parsed.Name, "rus"));
    }

    // ---- Unknown items return null (no false positives) ----

    [Theory]
    [InlineData("Зеркало Каландры")]           // currency, не в JSON
    [InlineData("Руна суперсилы")]              // выдуманное имя
    [InlineData("Совершенно неизвестный предмет")]
    public void TryTranslate_UnknownItem_ReturnsNull(string input)
    {
        Assert.Null(_translator.TryTranslate(input, "rus"));
    }

    // ---- Edge cases ----

    [Fact]
    public void TryTranslate_NullOrWhitespace_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => _translator.TryTranslate(null!, "rus"));
        Assert.ThrowsAny<ArgumentException>(() => _translator.TryTranslate("", "rus"));
        Assert.ThrowsAny<ArgumentException>(() => _translator.TryTranslate("   ", "rus"));
    }

    [Fact]
    public void TryTranslate_DefaultLanguageIsRus()
    {
        // Без явного language — default "rus".
        Assert.Equal("Fire Rune", _translator.TryTranslate("Руна огня"));
    }
}

/// <summary>
/// Subset-тесты OCR-искажений кириллицы, выделенные в отдельный класс
/// для roadmap-трекинга (см. STATUS.md → «Что дальше», M1.3 — RussianOcrPostProcessor).
/// Сейчас эти тесты проверяют, что translator+stemmer+Levenshtein корректно
/// восстанавливают типовые OCR-искажения. Когда будет RussianOcrPostProcessor,
/// эти тесты переедут к нему (или будут дублироваться).
/// </summary>
public sealed class RussianOcrDistortionTests
{
    // cache:null — см. комментарий в ItemNameTranslatorTests (изоляция от rus.ndjson bundling).
    private readonly ItemNameTranslator _translator =
        new(new RuneshapeCombinationTranslator(), cache: null);

    [Theory]
    [InlineData("Руна агня",       "Fire Rune", 1)]       // о→а, 1 edit
    [InlineData("Рунай огня",      "Fire Rune", 1)]       // й insertion, 1 edit
    [InlineData("Руну огня",       "Fire Rune", 0)]       // case ending (stem)
    [InlineData("Руной огня",      "Fire Rune", 0)]       // case ending (stem)
    public void OcrDistortion_RecoveredByStemOrLevenshtein(
        string ocrText, string expectedEn, int _)
    {
        Assert.Equal(expectedEn, _translator.TryTranslate(ocrText, "rus"));
    }

    [Theory]
    [InlineData("Рунайя огняяя",   null)]                  // too many edits
    [InlineData("Полнаячушь",      null)]                  // unrelated
    public void OcrDistortion_TooFar_ReturnsNull(string ocrText, string? expected)
    {
        Assert.Equal(expected, _translator.TryTranslate(ocrText, "rus"));
    }
}
