using AldurPrice.Core.Pricing;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Тесты Levenshtein distance с early exit по maxDistance.
/// Полезен для RuneshapeCombinationTranslator (OCR-искажения кириллицы).
/// </summary>
public sealed class LevenshteinTests
{
    [Theory]
    [InlineData("руна", "руна", 0)]   // идентичны
    [InlineData("руна", "руне", 1)]   // 1 замена (а→е)
    [InlineData("руна", "руну", 1)]   // 1 замена
    [InlineData("руна", "руной", 2)]  // 1 вставка + 1 замена = 2
    public void Distance_BasicCases(string a, string b, int expected)
    {
        var lev = new Levenshtein(maxDistance: 5);
        Assert.Equal(expected, lev.Distance(a, b));
    }

    [Fact]
    public void Distance_EarlyExit_ReturnsMaxPlusOne_WhenTooFar()
    {
        var lev = new Levenshtein(maxDistance: 2);
        // «руна» vs «алдур» — distance явно больше 2.
        Assert.Equal(3, lev.Distance("руна", "алдур")); // 2+1
    }

    [Fact]
    public void Distance_LengthDiffExceedsMax_ReturnsEarly()
    {
        var lev = new Levenshtein(maxDistance: 2);
        Assert.Equal(3, lev.Distance("руна", "рунаогнябога"));
    }

    [Fact]
    public void Distance_NullOrEmpty_Throws()
    {
        var lev = new Levenshtein();
        // ThrowIfNullOrWhiteSpace(null) → ArgumentNullException, ("") → ArgumentException.
        Assert.ThrowsAny<ArgumentException>(() => lev.Distance(null!, "руна"));
        Assert.ThrowsAny<ArgumentException>(() => lev.Distance("руна", null!));
        Assert.ThrowsAny<ArgumentException>(() => lev.Distance("", "руна"));
    }
}
