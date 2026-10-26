using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Core.Tests.IO;

public class ExpedicionesWorkbookParserTests
{
    [Fact]
    public void Parse_reads_expedition_lines_without_aggregating_duplicates()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("EXPEDICIONES");
            sheet.Cell("A1").Value = "ARTICULO";
            sheet.Cell("B1").Value = "DESCRIPCION";
            sheet.Cell("C1").Value = "CANTIDAD";
            sheet.Cell("A2").Value = "100002820";
            sheet.Cell("B2").Value = "Arena gato 10 kg";
            sheet.Cell("C2").Value = 96;
            sheet.Cell("A3").Value = "100002820";
            sheet.Cell("B3").Value = "Arena gato 10 kg";
            sheet.Cell("C3").Value = 96;
            sheet.Cell("A4").Value = "";
            sheet.Cell("C4").Value = 5;
        });

        var result = parser.Parse(stream);

        // The duplicate article is kept as two separate lines; the blank row is skipped.
        Assert.Equal(2, result.Lines.Count);
        Assert.All(result.Lines, line => Assert.Equal("100002820", line.Article));
        Assert.Equal("Arena gato 10 kg", result.Lines[0].Description);
        Assert.Equal(96, result.Lines[0].Quantity);
    }

    [Fact]
    public void Parse_picks_expediciones_sheet_even_when_an_inventory_sheet_shares_the_file()
    {
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var expediciones = workbook.AddWorksheet("EXPEDICIONES");
            expediciones.Cell("A1").Value = "ARTICULO";
            expediciones.Cell("B1").Value = "DESCRIPCION";
            expediciones.Cell("C1").Value = "CANTIDAD";
            expediciones.Cell("A2").Value = "555";
            expediciones.Cell("B2").Value = "Foo";
            expediciones.Cell("C2").Value = 10;

            var inventory = workbook.AddWorksheet("inventario");
            inventory.Cell("A1").Value = "Articulo";
            inventory.Cell("B1").Value = "Zona Almacenaje";
            inventory.Cell("C1").Value = "Cantidad Unidades";
            inventory.Cell("A2").Value = "555";
            inventory.Cell("B2").Value = "ZA15";
            inventory.Cell("C2").Value = 4;
        });

        // Expediciones parser must read the EXPEDICIONES sheet (demand), not the inventory one.
        var expediciones = new ExpedicionesWorkbookParser().Parse(stream);
        var line = Assert.Single(expediciones.Lines);
        Assert.Equal("555", line.Article);
        Assert.Equal(10, line.Quantity);

        // The SAME file feeds the inventory parser, which picks the inventory sheet.
        var inventory = new InventoryWorkbookParser().Parse(stream);
        var position = Assert.Single(inventory.Positions);
        Assert.Equal("ZA15", position.StorageZone);
        Assert.Equal(4, position.Quantity);
    }

    [Fact]
    public void Parse_reads_report_with_detail_header_below_dispatch_summary()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell("A1").Value = "Id Compania";
            sheet.Cell("D1").Value = "Expedicion";
            sheet.Cell("A2").Value = "0029";
            sheet.Cell("D2").Value = "2000017658";

            sheet.Cell("B4").Value = "Linea";
            sheet.Cell("C4").Value = "Articulo Cliente";
            sheet.Cell("D4").Value = "Articulo";
            sheet.Cell("E4").Value = "Descripcion";
            sheet.Cell("G4").Value = "Pedidas";
            sheet.Cell("O4").Value = "Cantidad pedida presentacion minima";

            sheet.Cell("B5").Value = 100;
            sheet.Cell("D5").Value = "100021561";
            sheet.Cell("E5").Value = "AZULEJO FLAX GRIS CLAR 30X60 CM 1.8 M2";
            sheet.Cell("G5").Value = "999 UD";
            sheet.Cell("O5").Value = 48;
        });

        var result = parser.Parse(stream);

        var line = Assert.Single(result.Lines);
        Assert.Equal("100021561", line.Article);
        Assert.Equal("AZULEJO FLAX GRIS CLAR 30X60 CM 1.8 M2", line.Description);
        Assert.Equal(48, line.Quantity);
    }

    [Fact]
    public void Parse_skips_repeated_dispatch_summaries_inside_the_report()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell("A1").Value = "Id Compania";
            sheet.Cell("D1").Value = "Expedicion";
            sheet.Cell("A2").Value = "0029";
            sheet.Cell("D2").Value = "2000017658";

            sheet.Cell("B4").Value = "Linea";
            sheet.Cell("D4").Value = "Articulo";
            sheet.Cell("E4").Value = "Descripcion";
            sheet.Cell("O4").Value = "Cantidad pedida presentacion minima";
            sheet.Cell("B5").Value = 100;
            sheet.Cell("D5").Value = "100021561";
            sheet.Cell("E5").Value = "AZULEJO FLAX";
            sheet.Cell("O5").Value = 48;

            sheet.Cell("A8").Value = "Id Compania";
            sheet.Cell("D8").Value = "Expedicion";
            sheet.Cell("O8").Value = "Clasificacion 1";
            sheet.Cell("A9").Value = "0029";
            sheet.Cell("D9").Value = "2000017659";

            sheet.Cell("B11").Value = "Linea";
            sheet.Cell("D11").Value = "Articulo";
            sheet.Cell("E11").Value = "Descripcion";
            sheet.Cell("O11").Value = "Cantidad pedida presentacion minima";
            sheet.Cell("B12").Value = 50;
            sheet.Cell("D12").Value = "100020523";
            sheet.Cell("E12").Value = "CANASTA PLASTICA";
            sheet.Cell("O12").Value = 144;
        });

        var result = parser.Parse(stream);

        Assert.Collection(
            result.Lines,
            line =>
            {
                Assert.Equal("100021561", line.Article);
                Assert.Equal(48, line.Quantity);
            },
            line =>
            {
                Assert.Equal("100020523", line.Article);
                Assert.Equal(144, line.Quantity);
            });
    }

    [Fact]
    public void Parse_reads_quantity_with_unit_when_only_pedidas_is_available()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("EXPEDICIONES");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "Descripcion";
            sheet.Cell("C1").Value = "Pedidas";
            sheet.Cell("A2").Value = "100020523";
            sheet.Cell("B2").Value = "CANASTA PLASTICA 25X20X10.5 CM";
            sheet.Cell("C2").Value = "144 UD";
        });

        var result = parser.Parse(stream);

        var line = Assert.Single(result.Lines);
        Assert.Equal("100020523", line.Article);
        Assert.Equal(144, line.Quantity);
    }

    [Fact]
    public void Parse_reads_simplified_report_with_expedition_number()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Hoja1");
            sheet.Cell("A1").Value = "expedicion";
            sheet.Cell("B1").Value = "articulo";
            sheet.Cell("C1").Value = "descripcion";
            sheet.Cell("D1").Value = "cantidad";
            sheet.Cell("A2").Value = 2800000236;
            sheet.Cell("B2").Value = "100003046";
            sheet.Cell("C2").Value = "EXTRACTOR BANO E-100 G 10 CM";
            sheet.Cell("D2").Value = 5;
        });

        var result = parser.Parse(stream);

        var line = Assert.Single(result.Lines);
        Assert.Equal("2800000236", line.ExpeditionNumber);
        Assert.Equal("100003046", line.Article);
        Assert.Equal("EXTRACTOR BANO E-100 G 10 CM", line.Description);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(5, line.RequestedQuantity);
        Assert.Equal(0, line.PreparedQuantity);
    }

    [Fact]
    public void Parse_reads_only_missing_quantity_from_full_report()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell("D1").Value = "Expedicion";
            sheet.Cell("G1").Value = "Situacion";
            sheet.Cell("D2").Value = "2000017658";
            sheet.Cell("G2").Value = "GENE";

            sheet.Cell("B4").Value = "Linea";
            sheet.Cell("D4").Value = "Articulo";
            sheet.Cell("E4").Value = "Descripcion";
            sheet.Cell("O4").Value = "Cantidad pedida presentacion minima";
            sheet.Cell("P4").Value = "Cantidad preparada presentacion minima";

            sheet.Cell("D5").Value = "100021561";
            sheet.Cell("E5").Value = "AZULEJO FLAX";
            sheet.Cell("O5").Value = 48;
            sheet.Cell("P5").Value = 48;

            sheet.Cell("D6").Value = "100004213";
            sheet.Cell("E6").Value = "INODORO SIENIA";
            sheet.Cell("O6").Value = 16;
            sheet.Cell("P6").Value = 1;

            sheet.Cell("D7").Value = "100021110";
            sheet.Cell("E7").Value = "HIELERA MARINE";
            sheet.Cell("O7").Value = 6;
        });

        var result = parser.Parse(stream);

        Assert.Collection(
            result.Lines,
            line =>
            {
                Assert.Equal("2000017658", line.ExpeditionNumber);
                Assert.Equal("100004213", line.Article);
                Assert.Equal(15, line.Quantity);
                Assert.Equal(16, line.RequestedQuantity);
                Assert.Equal(1, line.PreparedQuantity);
            },
            line =>
            {
                Assert.Equal("2000017658", line.ExpeditionNumber);
                Assert.Equal("100021110", line.Article);
                Assert.Equal(6, line.Quantity);
                Assert.Equal(6, line.RequestedQuantity);
                Assert.Equal(0, line.PreparedQuantity);
            });
    }

    [Fact]
    public void Parse_skips_cancelled_anul_expeditions()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell("D1").Value = "Expedicion";
            sheet.Cell("G1").Value = "Situacion";
            sheet.Cell("D2").Value = "2000017658";
            sheet.Cell("G2").Value = "ANUL";

            sheet.Cell("D4").Value = "Articulo";
            sheet.Cell("E4").Value = "Descripcion";
            sheet.Cell("O4").Value = "Cantidad pedida presentacion minima";
            sheet.Cell("P4").Value = "Cantidad preparada presentacion minima";

            sheet.Cell("D5").Value = "100021110";
            sheet.Cell("E5").Value = "HIELERA MARINE";
            sheet.Cell("O5").Value = 6;
            sheet.Cell("P5").Value = 0;
        });

        var result = parser.Parse(stream);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Parse_reads_expedition_number_when_summary_header_starts_with_section_label_column()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Expediciones");

            // Block 1: new eFlow layout with leading "EXPEDICIÓN" section column in A,
            // real summary columns starting at B, and detail header immediately on row 3.
            sheet.Cell("A1").Value = "EXPEDICIÓN";
            sheet.Cell("B1").Value = "Id Compañía";
            sheet.Cell("D1").Value = "Nombre Cliente";
            sheet.Cell("E1").Value = "Expedición";
            sheet.Cell("H1").Value = "Situación";

            sheet.Cell("B2").Value = "0029";
            sheet.Cell("D2").Value = "T002 CURRI";
            sheet.Cell("E2").Value = "2000018897";
            sheet.Cell("H2").Value = "GENE";

            sheet.Cell("B3").Value = "Línea";
            sheet.Cell("D3").Value = "Artículo";
            sheet.Cell("E3").Value = "Descripción";
            sheet.Cell("O3").Value = "Cantidad pedida presentación mínima";
            sheet.Cell("P3").Value = "Cantidad preparada presentación mínima";

            sheet.Cell("B4").Value = 40;
            sheet.Cell("D4").Value = "30000107";
            sheet.Cell("E4").Value = "MANGA ANTICORTE MAXICUT 10 (XL) EL PAR";
            sheet.Cell("O4").Value = 12;

            // Block 2: cancelled expedition in the same new layout must still be skipped.
            sheet.Cell("A6").Value = "EXPEDICIÓN";
            sheet.Cell("B6").Value = "Id Compañía";
            sheet.Cell("E6").Value = "Expedición";
            sheet.Cell("H6").Value = "Situación";

            sheet.Cell("B7").Value = "0029";
            sheet.Cell("E7").Value = "2000018912";
            sheet.Cell("H7").Value = "ANUL";

            sheet.Cell("B8").Value = "Línea";
            sheet.Cell("D8").Value = "Artículo";
            sheet.Cell("E8").Value = "Descripción";
            sheet.Cell("O8").Value = "Cantidad pedida presentación mínima";
            sheet.Cell("P8").Value = "Cantidad preparada presentación mínima";

            sheet.Cell("B9").Value = 10;
            sheet.Cell("D9").Value = "30000390";
            sheet.Cell("E9").Value = "TARIMAS 1.20X1.20";
            sheet.Cell("O9").Value = 11;

            // Block 3: second active expedition in the same layout.
            sheet.Cell("A11").Value = "EXPEDICIÓN";
            sheet.Cell("B11").Value = "Id Compañía";
            sheet.Cell("E11").Value = "Expedición";
            sheet.Cell("H11").Value = "Situación";

            sheet.Cell("B12").Value = "0029";
            sheet.Cell("E12").Value = "2000018900";
            sheet.Cell("H12").Value = "GENE";

            sheet.Cell("B13").Value = "Línea";
            sheet.Cell("D13").Value = "Artículo";
            sheet.Cell("E13").Value = "Descripción";
            sheet.Cell("O13").Value = "Cantidad pedida presentación mínima";
            sheet.Cell("P13").Value = "Cantidad preparada presentación mínima";

            sheet.Cell("B14").Value = 20;
            sheet.Cell("D14").Value = "30000149";
            sheet.Cell("E14").Value = "CAMISETA ENTRENAMIENTO XXXL";
            sheet.Cell("O14").Value = 5;
            sheet.Cell("P14").Value = 3;
        });

        var result = parser.Parse(stream);

        Assert.Collection(
            result.Lines,
            line =>
            {
                Assert.Equal("2000018897", line.ExpeditionNumber);
                Assert.Equal("30000107", line.Article);
                Assert.Equal(12, line.Quantity);
                Assert.Equal("GENE", line.Situation);
            },
            line =>
            {
                Assert.Equal("2000018900", line.ExpeditionNumber);
                Assert.Equal("30000149", line.Article);
                Assert.Equal(2, line.Quantity);
                Assert.Equal("GENE", line.Situation);
            });
    }

    [Fact]
    public void Parse_reads_expedition_number_even_without_company_column_or_with_repeated_banner_text()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Expediciones");
            sheet.Cell("A1").Value = "EXPEDICIÓN";
            sheet.Cell("E1").Value = "Expedición";
            sheet.Cell("H1").Value = "Situación";

            // Even if A2 repeats the banner label, E2 carries the actual expedition number.
            sheet.Cell("A2").Value = "EXPEDICIÓN";
            sheet.Cell("E2").Value = "2800000281";
            sheet.Cell("H2").Value = "GENE";

            // And even if A3 in the detail header also carries a blank "EXPEDICIÓN" banner column,
            // detail lines fall back to the active summary expedition number.
            sheet.Cell("A3").Value = "EXPEDICIÓN";
            sheet.Cell("D3").Value = "Artículo";
            sheet.Cell("E3").Value = "Descripción";
            sheet.Cell("O3").Value = "Cantidad pedida presentación mínima";

            sheet.Cell("D4").Value = "100024553";
            sheet.Cell("E4").Value = "SET 2 GANCHOS CUADRADO METAL SOPORTA 5KG";
            sheet.Cell("O4").Value = 400;
        });

        var result = parser.Parse(stream);

        var line = Assert.Single(result.Lines);
        Assert.Equal("2800000281", line.ExpeditionNumber);
        Assert.Equal("100024553", line.Article);
        Assert.Equal(400, line.Quantity);
    }

    [Fact]
    public void Parse_throws_when_no_expediciones_columns_are_found()
    {
        var parser = new ExpedicionesWorkbookParser();
        using var stream = WorkbookTestFactory.Create(workbook =>
        {
            var sheet = workbook.AddWorksheet("Hoja");
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "Notas";
        });

        var exception = Assert.Throws<WorkbookValidationException>(() => parser.Parse(stream));

        Assert.Contains("expediciones", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
