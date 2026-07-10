using System.IO;
using System.Text;
using AldurPrice.Core.Translation;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Интеграционные тесты цепочки fallback <see cref="ItemNameTranslator"/>:
/// [1] RuneshapeCombinationTranslator → [2] TranslationCache (rus.ndjson) → null.
///
/// <para>Проверяют приоритет (рунные комбинации из [1] не перекрываются cache [2]),
/// корректность fallback [2] для базовых предметов, и graceful degradation при пустом cache.</para>
/// </summary>
public sealed class ItemNameTranslatorCacheFallbackTests
{
    /// <summary>Inline NDJSON с базовыми предметами (НЕ рунные комбинации).
    /// Рунная комбинация «Руна огня» намеренно отсутствует — она должна резолвиться через [1].</summary>
    private const string BaseItemsNdjson = """
        {"name":"Подчиняющая порча","refName":"Abiding Hex","namespace":"GEM","w":1,"h":1}
        {"name":"Сфера хаоса","refName":"Chaos Orb","namespace":"CURRENCY","w":1,"h":1}
        {"name":"Зеркало Каландры","refName":"Mirror of Kalandra","namespace":"CURRENCY","w":1,"h":1}
        """;

    private static TranslationCache CreateCache() =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(BaseItemsNdjson)));

    private static ItemNameTranslator CreateTranslatorWithCache() =>
        new(new RuneshapeCombinationTranslator(), CreateCache());

    private static ItemNameTranslator CreateTranslatorWithoutCache() =>
        new(new RuneshapeCombinationTranslator(), cache: null);

    // ---- Fallback [2]: базовые предметы через TranslationCache ----

    [Theory]
    [InlineData("Подчиняющая порча", "Abiding Hex")]
    [InlineData("Сфера хаоса", "Chaos Orb")]
    [InlineData("Зеркало Каландры", "Mirror of Kalandra")]
    public void TryTranslate_BaseItem_ResolvesViaCacheFallback(string ru, string expectedEn)
    {
        var translator = CreateTranslatorWithCache();
        Assert.Equal(expectedEn, translator.TryTranslate(ru, "rus"));
    }

    [Fact]
    public void TryTranslate_BaseItem_CaseInsensitiveViaCache()
    {
        var translator = CreateTranslatorWithCache();
        Assert.Equal("Chaos Orb", translator.TryTranslate("СФЕРА ХАОСА", "rus"));
        Assert.Equal("Chaos Orb", translator.TryTranslate("сфера хаоса", "rus"));
    }

    // ---- Приоритет [1] > [2]: рунные комбинации резолвятся через RuneshapeCombinationTranslator ----

    [Theory]
    [InlineData("Руна огня", "Fire Rune")]
    [InlineData("Мастерская руна", "Masterwork Rune")]
    public void TryTranslate_RuneshapeItem_PriorityOverCache(string ru, string expectedEn)
    {
        // Даже если бы «Руна огня» была в cache (её там нет), [1] должен сработать раньше.
        var translator = CreateTranslatorWithCache();
        Assert.Equal(expectedEn, translator.TryTranslate(ru, "rus"));
    }

    // ---- Graceful degradation: cache пустой или null ----

    [Fact]
    public void TryTranslate_BaseItem_WithNullCache_ReturnsNull()
    {
        // cache=null → fallback [2] пропущен → null (рунных комбинаций для этих имён нет).
        var translator = CreateTranslatorWithoutCache();
        Assert.Null(translator.TryTranslate("Сфера хаоса", "rus"));
        Assert.Null(translator.TryTranslate("Зеркало Каландры", "rus"));
    }

    [Fact]
    public void TryTranslate_BaseItem_WithEmptyCache_ReturnsNull()
    {
        // Empty cache (rus.ndjson не bundled — KI-017) → fallback [2] промахивается → null.
        // Но рунные комбинации продолжают работать через [1].
        var translator = new ItemNameTranslator(new RuneshapeCombinationTranslator(), new TranslationCache());
        Assert.Null(translator.TryTranslate("Сфера хаоса", "rus"));
        Assert.Equal("Fire Rune", translator.TryTranslate("Руна огня", "rus"));
    }

    // ---- Unknown items: null regardless of cache ----

    [Theory]
    [InlineData("Несуществующий предмет")]
    [InlineData("Руна суперсилы")]
    public void TryTranslate_UnknownItem_ReturnsNull(string input)
    {
        var translator = CreateTranslatorWithCache();
        Assert.Null(translator.TryTranslate(input, "rus"));
    }

    // ---- Eng client: passthrough, cache не используется ----

    [Fact]
    public void TryTranslate_EngLanguage_ReturnsAsIs_IgnoreCache()
    {
        var translator = CreateTranslatorWithCache();
        Assert.Equal("Chaos Orb", translator.TryTranslate("Chaos Orb", "eng"));
        Assert.Equal("Anything", translator.TryTranslate("Anything", "eng"));
    }
}
