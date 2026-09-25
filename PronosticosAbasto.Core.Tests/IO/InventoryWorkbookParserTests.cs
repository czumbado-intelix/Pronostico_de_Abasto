using ClosedXML.Excel;
using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Core.Tests.IO;

public class InventoryWorkbookParserTests
{
    [Fact]
    public void Parse_reads_inventory_rows_and_ignores_blank_articles()
    {
        var parser = new InventoryWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Inventario");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "Zona de almacenaje";
            sheet.Cell("C1").Value = "Cantidad unidades";
            sheet.Cell("A2").Value = "A123";
            sheet.Cell("B2").Value = "Zona A";
            sheet.Cell("C2").Value = 10;
            sheet.Cell("A3").Value = "A123";
            sheet.Cell("B3").Value = "Zona B";
            sheet.Cell("C3").Value = 5;
            sheet.Cell("A4").Value = "";
            sheet.Cell("B4").Value = "Zona C";
            sheet.Cell("C4").Value = 9;
        });

        var result = parser.Parse(stream);

        Assert.Collection(
            result.Positions,
            position =>
            {
                Assert.Equal("A123", position.Article);
                Assert.Equal("Zona A", position.StorageZone);
                Assert.Equal(10, position.Quantity);
            },
            position =>
            {
                Assert.Equal("A123", position.Article);
                Assert.Equal("Zona B", position.StorageZone);
                Assert.Equal(5, position.Quantity);
            });
    }

    [Fact]
    public void Parse_accepts_real_inventory_headers_from_epa_example()
    {
        var parser = new InventoryWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Inventario");
            sheet.Cell("A1").Value = "Artículo";
            sheet.Cell("B1").Value = "Zona Almacenaje";
            sheet.Cell("C1").Value = "Cantidad Unidades";
            sheet.Cell("D1").Value = "Zona";
            sheet.Cell("A2").Value = "100000594";
            sheet.Cell("B2").Value = "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO";
            sheet.Cell("C2").Value = 90;
            sheet.Cell("D2").Value = "SI";
        });

        var result = parser.Parse(stream);

        var position = Assert.Single(result.Positions);
        Assert.Equal("100000594", position.Article);
        Assert.Equal("ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", position.StorageZone);
        Assert.Equal(90, position.Quantity);
    }

    [Fact]
    public void Parse_skips_summary_sheet_and_reads_the_detail_sheet_cofersa_layout()
    {
        var parser = new InventoryWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            // First sheet is a pivot/summary that lacks the inventory columns.
            var pivot = workbook.AddWorksheet("Hoja1");
            pivot.Cell("A3").Value = "Suma de Cantidad";
            pivot.Cell("A4").Value = "Etiquetas de fila";
            pivot.Cell("B4").Value = "ZA47-ZONA ALMACENAJE";
            pivot.Cell("A5").Value = "2040505";
            pivot.Cell("B5").Value = 704;

            // Real detail sheet (second), EPA-style columns.
            var detail = workbook.AddWorksheet("INVENTARIO");
            detail.Cell("A1").Value = "Artículo";
            detail.Cell("B1").Value = "Zona Almacenaje";
            detail.Cell("C1").Value = "Cantidad Unidades";
            detail.Cell("D1").Value = "Descripción";
            detail.Cell("A2").Value = "0709031";
            detail.Cell("B2").Value = "ZA15-ZONA ALMACENAJE";
            detail.Cell("C2").Value = 6;
            detail.Cell("D2").Value = "Palin espadon";
        });

        var result = parser.Parse(stream);

        var position = Assert.Single(result.Positions);
        Assert.Equal("0709031", position.Article);
        Assert.Equal("ZA15-ZONA ALMACENAJE", position.StorageZone);
        Assert.Equal(6, position.Quantity);
        Assert.Equal("Palin espadon", result.Descriptions["0709031"]);
    }

    [Fact]
    public void Parse_reads_pallet_fields_and_excludes_merma_rows()
    {
        var parser = new InventoryWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("ReporteInventario");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "Descripcion";
            sheet.Cell("C1").Value = "Pallet";
            sheet.Cell("D1").Value = "Cantidad unidades";
            sheet.Cell("E1").Value = "Zona de almacenaje";
            sheet.Cell("F1").Value = "Fecha de validacion";
            sheet.Cell("G1").Value = "Tipo pallet";

            sheet.Cell("A2").Value = "M001";
            sheet.Cell("B2").Value = "Manguera";
            sheet.Cell("C2").Value = "PAL-OLD";
            sheet.Cell("D2").Value = 200;
            sheet.Cell("E2").Value = "ZA47";
            sheet.Cell("F2").Value = new DateTime(2026, 5, 1);
            sheet.Cell("G2").Value = "Disponible";

            sheet.Cell("A3").Value = "M001";
            sheet.Cell("B3").Value = "Manguera";
            sheet.Cell("C3").Value = "PAL-MERMA";
            sheet.Cell("D3").Value = 25;
            sheet.Cell("E3").Value = "ZA47";
            sheet.Cell("F3").Value = new DateTime(2026, 4, 1);
            sheet.Cell("G3").Value = "Merma";
        });

        var result = parser.Parse(stream);

        var position = Assert.Single(result.Positions);
        Assert.Equal("M001", position.Article);
        Assert.Equal("Manguera", position.Description);
        Assert.Equal("PAL-OLD", position.Pallet);
        Assert.Equal(200, position.Quantity);
        Assert.Equal("ZA47", position.StorageZone);
        Assert.Equal(new DateOnly(2026, 5, 1), position.ValidationDate);
        Assert.Equal("Disponible", position.PalletType);
        Assert.Equal("Manguera", result.Descriptions["M001"]);
    }

    [Fact]
    public void Parse_keeps_storage_zones_that_analysis_can_exclude_by_configuration()
    {
        var parser = new InventoryWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("ReporteInventario");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "Descripcion";
            sheet.Cell("C1").Value = "Pallet";
            sheet.Cell("D1").Value = "Cantidad unidades";
            sheet.Cell("E1").Value = "Zona de almacenaje";
            sheet.Cell("F1").Value = "Tipo pallet";

            sheet.Cell("A2").Value = "100004010";
            sheet.Cell("B2").Value = "SLEEPING BAG PAVILLO 180 X 75 CM";
            sheet.Cell("C2").Value = "22O0013013501";
            sheet.Cell("D2").Value = 12;
            sheet.Cell("E2").Value = "ZA30-ZONA ALMACENAJE DAÑADOS - CLIRO";
            sheet.Cell("F2").Value = "MULART";

            sheet.Cell("A3").Value = "100004010";
            sheet.Cell("B3").Value = "SLEEPING BAG PAVILLO 180 X 75 CM";
            sheet.Cell("C3").Value = "22O0006566722";
            sheet.Cell("D3").Value = 24;
            sheet.Cell("E3").Value = "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO";
            sheet.Cell("F3").Value = "MULART";
        });

        var result = parser.Parse(stream);

        Assert.Collection(
            result.Positions,
            position =>
            {
                Assert.Equal("100004010", position.Article);
                Assert.Equal("22O0013013501", position.Pallet);
                Assert.Equal(12, position.Quantity);
                Assert.Equal("ZA30-ZONA ALMACENAJE DAÑADOS - CLIRO", position.StorageZone);
            },
            position =>
            {
                Assert.Equal("100004010", position.Article);
                Assert.Equal("22O0006566722", position.Pallet);
                Assert.Equal(24, position.Quantity);
                Assert.Equal("ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", position.StorageZone);
            });
    }

    [Fact]
    public void Parse_throws_validation_exception_when_required_header_is_missing()
    {
        var parser = new InventoryWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Inventario");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "Zona de almacenaje";
        });

        var exception = Assert.Throws<WorkbookValidationException>(() => parser.Parse(stream));

        Assert.Contains("Cantidad unidades", exception.Message);
    }
}
