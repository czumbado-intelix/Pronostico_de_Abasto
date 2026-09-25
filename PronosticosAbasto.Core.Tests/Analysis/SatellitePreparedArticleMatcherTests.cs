using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Tests.Analysis;

public class SatellitePreparedArticleMatcherTests
{
    private static readonly HashSet<string> EmptyCatalog = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Catalog_article_is_prepared()
    {
        var catalog = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "100020614",
        };

        var isPrepared = SatellitePreparedArticleMatcher.IsPrepared(
            catalog,
            "100020614",
            "Articulo regular");

        Assert.True(isPrepared);
    }

    [Theory]
    [InlineData("RODAPIÉ PVC BLANCO 10CM")]
    [InlineData("Rodapie de madera")]
    [InlineData("PISO DECK 14X2.5X220CM GRIS")]
    [InlineData("PISO-DECK CAFE OSCURO")]
    [InlineData("DECK 14X2.5X220CM GRIS")]
    public void Rodapie_and_deck_descriptions_are_prepared(string description)
    {
        var isPrepared = SatellitePreparedArticleMatcher.IsPrepared(
            EmptyCatalog,
            "A1",
            description);

        Assert.True(isPrepared);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Comoda de MDP blanca")]
    [InlineData("Taladro Black Decker")]
    public void Unrelated_descriptions_are_not_prepared(string description)
    {
        var isPrepared = SatellitePreparedArticleMatcher.IsPrepared(
            EmptyCatalog,
            "A1",
            description);

        Assert.False(isPrepared);
    }
}
