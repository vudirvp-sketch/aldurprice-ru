using AldurPrice.Core.Pricing;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Тесты <see cref="ItemNameParser"/>: выделение количества, уровня,
/// нормализация OCR-искажений (латиница→кириллица в mixed-словах).
/// </summary>
public sealed class ItemNameParserTests
{
    private readonly ItemNameParser _parser = new();

    // ---- Quantity parsing ----

    [Theory]
    [InlineData("1x Руна огня",         "Руна огня", 1)]     // leading "1x"
    [InlineData("2 x Руна огня",        "Руна огня", 2)]     // leading "2 x"
    [InlineData("3× Руна огня",         "Руна огня", 3)]     // leading "3×" (unicode multiply)
    [InlineData("2 шт Руна огня",       "Руна огня", 2)]     // leading "2 шт"
    [InlineData("2 шт. Руна огня",      "Руна огня", 2)]     // leading "2 шт."
    [InlineData("Руна огня ×5",         "Руна огня", 5)]     // trailing "×5"
    [InlineData("Руна огня x5",         "Руна огня", 5)]     // trailing "x5"
    public void Parse_ExtractsQuantity(string input, string expectedName, int expectedQty)
    {
        var result = _parser.Parse(input, "rus");
        Assert.Equal(expectedName, result.Name);
        Assert.Equal(expectedQty, result.Quantity);
    }

    // ---- Level parsing ----

    [Theory]
    [InlineData("Руна огня (lvl 20)",       "Руна огня", 20)]
    [InlineData("Руна огня (ур. 20)",       "Руна огня", 20)]
    [InlineData("Руна огня (level 20)",     "Руна огня", 20)]
    [InlineData("Руна огня lvl 20",         "Руна огня", 20)]
    [InlineData("Руна огня ур. 20",         "Руна огня", 20)]
    [InlineData("Руна огня lvl 18-20",      "Руна огня", 18)]  // range → take min
    public void Parse_ExtractsLevel(string input, string expectedName, int expectedLevel)
    {
        var result = _parser.Parse(input, "rus");
        Assert.Equal(expectedName, result.Name);
        Assert.Equal(expectedLevel, result.Level);
    }

    [Fact]
    public void Parse_NoLevel_ReturnsNullLevel()
    {
        var result = _parser.Parse("Руна огня", "rus");
        Assert.Null(result.Level);
    }

    // ---- Whitespace normalization ----

    [Theory]
    [InlineData("  Руна   огня  ", "Руна огня")]  // multiple spaces collapsed
    [InlineData("Руна\tогня",       "Руна огня")]  // tab → space
    public void Parse_NormalizesWhitespace(string input, string expectedName)
    {
        var result = _parser.Parse(input, "rus");
        Assert.Equal(expectedName, result.Name);
    }

    // ---- OCR normalization (Latin → Cyrillic in mixed words) ----

    [Theory]
    [InlineData("Pyна огня",  "Руна огня")]   // Latin P → Cyrillic Р (in word with cyrillic)
    [InlineData("Рyна огня",  "Руна огня")]   // Latin y → Cyrillic у
    [InlineData("Рyнa oгня",  "Руна огня")]   // mix of Latin y, a, o → Cyrillic у, а, о
    public void Parse_NormalizesLatinLookalikesInCyrillicWords(string input, string expectedName)
    {
        var result = _parser.Parse(input, "rus");
        Assert.Equal(expectedName, result.Name);
    }

    [Fact]
    public void Parse_PureEnglishWord_NotNormalized()
    {
        // На ENG-клиенте слово "Exalted" не должно превращаться в кириллицу.
        var result = _parser.Parse("Exalted Orb", "eng");
        Assert.Equal("Exalted Orb", result.Name);
    }

    [Fact]
    public void Parse_PureEnglishWordInRusMode_NotNormalized()
    {
        // Даже на RU-клиенте слово без кириллицы не нормализуется
        // (предположение: OCR вернул английское имя для валюты).
        var result = _parser.Parse("Exalted Orb", "rus");
        Assert.Equal("Exalted Orb", result.Name);
    }

    // ---- Language passthrough ----

    [Fact]
    public void Parse_PreservesLanguageField()
    {
        Assert.Equal("rus", _parser.Parse("Руна", "rus").Language);
        Assert.Equal("eng", _parser.Parse("Rune", "eng").Language);
    }

    [Fact]
    public void Parse_NullOrWhitespace_Throws()
    {
        // ThrowIfNullOrWhiteSpace throws on null, empty, and whitespace-only.
        Assert.ThrowsAny<ArgumentException>(() => _parser.Parse(null!, "rus"));
        Assert.ThrowsAny<ArgumentException>(() => _parser.Parse("", "rus"));
        Assert.ThrowsAny<ArgumentException>(() => _parser.Parse("   ", "rus"));
    }

    // ---- Combined quantity + level ----

    [Fact]
    public void Parse_QuantityAndLevel_ExtractsBoth()
    {
        var result = _parser.Parse("2x Руна огня (lvl 20)", "rus");
        Assert.Equal("Руна огня", result.Name);
        Assert.Equal(2, result.Quantity);
        Assert.Equal(20, result.Level);
    }
}
