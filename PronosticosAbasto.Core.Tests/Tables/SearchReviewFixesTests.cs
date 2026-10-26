using System.Diagnostics;
using PronosticosAbasto.Core.Tables;

namespace PronosticosAbasto.Core.Tests.Tables;

/// <summary>
/// Casos que encontro la revision adversarial del buscador. Cada prueba fija un
/// comportamiento que antes fallaba con datos reales de abasto.
/// </summary>
public class SearchReviewFixesTests
{
    private sealed record Row(string Article, string Description, string Extra = "");

    private static SearchIndex<Row> Index() => new(row => [row.Article, row.Description, row.Extra]);

    private static string[] Articles(SearchResult<Row> result) => result.Rows.Select(row => row.Article).ToArray();

    // --- Coma decimal -------------------------------------------------------

    [Theory]
    [InlineData("2,5")]
    [InlineData("0,75")]
    [InlineData("1,250")]
    public void A_decimal_or_thousands_comma_does_not_split_the_query(string query)
    {
        var parsed = SearchQuery.Parse(query);

        Assert.Equal(1, parsed.AlternativeCount);
        Assert.True(parsed.MatchesRaw($"CABLE THHN {query}MM"));
        Assert.False(parsed.MatchesRaw("CABLE THHN 2 X 5"));
    }

    [Theory]
    [InlineData("100002040,100002041")]
    [InlineData("100002040, 100002041")]
    [InlineData("codo,tee")]
    public void A_comma_between_codes_or_words_still_splits(string query) =>
        Assert.Equal(2, SearchQuery.Parse(query).AlternativeCount);

    // --- Codigos como palabra completa dentro de una lista ------------------

    [Fact]
    public void A_pasted_code_does_not_match_a_longer_code_that_contains_it()
    {
        var rows = new[] { new Row("100002001", "Codo"), new Row("100002040", "Sellador") };

        var result = Index().Search(rows, SearchQuery.Parse("0002001\n100002040"));

        Assert.Equal(new[] { "100002040" }, Articles(result));
        Assert.Equal(new[] { "0002001" }, result.Unmatched);
    }

    [Fact]
    public void A_pasted_year_does_not_match_pallet_dates()
    {
        var rows = new[] { new Row("100002040", "Sellador", "P-77 2026-09-01") };

        var result = Index().Search(rows, SearchQuery.Parse("2026, 100002099"));

        Assert.Empty(result.Rows);
        Assert.Equal(new[] { "2026", "100002099" }, result.Unmatched);
    }

    [Fact]
    public void A_single_typed_partial_code_still_finds_everything_that_contains_it()
    {
        var rows = new[] { new Row("100002001", "Codo"), new Row("200002001", "Tee") };

        Assert.Equal(2, Index().Search(rows, SearchQuery.Parse("0002001")).Rows.Count);
    }

    // --- Codigos alfanumericos y la lectura "todas las palabras" ------------

    [Fact]
    public void Alphanumeric_codes_are_codes_too()
    {
        var parsed = SearchQuery.Parse("06019J85E1 100002040");

        Assert.Equal(2, parsed.AlternativeCount);
        Assert.True(parsed.MatchesRaw("06019J85E1", "Llave de impacto"));
    }

    [Fact]
    public void Two_codes_are_read_as_all_words_when_some_row_has_both()
    {
        // Expediciones: buscar ese articulo dentro de esa expedicion.
        var rows = new[]
        {
            new Row("100002040", "Sellador", "2000017658"),
            new Row("100002040", "Sellador", "2000099999"),
            new Row("100002041", "Codo", "2000017658"),
        };

        var result = Index().Search(rows, SearchQuery.Parse("2000017658 100002040"));

        Assert.Single(result.Rows);
        Assert.Equal(1, result.AppliedQuery.AlternativeCount);
        Assert.Empty(result.Unmatched);
    }

    [Fact]
    public void Two_codes_are_read_as_a_list_when_no_row_has_both()
    {
        var rows = new[] { new Row("100002040", "Sellador"), new Row("100002041", "Codo"), new Row("100002099", "Tee") };

        var result = Index().Search(rows, SearchQuery.Parse("100002040 100002041"));

        Assert.Equal(new[] { "100002040", "100002041" }, Articles(result));
        Assert.Equal(2, result.AppliedQuery.AlternativeCount);
    }

