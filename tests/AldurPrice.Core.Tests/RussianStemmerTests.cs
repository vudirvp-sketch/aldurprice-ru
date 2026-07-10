using AldurPrice.Core.Translation;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Тесты RussianStemmer. M1.2: список окончаний расширен (причастия, краткие
/// прилагательные, отглагольные существительные, прилагательные на -ский/-цкий).
/// Полный Snowball Russian с RV-регионами — см. KI-007 в STATUS.md.
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

    // ---- M1.2: расширенные кейсы ----

    [Theory]
    [InlineData("руны",     "рун")]   // plural genitive
    [InlineData("руне",     "рун")]   // prepositional
    [InlineData("рунам",    "рун")]   // dative plural
    [InlineData("рунах",    "рун")]   // prepositional plural
    public void Stem_AllCaseFormsOfRune_CollapseToSameStem(string input, string expected)
    {
        Assert.Equal(expected, _stemmer.Stem(input));
    }

    [Theory]
    [InlineData("новости",     "новост")]   // plural nominative
    [InlineData("новостью",    "новост")]   // instrumental — strip "ой"
    [InlineData("новостях",    "новост")]   // prepositional pl — strip "ах"
    public void Stem_NewsForms_CollapseToNovost(string input, string expected)
    {
        Assert.Equal(expected, _stemmer.Stem(input));
    }

    [Theory]
    [InlineData("рунный",      "рунн")]     // adjective m.sg. -ый
    [InlineData("рунного",     "рунн")]     // genitive m.sg.
    [InlineData("рунному",     "рунн")]     // dative m.sg.
    [InlineData("рунными",     "рунн")]     // instrumental pl.
    [InlineData("рунная",      "рунн")]     // feminine nom.
    [InlineData("рунное",      "рунн")]     // neuter nom.
    [InlineData("рунные",      "рунн")]     // plural nom.
    public void Stem_AdjectiveForms_CollapseToStem(string input, string expected)
    {
        Assert.Equal(expected, _stemmer.Stem(input));
    }

    [Theory]
    [InlineData("русский",     "рус")]     // -ский (4-char ending)
    [InlineData("русская",     "рус")]     // fem
    [InlineData("русское",     "рус")]     // neuter
    [InlineData("русские",     "рус")]     // plural
    [InlineData("русского",    "рус")]     // genitive
    [InlineData("русским",     "рус")]     // instrumental
    public void Stem_SkyAdjectives_CollapseToStem(string input, string expected)
    {
        Assert.Equal(expected, _stemmer.Stem(input));
    }

    [Theory]
    [InlineData("древний",     "древн")]    // -ний adjective
    [InlineData("древняя",     "древн")]
    [InlineData("древнее",     "древн")]
    [InlineData("древние",     "древн")]
    public void Stem_DrevnyAdjectiveForms_CollapseToStem(string input, string expected)
    {
        Assert.Equal(expected, _stemmer.Stem(input));
    }

    [Fact]
    public void Stem_MixedCase_Lowercased()
    {
        // Вход в произвольном регистре — выход всегда lowercase InvariantCulture.
        Assert.Equal("рун", _stemmer.Stem("РУНА"));
        Assert.Equal("рун", _stemmer.Stem("РунА"));
    }

    [Fact]
    public void Stem_EmptyAfterLower_ReturnsAsLower()
    {
        // 3-char input — минимально допустимая длина для обработки.
        Assert.Equal("дом", _stemmer.Stem("Дом"));  // no ending to strip
    }
}
