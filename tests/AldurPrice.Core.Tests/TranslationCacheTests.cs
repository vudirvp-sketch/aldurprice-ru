using System.IO;
using System.Text;
using AldurPrice.Core.Translation;
using Xunit;

namespace AldurPrice.Core.Tests;

/// <summary>
/// Тесты <see cref="TranslationCache"/>: загрузка NDJSON (формат Exiled Exchange 2),
/// точный lookup (OrdinalIgnoreCase), Store/Clear, edge-cases.
///
/// <para>Использует inline NDJSON (одна JSON-запись на строку) — не зависит от
/// bundled <c>rus.ndjson</c> (который может быть не загружен — KI-017).</para>
/// </summary>
public sealed class TranslationCacheTests
{
    /// <summary>Inline NDJSON для тестов парсинга. Формат зеркалирует Exiled Exchange 2:
    /// 5 валидных пар + 1 name==refName (skip) + 1 empty name (skip) + 1 malformed (skip).
    /// Поля icon/tags/craftable/w/h/gem намеренно варьируются — парсер должен брать только name+refName.</summary>
    private const string InlineNdjson = """
        {"name":"Подчиняющая порча","refName":"Abiding Hex","namespace":"GEM","icon":"https://web.poecdn.com/x","tags":[],"tradeTag":"abiding-hex","craftable":{"category":"Support Skill Gem"},"w":1,"h":1,"gem":{"awakened":false,"transfigured":false}}
        {"name":"Сфера хаоса","refName":"Chaos Orb","namespace":"CURRENCY","icon":"https://web.poecdn.com/y","tags":[],"tradeTag":"chaos-orb","w":1,"h":1}
        {"name":"Зеркало Каландры","refName":"Mirror of Kalandra","namespace":"CURRENCY","tags":[],"w":1,"h":1}
        {"name":"Древний жезл","refName":"Ancient Wand","namespace":"WEAPON","tags":["staff"],"tradeTag":"ancient-wand","w":1,"h":3}
        {"name":"Простой предмет","refName":"Plain Item","namespace":"ITEM","w":2,"h":2}
        {"name":"Exalted Orb","refName":"Exalted Orb","namespace":"CURRENCY","w":1,"h":1}
        {"name":"","refName":"Empty Name","namespace":"ITEM","w":1,"h":1}
        this is not json at all
        """;

