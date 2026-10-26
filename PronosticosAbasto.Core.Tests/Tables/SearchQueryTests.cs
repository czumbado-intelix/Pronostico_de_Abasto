using PronosticosAbasto.Core.Tables;

namespace PronosticosAbasto.Core.Tests.Tables;

public class SearchQueryTests
{
    private static bool Finds(string query, params string[] fields) => SearchQuery.Parse(query).MatchesRaw(fields);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" , ;\n\t| ")]
    public void Blank_queries_match_everything(string? query)
    {
        var parsed = SearchQuery.Parse(query);

        Assert.True(parsed.IsEmpty);
        Assert.True(parsed.MatchesRaw("cualquier cosa"));
        Assert.True(parsed.Matches(string.Empty));
    }

    [Fact]
    public void Matching_ignores_case()
    {
        Assert.True(Finds("codo pvc", "CODO PVC 1/2"));
        Assert.True(Finds("CODO", "codo pvc"));
    }

    [Theory]
    [InlineData("articulo", "Artículo de prueba")]
    [InlineData("tamano", "TAMAÑO GRANDE")]
    [InlineData("tamaño", "TAMANO GRANDE")]
    [InlineData("pinguino", "Pingüino")]
    [InlineData("descripcion", "DESCRIPCIÓN")]
    public void Matching_ignores_accents_and_diaeresis(string query, string field) =>
        Assert.True(Finds(query, field));

    [Fact]
    public void Several_words_must_all_appear_in_any_order()
    {
        Assert.True(Finds("pvc codo", "CODO PVC 1/2"));
        Assert.True(Finds("1/2 codo", "CODO PVC 1/2"));
        Assert.False(Finds("pvc tee", "CODO PVC 1/2"));
    }

    [Fact]
    public void Words_can_come_from_different_fields_of_the_row()
    {
        // Articulo en un campo, descripcion en otro.
        Assert.True(Finds("100002040 sellador", "100002040", "Sellador acrilico"));
    }

    [Theory]
    [InlineData("codo, tee")]
    [InlineData("codo; tee")]
    [InlineData("codo\ntee")]
    [InlineData("codo\r\ntee")]
    [InlineData("codo\ttee")]
    [InlineData("codo | tee")]
    public void Separators_split_the_query_into_alternatives(string query)
    {
        var parsed = SearchQuery.Parse(query);

        Assert.Equal(2, parsed.AlternativeCount);
        Assert.True(parsed.MatchesRaw("CODO PVC"));
        Assert.True(parsed.MatchesRaw("TEE PVC"));
        Assert.False(parsed.MatchesRaw("UNION PVC"));
    }

    [Fact]
    public void Each_alternative_keeps_its_own_words_together()
    {
        var parsed = SearchQuery.Parse("codo pvc, tee cpvc");

        Assert.True(parsed.MatchesRaw("CODO PVC 1/2"));
        Assert.True(parsed.MatchesRaw("TEE CPVC 3/4"));
        Assert.False(parsed.MatchesRaw("CODO GALVANIZADO"));
        Assert.False(parsed.MatchesRaw("TEE"));
    }

    [Fact]
    public void A_pasted_list_of_codes_separated_by_spaces_is_a_list_of_alternatives()
    {
        var parsed = SearchQuery.Parse("100002040 100002041   100002050");

        Assert.Equal(3, parsed.AlternativeCount);
        Assert.True(parsed.MatchesRaw("100002040", "Sellador"));
        Assert.True(parsed.MatchesRaw("100002050", "Codo"));
        Assert.False(parsed.MatchesRaw("100002099", "Otro"));
    }

    [Fact]
    public void A_column_pasted_from_excel_works_too()
    {
        var parsed = SearchQuery.Parse("100002040\r\n100002041\r\n100002050\r\n");

        Assert.Equal(3, parsed.AlternativeCount);
        Assert.True(parsed.MatchesRaw("100002041"));
    }

    [Fact]
    public void Short_numbers_mixed_with_words_are_not_treated_as_a_code_list()
    {
        // "codo 1/2" y "tubo 3 4": numeros cortos o mezclados con palabras son
        // parte de la descripcion, no una lista de articulos.
        var parsed = SearchQuery.Parse("tubo 3 4");

        Assert.Equal(1, parsed.AlternativeCount);
        Assert.True(parsed.MatchesRaw("TUBO 3/4 X 4"));
        Assert.False(parsed.MatchesRaw("TUBO 1/2"));
    }

    [Fact]
    public void Two_short_numbers_are_still_words_not_codes()
    {
        var parsed = SearchQuery.Parse("12 34");

        Assert.Equal(1, parsed.AlternativeCount);
        Assert.True(parsed.MatchesRaw("ITEM 12 MEDIDA 34"));
        Assert.False(parsed.MatchesRaw("ITEM 12"));
    }

    [Fact]
    public void A_single_code_is_a_plain_contains_search()
    {
        var parsed = SearchQuery.Parse("10000204");

        Assert.Equal(1, parsed.AlternativeCount);
        Assert.True(parsed.MatchesRaw("100002040"));
        Assert.True(parsed.MatchesRaw("100002041"));
    }

    [Fact]
    public void Repeated_alternatives_and_words_are_collapsed()
    {
        var parsed = SearchQuery.Parse("100002040, 100002040 100002040; codo codo");

        Assert.Equal(2, parsed.AlternativeCount);
        Assert.Equal(new[] { "CODO" }, parsed.Alternatives[1]);
    }

    [Fact]
    public void Alternatives_are_exposed_normalized_and_in_order()
    {
        var parsed = SearchQuery.Parse("Tamaño grande, 100002040");

        Assert.Equal(new[] { "TAMANO", "GRANDE" }, parsed.Alternatives[0]);
        Assert.Equal(new[] { "100002040" }, parsed.Alternatives[1]);
    }

    [Theory]
    [InlineData("  Codo   PVC\t1/2 ", "CODO PVC 1/2")]
    [InlineData("Artículo", "ARTICULO")]
    [InlineData("ñandú", "NANDU")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize_uppercases_strips_accents_and_collapses_spaces(string? input, string expected) =>
        Assert.Equal(expected, SearchQuery.Normalize(input));
}
