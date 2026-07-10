using AldurPrice.Core.Translation;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Smoke-тест для M0: проверяет, что RussianStemmer снимает типовые русские окончания.
/// Полная версия Snowball-алгоритма — в M1.2 (см. docs/05-ROADMAP.md).
/// </summary>
public sealed class RussianStemmerTests
{
    private readonly RussianStemmer _stemmer = new();

    [Theory]
    [InlineData("Руна",  "рун")]   // Nominative, capital — проверка lowercasing
    [InlineData("Руну",  "рун")]   // Accusative
    [InlineData("Руной", "рун")]   // Instrumental (-ой)
    public void Stem_RemovesRussianEndings(string input, string expected)
    {
        var actual = _stemmer.Stem(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Stem_TooShortWord_ReturnedAsLowercase()
    {
        // Слово короче MinStemLength — не трогаем окончания, только lowercasing.
        Assert.Equal("ру", _stemmer.Stem("Ру"));
    }

    [Fact]
    public void Stem_AlreadyStem_StaysSame()
    {
        // «рун» — уже базовая форма, окончаний нет.
        Assert.Equal("рун", _stemmer.Stem("рун"));
    }

    [Fact]
    public void Stem_NullOrWhitespace_Throws()
    {
        // ThrowIfNullOrWhiteSpace(null) → ArgumentNullException (производный от ArgumentException),
        // ThrowIfNullOrWhiteSpace("")  → ArgumentException.
        // ThrowsAny ловит оба.
        Assert.ThrowsAny<ArgumentException>(() => _stemmer.Stem(null!));
        Assert.ThrowsAny<ArgumentException>(() => _stemmer.Stem(""));
        Assert.ThrowsAny<ArgumentException>(() => _stemmer.Stem("   "));
    }
}