    [Fact]
    public void The_all_words_reading_only_applies_to_a_single_line_of_codes()
    {
        Assert.Null(SearchQuery.Parse("100002040, 100002041").AllWordsReading);
        Assert.Null(SearchQuery.Parse("codo pvc").AllWordsReading);
        Assert.NotNull(SearchQuery.Parse("100002040 100002041").AllWordsReading);
    }

    // --- Numeros cortos sueltos en una lista --------------------------------

    [Fact]
    public void A_short_quantity_pasted_with_a_row_is_dropped_from_the_list()
    {
        // Fila de Excel: articulo, descripcion, cantidad.
        var parsed = SearchQuery.Parse("100002040\tSELLADOR ACRILICO\t25");

        Assert.Equal(2, parsed.AlternativeCount);
        Assert.False(parsed.MatchesRaw("CODO 25 UNIDADES"));
    }

    [Fact]
    public void Short_numbers_are_kept_when_they_are_all_there_is()
    {
        var parsed = SearchQuery.Parse("2, 5");

        Assert.Equal(2, parsed.AlternativeCount);
    }

    // --- Caracteres invisibles ----------------------------------------------

    [Theory]
    [InlineData("100002040​ 100002041")]
    [InlineData("﻿100002040 100002041")]
    [InlineData("100002040 100002041")]
    public void Invisible_characters_from_teams_or_outlook_are_ignored(string query)
    {
        var parsed = SearchQuery.Parse(query);

        Assert.Equal(2, parsed.AlternativeCount);
        Assert.Equal(new[] { "100002040" }, parsed.Alternatives[0]);
    }

    [Fact]
    public void Normalize_drops_format_and_control_characters()
    {
        Assert.Equal("100002040", SearchQuery.Normalize("100​002⁠040\u0007"));
    }

    // --- Indice -------------------------------------------------------------

    [Fact]
    public void InvalidateAll_picks_up_bulk_edits()
    {
        var row = new MutableRow { Comment = "revisar" };
        var index = new SearchIndex<MutableRow>(r => [r.Comment]);
        var query = SearchQuery.Parse("revisar");
        Assert.Single(index.Filter([row], query));

        row.Comment = "no solicita";
        Assert.Single(index.Filter([row], query));

        index.InvalidateAll();
        Assert.Empty(index.Filter([row], query));
    }

    [Fact]
    public void Search_reports_matches_and_unmatched_in_the_order_they_were_written()
    {
        var rows = new[] { new Row("100002041", "Codo PVC"), new Row("100002040", "Sellador") };

        var result = Index().Search(rows, SearchQuery.Parse("100002099; sellador; 100002041; tee"));

        Assert.Equal(new[] { "100002041", "100002040" }, Articles(result));
        Assert.Equal(new[] { "100002099", "TEE" }, result.Unmatched);
    }

    [Fact]
    public void A_large_pasted_list_is_one_pass_over_the_rows()
    {
        var rows = Enumerable.Range(0, 30_000)
            .Select(i => new Row((100_000_000 + i).ToString(), $"Articulo {i}", $"P-{i} ZA{i % 50}"))
            .ToArray();
        var pasted = string.Join("\r\n", Enumerable.Range(0, 1_000).Select(i => (100_000_000 + (i * 29)).ToString()));
        var index = Index();
        _ = index.Search(rows, SearchQuery.Parse("calentar"));

        var stopwatch = Stopwatch.StartNew();
        var result = index.Search(rows, SearchQuery.Parse(pasted));
        stopwatch.Stop();

        Assert.Equal(1_000, result.Rows.Count);
        Assert.Empty(result.Unmatched);

        // Holgado a proposito: lo que importa es que no sea filas x codigos
        // (30 millones de comparaciones, varios segundos).
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Tardo {stopwatch.Elapsed.TotalMilliseconds:N0} ms");
    }

    private sealed class MutableRow
    {
        public string Comment { get; set; } = string.Empty;
    }
}
