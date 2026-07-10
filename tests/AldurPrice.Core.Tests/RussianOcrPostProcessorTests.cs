using AldurPrice.Core.Translation;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Тесты <see cref="RussianOcrPostProcessor"/>: нормализация OCR-текста для кириллицы.
/// </summary>
public sealed class RussianOcrPostProcessorTests
{
    private readonly RussianOcrPostProcessor _proc = new();

    // ---- Ё → Е нормализация ----

    [Theory]
    [InlineData("Руна Алёны",   "Руна Алены")]     // ё → е в середине слова
    [InlineData("Всё ещё",      "Все еще")]        // ё в окончании
    [InlineData("Ёлка",         "Елка")]           // ё в начале
    [InlineData("ТЁМНЫЙ",       "ТЕМНЫЙ")]         // Ё заглавная
    public void ProcessLine_ReplacesYoWithE(string input, string expected)
    {
        Assert.Equal(expected, _proc.ProcessLine(input, "rus"));
    }

    // ---- Управляющие символы ----

    [Fact]
    public void ProcessLine_StripsControlCharsExceptTabNewline()
    {
        var input = "Руна\u0007огня\u000Bтут";  // BEL, VT
        Assert.Equal("Рунаогнятут", _proc.ProcessLine(input, "rus"));
    }

    [Fact]
    public void ProcessLine_PreservesNewlineAsSeparator()
    {
        var input = "Руна огня\nРуна холода";
        Assert.Equal("Руна огня\nРуна холода", _proc.ProcessLine(input, "rus"));
    }

    // ---- Пробелы ----

    [Theory]
    [InlineData("Руна  огня",          "Руна огня")]       // multiple spaces
    [InlineData("Руна\tогня",          "Руна огня")]       // tab
    [InlineData("  Руна огня  ",       "Руна огня")]       // trim
    [InlineData("Руна\u00A0огня",      "Руна огня")]       // NBSP
    [InlineData("Руна\u2009огня",      "Руна огня")]       // thin space
    [InlineData("Руна\u3000огня",      "Руна огня")]       // ideographic space
    public void ProcessLine_NormalizesWhitespace(string input, string expected)
    {
        Assert.Equal(expected, _proc.ProcessLine(input, "rus"));
    }

    [Fact]
    public void ProcessLine_TrimsSpaceBeforeNewline()
    {
        var input = "Руна \n огня";
        Assert.Equal("Руна\nогня", _proc.ProcessLine(input, "rus"));
    }

    // ---- Кавычки ----

    [Theory]
    [InlineData("«Руна» огня",      "\"Руна\" огня")]
    [InlineData("„Руна“ огня",      "\"Руна\" огня")]
    [InlineData("“Руна” огня",      "\"Руна\" огня")]
    public void ProcessLine_ReplacesTypographicQuotes(string input, string expected)
    {
        Assert.Equal(expected, _proc.ProcessLine(input, "rus"));
    }

    // ---- Апострофы ----

    [Theory]
    [InlineData("Фаррул\u2019s",   "Фаррул's")]   // U+2019 RIGHT SINGLE QUOTATION MARK
    [InlineData("Фаррул\u2018s",   "Фаррул's")]   // U+2018 LEFT SINGLE QUOTATION MARK
    public void ProcessLine_ReplacesTypographicApostrophes(string input, string expected)
    {
        Assert.Equal(expected, _proc.ProcessLine(input, "rus"));
    }

    // ---- Дефис-минусы ----

    [Theory]
    [InlineData("Руна—огня",     "Руна-огня")]    // em-dash
    [InlineData("Руна–огня",     "Руна-огня")]    // en-dash
    [InlineData("Руна‐огня",     "Руна-огня")]    // hyphen
    public void ProcessLine_NormalizesDashesToHyphen(string input, string expected)
    {
        Assert.Equal(expected, _proc.ProcessLine(input, "rus"));
    }

    // ---- Висящие пунктуации ----

    [Theory]
    [InlineData("- Руна огня -",   "Руна огня")]
    [InlineData("|Руна огня|",     "Руна огня")]
    [InlineData("· Руна огня ·",   "Руна огня")]
    public void ProcessLine_TrimsStrayPunctuation(string input, string expected)
    {
        Assert.Equal(expected, _proc.ProcessLine(input, "rus"));
    }

    // ---- Буллеты → пробелы ----

    [Fact]
    public void ProcessLine_ConvertsBulletsToSpaces()
    {
        var input = "•Руна•огня";
        // Bullet → space, затем collapse → "Руна огня"
        Assert.Equal("Руна огня", _proc.ProcessLine(input, "rus"));
    }

    // ---- ENG-язык: Ё не трогается ----

    [Fact]
    public void ProcessLine_EngLanguage_DoesNotReplaceYo()
    {
        // На ENG-клиенте ё может быть в legitimately-английских словах (например, имена собственные).
        Assert.Equal("Ёлка", _proc.ProcessLine("Ёлка", "eng"));
    }

    [Fact]
    public void ProcessLine_EngLanguage_StillStripsControlChars()
    {
        // Управляющие символы и пробелы нормализуются для всех языков.
        Assert.Equal("Exalted Orb", _proc.ProcessLine("Exalted\u0007 Orb", "eng"));
    }

    // ---- Edge cases ----

    [Fact]
    public void ProcessLine_EmptyAfterNormalization_ReturnsEmpty()
    {
        Assert.Equal("", _proc.ProcessLine("   \u0007\u000B   ", "rus"));
    }

    [Fact]
    public void ProcessLine_OnlyStrayPunctuation_ReturnsEmpty()
    {
        Assert.Equal("", _proc.ProcessLine("- - - | · •", "rus"));
    }

    [Fact]
    public void ProcessLine_NullOrWhitespace_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => _proc.ProcessLine(null!, "rus"));
        Assert.ThrowsAny<ArgumentException>(() => _proc.ProcessLine("", "rus"));
        Assert.ThrowsAny<ArgumentException>(() => _proc.ProcessLine("   ", "rus"));
    }

    [Fact]
    public void ProcessLine_PreservesInnerPeriod()
    {
        // Точка внутри строки не должна срезаться — может быть в "ур. 20".
        Assert.Equal("Руна ур. 20", _proc.ProcessLine("Руна ур. 20", "rus"));
    }

    // ---- ProcessLines (multiple) ----

    [Fact]
    public void ProcessLines_PreservesOrderAndCount()
    {
        var lines = new[] { "Руна огня", "  Руна холода  ", "Руна адаптации" };
        var result = _proc.ProcessLines(lines, "rus");
        Assert.Equal(3, result.Count);
        Assert.Equal("Руна огня", result[0]);
        Assert.Equal("Руна холода", result[1]);
        Assert.Equal("Руна адаптации", result[2]);
    }

    [Fact]
    public void ProcessLines_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _proc.ProcessLines(null!, "rus"));
    }

    [Fact]
    public void ProcessLines_EmptyStringEntries_BecomeEmpty()
    {
        var lines = new[] { "Руна", "", "   ", "Огня" };
        var result = _proc.ProcessLines(lines, "rus");
        Assert.Equal(4, result.Count);
        Assert.Equal("Руна", result[0]);
        Assert.Equal("", result[1]);
        Assert.Equal("", result[2]);
        Assert.Equal("Огня", result[3]);
    }
}
