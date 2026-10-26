using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Tests.Analysis;

public sealed class ExpeditionNumberFormatterTests
{
    [Fact]
    public void ExtractNumbers_splits_deduplicates_and_preserves_order_for_multiple_expeditions()
    {
        var numbers = ExpeditionNumberFormatter.ExtractNumbers(
            "2800000280; 2800000281; 2800000280; 2800000282; 2800000283; 2800000284; 2800000285");

        Assert.Equal(
            ["2800000280", "2800000281", "2800000282", "2800000283", "2800000284", "2800000285"],
            numbers);
        Assert.Equal("2800000280", ExpeditionNumberFormatter.FormatPrimary(numbers));
        Assert.Equal("+5", ExpeditionNumberFormatter.FormatExtraBadge(numbers));
        Assert.Equal("6 expediciones asignadas", ExpeditionNumberFormatter.FormatCountTitle(numbers));
        Assert.Equal("Copiar las 6 expediciones", ExpeditionNumberFormatter.FormatCopyButtonText(numbers));
        Assert.Equal(
            string.Join(Environment.NewLine, numbers),
            ExpeditionNumberFormatter.FormatMultiline(numbers));
    }

    [Fact]
    public void Formatters_handle_single_expedition_without_extra_badge()
    {
        var numbers = ExpeditionNumberFormatter.ExtractNumbers("2000018897");

        Assert.Equal(["2000018897"], numbers);
        Assert.Equal("2000018897", ExpeditionNumberFormatter.FormatPrimary(numbers));
        Assert.Equal(string.Empty, ExpeditionNumberFormatter.FormatExtraBadge(numbers));
        Assert.Equal("1 expedición asignada", ExpeditionNumberFormatter.FormatCountTitle(numbers));
        Assert.Equal("Copiar expedición", ExpeditionNumberFormatter.FormatCopyButtonText(numbers));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sin expedicion")]
    [InlineData("Sin expedición")]
    public void ExtractNumbers_returns_empty_for_missing_or_placeholder_values(string? rawValue)
    {
        var numbers = ExpeditionNumberFormatter.ExtractNumbers(rawValue);

        Assert.Empty(numbers);
        Assert.Equal("Sin expedicion", ExpeditionNumberFormatter.FormatPrimary(numbers));
        Assert.Equal(string.Empty, ExpeditionNumberFormatter.FormatExtraBadge(numbers));
        Assert.Equal("Sin expedición asignada", ExpeditionNumberFormatter.FormatCountTitle(numbers));
        Assert.Equal("Sin expedicion", ExpeditionNumberFormatter.FormatMultiline(numbers));
    }
}
