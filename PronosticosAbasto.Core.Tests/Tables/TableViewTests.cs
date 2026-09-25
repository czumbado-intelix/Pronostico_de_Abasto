using PronosticosAbasto.Core.Tables;

namespace PronosticosAbasto.Core.Tests.Tables;

public class TableViewTests
{
    private sealed record Row(string Article, string Status, decimal? Coverage);

    /// <summary>Filtro minimo: deja pasar lo que contenga <see cref="Contains"/>.</summary>
    private sealed class FakeFilter : IColumnValueFilter
    {
        public string? Contains { get; set; }

        public List<string?> LastOptions { get; } = [];

        public bool IsActive => !string.IsNullOrEmpty(Contains);

        public bool Matches(string? value) =>
            string.IsNullOrEmpty(Contains) ||
            (value ?? string.Empty).Contains(Contains, StringComparison.OrdinalIgnoreCase);

        public void SetOptions(IEnumerable<string?> values)
        {
            LastOptions.Clear();
            LastOptions.AddRange(values);
        }

        public void ClearFilter() => Contains = null;
    }

    private static readonly Row[] Rows =
    [
        new("B-100", "Rojo", 2m),
        new("A-200", "Verde", null),
        new("C-300", "Rojo", 1m),
    ];

    private static (TableView<Row> View, FakeFilter Article, FakeFilter Status, FakeFilter Coverage) Build()
    {
        var article = new FakeFilter();
        var status = new FakeFilter();
        var coverage = new FakeFilter();
        var view = new TableView<Row>(
            new TableColumn<Row>("Article", row => row.Article, article),
            new TableColumn<Row>("Status", row => row.Status, status),
            new TableColumn<Row>("Coverage", row => row.Coverage?.ToString("0") ?? "—", coverage, row => row.Coverage));
        return (view, article, status, coverage);
    }

    [Fact]
    public void Duplicate_column_keys_are_rejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => new TableView<Row>(
            new TableColumn<Row>("Article", row => row.Article, new FakeFilter()),
            new TableColumn<Row>("article", row => row.Status, new FakeFilter())));

        Assert.Contains("Article", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_row_passes_only_when_every_column_filter_passes()
    {
        var (view, article, status, _) = Build();
        article.Contains = "00";
        status.Contains = "Rojo";

        Assert.Equal(new[] { "B-100", "C-300" }, view.Apply(Rows).Select(row => row.Article));
    }

    [Fact]
    public void Without_filters_every_row_passes_in_the_original_order()
    {
        var (view, _, _, _) = Build();

        Assert.Equal(Rows.Select(row => row.Article), view.Apply(Rows).Select(row => row.Article));
    }

    [Fact]
    public void RefreshOptions_feeds_each_column_its_own_cell_text()
    {
        var (view, article, status, coverage) = Build();

        view.RefreshOptions(Rows);

        Assert.Equal(new[] { "B-100", "A-200", "C-300" }, article.LastOptions);
        Assert.Equal(new[] { "Rojo", "Verde", "Rojo" }, status.LastOptions);
        Assert.Equal(new[] { "2", "—", "1" }, coverage.LastOptions);
    }

    [Fact]
    public void AnyFilterActive_reflects_the_columns()
    {
        var (view, article, _, _) = Build();
        Assert.False(view.AnyFilterActive);

        article.Contains = "A";
        Assert.True(view.AnyFilterActive);

        view.ClearFilters();
        Assert.False(view.AnyFilterActive);
    }

    [Fact]
    public void Sorting_is_requested_by_filter_instance_not_by_a_string_key()
    {
        var (view, article, _, _) = Build();

        Assert.True(view.TrySetSort(article, ascending: true));
        Assert.Equal("Article", view.SortColumn!.Key);
        Assert.Equal(new[] { "A-200", "B-100", "C-300" }, view.Apply(Rows).Select(row => row.Article));
    }

    [Fact]
    public void A_filter_from_another_table_is_rejected_instead_of_sorting_the_wrong_column()
    {
        var (view, _, _, _) = Build();

        Assert.False(view.TrySetSort(new FakeFilter(), ascending: true));
        Assert.Null(view.SortColumn);
    }

    [Fact]
    public void Text_sorting_ignores_case()
    {
        var filter = new FakeFilter();
        var view = new TableView<Row>(new TableColumn<Row>("Article", row => row.Article, filter));
        var rows = new[] { new Row("b", "", null), new Row("A", "", null), new Row("a", "", null) };

        view.TrySetSort(filter, ascending: true);

        Assert.Equal(new[] { "A", "a", "b" }, view.Apply(rows).Select(row => row.Article));
    }

    [Fact]
    public void Descending_text_sort_reverses_the_order()
    {
        var (view, article, _, _) = Build();

        view.TrySetSort(article, ascending: false);

        Assert.Equal(new[] { "C-300", "B-100", "A-200" }, view.Apply(Rows).Select(row => row.Article));
    }

    [Fact]
    public void Numeric_columns_sort_by_number_not_by_text()
    {
        var filter = new FakeFilter();
        var view = new TableView<Row>(
            new TableColumn<Row>("Coverage", row => row.Coverage?.ToString("0") ?? "—", filter, row => row.Coverage));
        var rows = new[] { new Row("a", "", 10m), new Row("b", "", 9m), new Row("c", "", 100m) };

        view.TrySetSort(filter, ascending: true);

        // Por texto quedaria 10, 100, 9.
        Assert.Equal(new decimal?[] { 9m, 10m, 100m }, view.Apply(rows).Select(row => row.Coverage));
    }

    [Fact]
    public void Rows_without_a_numeric_value_stay_last_ascending_and_descending()
    {
        var (view, _, _, coverage) = Build();

        view.TrySetSort(coverage, ascending: true);
        Assert.Equal(new decimal?[] { 1m, 2m, null }, view.Apply(Rows).Select(row => row.Coverage));

        view.TrySetSort(coverage, ascending: false);
        Assert.Equal(new decimal?[] { 2m, 1m, null }, view.Apply(Rows).Select(row => row.Coverage));
    }

    [Fact]
    public void ClearSort_goes_back_to_the_original_order()
    {
        var (view, article, _, _) = Build();
        view.TrySetSort(article, ascending: true);

        view.ClearSort();

        Assert.Equal(Rows.Select(row => row.Article), view.Apply(Rows).Select(row => row.Article));
    }

    [Fact]
    public void ClearFilters_does_not_clear_the_sort()
    {
        var (view, article, _, _) = Build();
        article.Contains = "A";
        view.TrySetSort(article, ascending: false);

        view.ClearFilters();

        Assert.Equal("Article", view.SortColumn!.Key);
        Assert.Equal(3, view.Apply(Rows).Count);
    }

    [Fact]
    public void Filtering_happens_before_sorting()
    {
        var (view, article, status, _) = Build();
        status.Contains = "Rojo";
        view.TrySetSort(article, ascending: true);

        Assert.Equal(new[] { "B-100", "C-300" }, view.Apply(Rows).Select(row => row.Article));
    }

    [Fact]
    public void ColumnFor_finds_the_owner_of_a_filter()
    {
        var (view, _, status, _) = Build();

        Assert.Equal("Status", view.ColumnFor(status)!.Key);
        Assert.Null(view.ColumnFor(new FakeFilter()));
    }
}
