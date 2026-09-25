using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Core.Tests.IO;

public class ForecastWorkbookParserTests
{
    [Fact]
    public void Parse_extracts_available_weeks_and_weekly_entries_from_header_columns()
    {
        var parser = new ForecastWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Forecast");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "01.06.26";
            sheet.Cell("C1").Value = new DateTime(2026, 6, 8);
            sheet.Cell("A2").Value = "A123";
            sheet.Cell("B2").Value = 50;
            sheet.Cell("C2").Value = 75;
        });

        var result = parser.Parse(stream);

        Assert.Equal(
            new[] { new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8) },
            result.AvailableWeeks);
        Assert.Collection(
            result.Entries.OrderBy(entry => entry.Week),
            entry =>
            {
                Assert.Equal("A123", entry.Article);
                Assert.Equal(new DateOnly(2026, 6, 1), entry.Week);
                Assert.Equal(50, entry.Quantity);
            },
            entry =>
            {
                Assert.Equal("A123", entry.Article);
                Assert.Equal(new DateOnly(2026, 6, 8), entry.Week);
                Assert.Equal(75, entry.Quantity);
            });
    }

    [Fact]
    public void Parse_accepts_real_forecast_headers_from_epa_example()
    {
        var parser = new ForecastWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Forecast");
            sheet.Cell("B1").Value = "Prod.";
            sheet.Cell("C1").Value = "01.06.2026";
            sheet.Cell("D1").Value = "08.06.2026";
            sheet.Cell("B2").Value = "100000263";
            sheet.Cell("C2").Value = "0,000";
            sheet.Cell("D2").Value = "12,000";
        });

        var result = parser.Parse(stream);

        Assert.Equal(
            new[] { new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8) },
            result.AvailableWeeks);
        var entry = Assert.Single(result.Entries);
        Assert.Equal("100000263", entry.Article);
        Assert.Equal(new DateOnly(2026, 6, 8), entry.Week);
        Assert.Equal(12, entry.Quantity);
    }

    [Fact]
    public void Parse_reads_cofersa_monthly_layout_with_header_on_third_row_and_item_code()
    {
        var parser = new ForecastWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Forecast");
            // Rows 1-2 are banners/metadata above the real header (Cofersa layout).
            sheet.Cell("F1").Value = "Final forecast";
            sheet.Cell("G2").Value = "661606";
            sheet.Cell("H2").Value = "670485";
            sheet.Cell("I2").Value = "666894";

            // Real header on row 3.
            sheet.Cell("B3").Value = "marca";
            sheet.Cell("C3").Value = "categoria";
            sheet.Cell("D3").Value = "clase_bdf";
            sheet.Cell("E3").Value = "Item code";
            sheet.Cell("F3").Value = "Description";
            sheet.Cell("G3").Value = "Jun 2026";
            sheet.Cell("H3").Value = "Jul 2026";
            sheet.Cell("I3").Value = "Aug 2026";

            sheet.Cell("A4").Value = 1;
            sheet.Cell("B4").Value = "DYLLU";
            sheet.Cell("E4").Value = "0002001";
            sheet.Cell("F4").Value = "Bomba manual";
            sheet.Cell("G4").Value = 2;
            sheet.Cell("H4").Value = 2;
            sheet.Cell("I4").Value = 3;

            sheet.Cell("A5").Value = 2;
            sheet.Cell("B5").Value = "BOSCH";
            sheet.Cell("E5").Value = "06019J85E1";
            sheet.Cell("F5").Value = "Llave de impacto";
            sheet.Cell("G5").Value = 1;
            sheet.Cell("H5").Value = 1;
            sheet.Cell("I5").Value = 1;
        });

        var result = parser.Parse(stream);

        Assert.Equal(ForecastGranularity.Monthly, result.Granularity);
        Assert.Equal(
            new[] { new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1) },
            result.AvailableWeeks);
        Assert.Contains(result.Entries, entry =>
            entry.Article == "0002001" && entry.Week == new DateOnly(2026, 6, 1) && entry.Quantity == 2);
        Assert.Contains(result.Entries, entry =>
            entry.Article == "0002001" && entry.Week == new DateOnly(2026, 8, 1) && entry.Quantity == 3);
        Assert.Contains(result.Entries, entry =>
            entry.Article == "06019J85E1" && entry.Week == new DateOnly(2026, 7, 1) && entry.Quantity == 1);
        Assert.Equal("Bomba manual", result.Descriptions["0002001"]);
        Assert.Equal("Llave de impacto", result.Descriptions["06019J85E1"]);
    }

    [Fact]
    public void Parse_marks_weekly_layout_granularity_as_weekly()
    {
        var parser = new ForecastWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Forecast");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "01.06.26";
            sheet.Cell("C1").Value = "08.06.26";
            sheet.Cell("A2").Value = "A123";
            sheet.Cell("B2").Value = 5;
            sheet.Cell("C2").Value = 5;
        });

        var result = parser.Parse(stream);

        Assert.Equal(ForecastGranularity.Weekly, result.Granularity);
    }

    [Fact]
    public void Parse_throws_validation_exception_when_no_week_columns_are_found()
    {
        var parser = new ForecastWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Forecast");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "Observaciones";
        });

        var exception = Assert.Throws<WorkbookValidationException>(() => parser.Parse(stream));

        Assert.Contains("semana", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