    private static TranslationCache CreateFromInline() =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(InlineNdjson)));

    [Fact]
    public void StreamConstructor_LoadsValidEntries_SkipsInvalid()
    {
        var cache = CreateFromInline();
        // 5 валидных пар (1-5). Строки 6 (name==refName), 7 (empty name), 8 (malformed) пропущены.
        Assert.Equal(5, cache.Count);
    }

    [Fact]
    public void StreamConstructor_EmptyStream_Throws()
    {
        Assert.ThrowsAny<Exception>(() => new TranslationCache(new MemoryStream()));
    }

    [Fact]
    public void StreamConstructor_OnlyWhitespaceLines_Throws()
    {
        var ws = "  \n\n   \t\n";
        Assert.ThrowsAny<Exception>(() =>
            new TranslationCache(new MemoryStream(Encoding.UTF8.GetBytes(ws))));
    }

    [Fact]
    public void StreamConstructor_OnlyNameEqualsRefName_Throws()
    {
        // Все записи где name==refName — нет валидных пар → InvalidDataException.
        var ndjson = "{\"name\":\"Foo\",\"refName\":\"Foo\"}\n{\"name\":\"Bar\",\"refName\":\"Bar\"}\n";
        Assert.ThrowsAny<Exception>(() =>
            new TranslationCache(new MemoryStream(Encoding.UTF8.GetBytes(ndjson))));
    }

    [Fact]
    public void StreamConstructor_MalformedLine_SkippedValidLinesLoaded()
    {
        var ndjson = """
            {"name":"Первый","refName":"First"}
            not json
            {"name":"Третий","refName":"Third"}
            """;
        var cache = new TranslationCache(new MemoryStream(Encoding.UTF8.GetBytes(ndjson)));
        Assert.Equal(2, cache.Count);
        Assert.Equal("First", cache.TryLookup("Первый"));
        Assert.Equal("Third", cache.TryLookup("Третий"));
    }

    [Fact]
    public void TryLookup_ExactMatch_ReturnsRefName()
    {
        var cache = CreateFromInline();
        Assert.Equal("Abiding Hex", cache.TryLookup("Подчиняющая порча"));
        Assert.Equal("Chaos Orb", cache.TryLookup("Сфера хаоса"));
    }

    [Fact]
    public void TryLookup_CaseInsensitive()
    {
        var cache = CreateFromInline();
        Assert.Equal("Chaos Orb", cache.TryLookup("СФЕРА ХАОСА"));
        Assert.Equal("Chaos Orb", cache.TryLookup("сфера хаоса"));
    }

    [Fact]
    public void TryLookup_NoMatch_ReturnsNull()
    {
        var cache = CreateFromInline();
        Assert.Null(cache.TryLookup("Несуществующий предмет"));
        Assert.Null(cache.TryLookup("Exalted Orb"));  // name==refName был пропущен
    }

    [Fact]
    public void TryLookup_SkipsEmptyAndNameEqualsRefName()
    {
        var cache = CreateFromInline();
        // Строка с empty name не загружена.
        Assert.Null(cache.TryLookup("Empty Name"));
        // Строка с name==refName не загружена (нет перевода).
        Assert.Null(cache.TryLookup("Exalted Orb"));
    }

    [Fact]
    public void TryLookup_NullOrWhitespace_Throws()
    {
        var cache = CreateFromInline();
        Assert.ThrowsAny<ArgumentException>(() => cache.TryLookup(null!));
        Assert.ThrowsAny<ArgumentException>(() => cache.TryLookup(""));
        Assert.ThrowsAny<ArgumentException>(() => cache.TryLookup("   "));
    }

    [Fact]
    public void Store_AddsEntry_TryLookupFindsIt()
    {
        var cache = new TranslationCache();
        Assert.Equal(0, cache.Count);
        cache.Store("Новый предмет", "New Item");
        Assert.Equal(1, cache.Count);
        Assert.Equal("New Item", cache.TryLookup("Новый предмет"));
    }

    [Fact]
    public void Store_OverwritesExistingEntry()
    {
        var cache = new TranslationCache();
        cache.Store("Предмет", "Item v1");
        cache.Store("Предмет", "Item v2");
        Assert.Equal(1, cache.Count);
        Assert.Equal("Item v2", cache.TryLookup("Предмет"));
    }

    [Fact]
    public void Store_NullOrWhitespaceArgs_Throws()
    {
        var cache = new TranslationCache();
        Assert.ThrowsAny<ArgumentException>(() => cache.Store("", "Target"));
        Assert.ThrowsAny<ArgumentException>(() => cache.Store("Source", ""));
        Assert.ThrowsAny<ArgumentException>(() => cache.Store(null!, "Target"));
        Assert.ThrowsAny<ArgumentException>(() => cache.Store("Source", null!));
    }

    [Fact]
    public void Clear_EmptiesCache()
    {
        var cache = CreateFromInline();
        Assert.True(cache.Count > 0);
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.Null(cache.TryLookup("Подчиняющая порча"));
    }

    [Fact]
    public void LoadNdjson_AfterClear_Repopulates()
    {
        var cache = CreateFromInline();
        cache.Clear();
        Assert.Equal(0, cache.Count);
        cache.LoadNdjson(new MemoryStream(Encoding.UTF8.GetBytes(InlineNdjson)));
        Assert.Equal(5, cache.Count);
    }

    [Fact]
    public void LoadEmbeddedOrDefault_NeverThrowsRegardlessOfBundling()
    {
        // Environment-dependent: если rus.ndjson bundled (пользователь запустил
        // update-translations.py) → populated cache (4319 entries). Если не bundled
        // (KI-017, default) → пустой кэш. В любом случае метод не должен бросать.
        // Не assertим Count — он зависит от состояния репозитория.
        var cache = TranslationCache.LoadEmbeddedOrDefault();
        Assert.True(cache.Count >= 0);  // тривиально, но фиксирует «не упало, вернуло кэш»
    }

    [Fact]
    public void DefaultConstructor_CreatesEmptyCache()
    {
        var cache = new TranslationCache();
        Assert.Equal(0, cache.Count);
        Assert.Null(cache.TryLookup("Что угодно"));
    }
}
