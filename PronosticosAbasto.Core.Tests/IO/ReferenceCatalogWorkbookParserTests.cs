using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Core.Tests.IO;

public class ReferenceCatalogWorkbookParserTests
{
    [Fact]
    public void SatellitePreparedArticleWorkbookParser_reads_articles_and_descriptions()
    {
        var parser = new SatellitePreparedArticleWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Hoja1");
            sheet.Cell("A1").Value = "ARTICULO";
            sheet.Cell("B1").Value = "DESCRIPCION";
            sheet.Cell("A2").Value = "100002040";
            sheet.Cell("B2").Value = "Sellador";
            sheet.Cell("A3").Value = "";
            sheet.Cell("B3").Value = "Ignorar";
        });

        var result = parser.Parse(stream);

        var article = Assert.Single(result.Articles);
        Assert.Equal("100002040", article.Article);
        Assert.Equal("Sellador", article.Description);
    }

    [Fact]
    public void TarimaSizeWorkbookParser_normalizes_size_names_and_numbers()
    {
        var parser = new TarimaSizeWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Hoja1");
            sheet.Cell("A1").Value = "ARTICULO";
            sheet.Cell("B1").Value = "DESCRIPCION";
            sheet.Cell("C1").Value = "TAMAÑO TARIMA";
            sheet.Cell("A2").Value = "100002040";
            sheet.Cell("B2").Value = "Sellador";
            sheet.Cell("C2").Value = "DOBLE";
            sheet.Cell("A3").Value = "100002041";
            sheet.Cell("B3").Value = "Caja";
            sheet.Cell("C3").Value = "3";
        });

        var result = parser.Parse(stream);

        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("100002040", entry.Article);
                Assert.Equal("D", entry.SizeCode);
                Assert.Equal(2, entry.SizeNumber);
            },
            entry =>
            {
                Assert.Equal("100002041", entry.Article);
                Assert.Equal("T", entry.SizeCode);
                Assert.Equal(3, entry.SizeNumber);
            });
    }
}
