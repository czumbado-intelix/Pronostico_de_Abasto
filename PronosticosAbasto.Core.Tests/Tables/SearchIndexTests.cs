using PronosticosAbasto.Core.Tables;

namespace PronosticosAbasto.Core.Tests.Tables;

public class SearchIndexTests
{
    private sealed class Row(string article, string description)
    {
        public string Article { get; } = article;

        public string Description { get; set; } = description;

        public int FieldReads { get; set; }
    }

    private static SearchIndex<Row> IndexOverArticleAndDescription() =>
        new(row =>
        {
            row.FieldReads++;
            return [row.Article, row.Description];
        });

    [Fact]
    public void Filter_keeps_matching_rows_in_their_original_order()
    {
        var index = IndexOverArticleAndDescription();
        var rows = new[]
        {
            new Row("100002041", "Codo PVC 1/2"),
            new Row("100002040", "Sellador"),
            new Row("100002042", "Codo CPVC"),
        };

        var found = index.Filter(rows, SearchQuery.Parse("codo")).Select(row => row.Article);

        Assert.Equal(new[] { "100002041", "100002042" }, found);
    }

    [Fact]
    public void An_empty_query_returns_every_row_without_building_keys()
    {
        var index = IndexOverArticleAndDescription();
        var rows = new[] { new Row("1", "a"), new Row("2", "b") };

        Assert.Equal(rows, index.Filter(rows, SearchQuery.Empty));
        Assert.All(rows, row => Assert.Equal(0, row.FieldReads));
    }

    [Fact]
    public void Each_row_is_normalized_once_no_matter_how_many_searches_run()
    {
        var index = IndexOverArticleAndDescription();
        var rows = Enumerable.Range(0, 50).Select(i => new Row($"10000{i:D4}", $"Articulo {i}")).ToArray();

        foreach (var query in new[] { "1", "10", "100", "1000", "10000", "articulo 4" })
        {
            _ = index.Filter(rows, SearchQuery.Parse(query)).ToArray();
        }

        Assert.All(rows, row => Assert.Equal(1, row.FieldReads));
    }

    [Fact]
    public void Invalidate_picks_up_an_edited_field()
    {
        var index = IndexOverArticleAndDescription();
        var row = new Row("100002040", "Sin comentario");
        var query = SearchQuery.Parse("no solicita");
        Assert.Empty(index.Filter([row], query));

        row.Description = "No solicita";
        Assert.Empty(index.Filter([row], query));

        index.Invalidate(row);
        Assert.Single(index.Filter([row], query));
    }

    [Fact]
    public void KeyFor_is_normalized()
    {
        var index = IndexOverArticleAndDescription();

        Assert.Equal("100002040 TAMANO GRANDE", index.KeyFor(new Row("100002040", "Tamaño  grande")).Text);
    }

    [Fact]
    public void UnmatchedAlternatives_lists_the_pasted_codes_that_are_not_there()
    {
        var index = IndexOverArticleAndDescription();
        var rows = new[] { new Row("100002040", "Sellador"), new Row("100002041", "Codo") };

        var missing = index.UnmatchedAlternatives(rows, SearchQuery.Parse("100002040 100002099 100002041 100002050"));

        Assert.Equal(new[] { "100002099", "100002050" }, missing);
    }

    [Fact]
    public void UnmatchedAlternatives_reports_multi_word_alternatives_whole()
    {
        var index = IndexOverArticleAndDescription();
        var rows = new[] { new Row("100002041", "Codo PVC") };

        Assert.Equal(new[] { "TEE PVC" }, index.UnmatchedAlternatives(rows, SearchQuery.Parse("codo pvc, tee pvc")));
    }

    [Fact]
    public void UnmatchedAlternatives_is_empty_for_an_empty_query()
    {
        var index = IndexOverArticleAndDescription();

        Assert.Empty(index.UnmatchedAlternatives([new Row("1", "a")], SearchQuery.Empty));
    }
}
