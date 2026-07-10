using System.IO;
using System.Text;
using AldurPrice.Core.Translation;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Тесты <see cref="RuneshapeCombinationTranslator"/>: загрузка embedded JSON,
/// exact/stem/Levenshtein matching. Использует bundled JSON (~150 записей)
/// через default-конструктор + inline JSON через stream-конструктор.
/// </summary>
public sealed class RuneshapeCombinationTranslatorTests
{
    /// <summary>Inline JSON для тестов fallback-логики без зависимости от bundled данных.</summary>
    private const string InlineJson = """
    {
      "version": 1,
      "source": "test://inline",
      "fetched_at": "2025-07-10T00:00:00Z",
      "count": 3,
      "combinations": [
        { "en": "Fire Rune",       "ru": "Руна огня",      "tier": "basic" },
        { "en": "Adaptive Alloy",  "ru": "Адаптивный сплав","tier": "alloy" },
        { "en": "Masterwork Rune", "ru": "Мастерская руна","tier": "master" }
      ]
    }
    """;

    private static RuneshapeCombinationTranslator CreateFromInline() =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(InlineJson)));

    [Fact]
    public void DefaultConstructor_LoadsEmbeddedJson_CountGreaterThanZero()
    {
        var t = new RuneshapeCombinationTranslator();
        Assert.True(t.Count > 0, "Bundled JSON should contain at least 1 entry.");
        // По состоянию на M1.2 парсер извлекает ~150 записей.
        Assert.True(t.Count >= 100, $"Expected ≥100 bundled entries, got {t.Count}.");
    }

    [Fact]
    public void StreamConstructor_EmptyJson_Throws()
    {
        var empty = """{"version":1,"combinations":[]}""";
        Assert.ThrowsAny<Exception>(() =>
            new RuneshapeCombinationTranslator(
                new MemoryStream(Encoding.UTF8.GetBytes(empty))));
    }

    [Fact]
    public void StreamConstructor_MalformedJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            new RuneshapeCombinationTranslator(
                new MemoryStream(Encoding.UTF8.GetBytes("not json at all"))));
    }

    [Fact]
    public void TryTranslate_ExactMatch_ReturnsEn()
    {
        var t = CreateFromInline();
        Assert.Equal("Fire Rune", t.TryTranslate("Руна огня"));
    }

    [Fact]
    public void TryTranslate_ExactMatch_CaseInsensitive()
    {
        var t = CreateFromInline();
        // Same chars, different case — OrdinalIgnoreCase.
        Assert.Equal("Fire Rune", t.TryTranslate("РУНА ОГНЯ"));
        Assert.Equal("Fire Rune", t.TryTranslate("руна огня"));
    }

    [Fact]
    public void TryTranslate_ExactMatch_TrimsWhitespace()
    {
        var t = CreateFromInline();
        Assert.Equal("Fire Rune", t.TryTranslate("  Руна огня  "));
    }

    [Fact]
    public void TryTranslate_StemMatch_HandlesInstrumentalCase()
    {
        var t = CreateFromInline();
        // "Руной огня" — instrumental case of "Руна огня".
        // Stem of "Руной" = "рун", stem of "Руна" = "рун" → match.
        Assert.Equal("Fire Rune", t.TryTranslate("Руной огня"));
    }

    [Fact]
    public void TryTranslate_StemMatch_HandlesPluralGenitive()
    {
        var t = CreateFromInline();
        // "Руну огня" — accusative case of "Руна огня".
        // Stem of "Руну" = "рун", stem of "Руна" = "рун" → match.
        Assert.Equal("Fire Rune", t.TryTranslate("Руну огня"));
    }

    [Fact]
    public void TryTranslate_LevenshteinMatch_HandlesOcrDistortion()
    {
        var t = CreateFromInline();
        // "Руна агня" vs "Руна огня" — 1 substitution (о→а). Distance=1 ≤ 2 → match.
        Assert.Equal("Fire Rune", t.TryTranslate("Руна агня"));
    }

    [Fact]
    public void TryTranslate_LevenshteinMatch_HandlesExtraChar()
    {
        var t = CreateFromInline();
        // "Рунай огня" vs "Руна огня" — 1 insertion (й). Distance=1 ≤ 2 → match.
        Assert.Equal("Fire Rune", t.TryTranslate("Рунай огня"));
    }

    [Fact]
    public void TryTranslate_NoMatch_ReturnsNull()
    {
        var t = CreateFromInline();
        // Что-то совершенно другое.
        Assert.Null(t.TryTranslate("Зеркало Каландры"));
    }

    [Fact]
    public void TryTranslate_TooManyEdits_ReturnsNull()
    {
        var t = CreateFromInline();
        // "Рунайя огняяя" vs "Руна огня" — too many edits.
        Assert.Null(t.TryTranslate("Рунайя огняяя"));
    }

    [Fact]
    public void TryTranslate_NullOrWhitespace_Throws()
    {
        var t = CreateFromInline();
        Assert.ThrowsAny<ArgumentException>(() => t.TryTranslate(null!));
        Assert.ThrowsAny<ArgumentException>(() => t.TryTranslate(""));
        Assert.ThrowsAny<ArgumentException>(() => t.TryTranslate("   "));
    }

    [Fact]
    public void TryTranslate_BundledJson_KnownRunes()
    {
        // Smoke-test на реальных bundled данных: несколько записей, которые
        // точно должны быть в JSON (проверялись вручную).
        var t = new RuneshapeCombinationTranslator();
        Assert.Equal("Fire Rune", t.TryTranslate("Руна огня"));
        Assert.Equal("Adaptive Rune", t.TryTranslate("Руна адаптации"));
        Assert.Equal("Masterwork Rune", t.TryTranslate("Мастерская руна"));
    }

    [Fact]
    public void Count_ReturnsNumberOfLoadedEntries()
    {
        var t = CreateFromInline();
        Assert.Equal(3, t.Count);
    }
}
