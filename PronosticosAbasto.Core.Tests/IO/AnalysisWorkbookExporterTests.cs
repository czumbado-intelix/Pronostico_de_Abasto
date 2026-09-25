using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using PronosticosAbasto.Core.Analysis;
using PronosticosAbasto.Core.IO;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PronosticosAbasto.Core.Tests.IO;

public class AnalysisWorkbookExporterTests
{
    [Fact]
    public void ExportWarehouseComparison_exports_visible_rows_with_comments()
    {
        var exporter = new AnalysisWorkbookExporter();
        var comparison = new WarehouseComparisonResult(
            new DateOnly(2026, 8, 31),
            new DateOnly(2026, 9, 28),
            5,
            new[]
            {
                new WarehouseComparisonRow(
                    "100",
                    "Articulo 100",
                    OloQuantity: 12,
                    ServicaQuantity: 24,
                    ForecastQuantity: 36,
                    CoveragePeriods: 5,
                    HasFutureForecast: true,
                    AutomaticComment: string.Empty),
                new WarehouseComparisonRow(
                    "200",
                    "Articulo 200",
                    OloQuantity: 10,
                    ServicaQuantity: 0,
                    ForecastQuantity: 0,
                    CoveragePeriods: null,
                    HasFutureForecast: false,
                    AutomaticComment: "no solicita"),
            });
        var visibleRows = comparison.Rows.Take(1).ToArray();
        var comments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["100"] = "ya solicitada tarima",
        };
        var histogramBuckets = new[]
        {
            new WarehouseComparisonHistogramBucket("0 sem", 1, 50m),
            new WarehouseComparisonHistogramBucket("1 sem", 0, 0m),
            new WarehouseComparisonHistogramBucket("2 sem", 1, 50m),
        };

        var bytes = exporter.ExportWarehouseComparison("EPA", comparison, visibleRows, comments, histogramBuckets);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Comparativa");
        var histogram = workbook.Worksheet("Histograma");

        Assert.Equal("Articulo", sheet.Cell("A1").GetString());
        Assert.Equal("Descripcion", sheet.Cell("B1").GetString());
        Assert.Equal("Inv OLO", sheet.Cell("C1").GetString());
        Assert.Equal("Inv Servica", sheet.Cell("D1").GetString());
        Assert.Equal("Semanas OLO", sheet.Cell("E1").GetString());
        Assert.Equal("Comentarios", sheet.Cell("F1").GetString());
        Assert.Equal("100", sheet.Cell("A2").GetString());
        Assert.Equal("Articulo 100", sheet.Cell("B2").GetString());
        Assert.Equal("12", sheet.Cell("C2").GetString());
        Assert.Equal("24", sheet.Cell("D2").GetString());
        Assert.Equal("5", sheet.Cell("E2").GetString());
        Assert.Equal("ya solicitada tarima", sheet.Cell("F2").GetString());
        Assert.True(sheet.Cell("A3").IsEmpty());

        Assert.Equal("Reporte de Distribucion de Articulos por Periodo", histogram.Cell("A1").GetString());
        Assert.Equal("Periodo", histogram.Cell("A3").GetString());
        Assert.Equal("Cantidad de articulos", histogram.Cell("B3").GetString());
        Assert.Equal("% del total", histogram.Cell("C3").GetString());
        Assert.Equal("0 sem", histogram.Cell("A4").GetString());
        Assert.Equal(1, histogram.Cell("B4").GetValue<int>());
        Assert.Equal(0.5m, histogram.Cell("C4").GetValue<decimal>());
        Assert.Equal("1 sem", histogram.Cell("A5").GetString());
        Assert.Equal(0, histogram.Cell("B5").GetValue<int>());
        Assert.Equal("2 sem", histogram.Cell("A6").GetString());
        Assert.Equal(1, histogram.Cell("B6").GetValue<int>());
        Assert.Equal("Total", histogram.Cell("A7").GetString());
        Assert.Equal(2, histogram.Cell("B7").GetValue<int>());

        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var workbookPart = document.WorkbookPart!;
        var histogramSheet = workbookPart.Workbook.Sheets!.Elements<S.Sheet>()
            .Single(sheet => sheet.Name == "Histograma");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(histogramSheet.Id!);
        var chartPart = Assert.Single(worksheetPart.DrawingsPart!.ChartParts);
        var formulas = chartPart.ChartSpace.Descendants<C.Formula>().Select(formula => formula.Text).ToArray();
        Assert.Contains("'Histograma'!$A$4:$A$6", formulas);
        Assert.Contains("'Histograma'!$B$4:$B$6", formulas);
    }

    [Fact]
    public void Export_creates_summary_results_and_zone_detail_sheets()
    {
        var exporter = new AnalysisWorkbookExporter();
        var result = new WeeklyAnalysisResult(
            new DateOnly(2026, 6, 8),
            new DateOnly(2026, 6, 8),
            new DateOnly(2026, 6, 29),
            requestedWeeks: 4,
            analyzedWeeks: 4,
            isIncompleteHorizon: false,
            new[]
            {
                new ArticleAnalysisResult(
                    "A123",
                    principalInventoryQuantity: 20,
                    totalInventoryQuantity: 30,
                    50,
                    -30,
                    CoverageStatus.Critical,
                    new[]
                    {
                        new ZoneInventoryDetail("Zona A", 10, isSatellite: true),
                        new ZoneInventoryDetail("Zona B", 10),
                    },
                    satelliteInventoryQuantity: 10,
                    satelliteDifference: -40,
                    satelliteStatus: CoverageStatus.Critical,
                    transferSuggestionQuantity: 10,
                    remainingShortageAfterTransfer: 20,
                    transferRecommendationState: TransferRecommendationState.Partial,
                    satelliteZones: new[]
                    {
                        new ZoneInventoryDetail("Zona A", 10, isSatellite: true),
                    }),
                new ArticleAnalysisResult(
                    "B456",
                    principalInventoryQuantity: 55,
                    totalInventoryQuantity: 59,
                    50,
                    5,
                    CoverageStatus.Healthy,
                    Array.Empty<ZoneInventoryDetail>(),
                    satelliteInventoryQuantity: 4,
                    satelliteDifference: -46,
                    satelliteStatus: CoverageStatus.Critical,
                    transferSuggestionQuantity: 0,
                    remainingShortageAfterTransfer: 0,
                    transferRecommendationState: TransferRecommendationState.NotRequired,
                    satelliteZones: Array.Empty<ZoneInventoryDetail>()),
            },
            criticalCount: 1,
            warningCount: 0,
            healthyCount: 1,
            satelliteCriticalCount: 2,
            satelliteWarningCount: 0,
            satelliteHealthyCount: 0);

        var preparedArticles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A123" };
        var tarimaSizes = new Dictionary<string, TarimaSizeEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["A123"] = new("A123", "Articulo A", "D"),
        };

        var bytes = exporter.Export("EPA", result, preparedArticles, tarimaSizes);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));

        Assert.NotNull(workbook.Worksheet("Resumen"));
        Assert.NotNull(workbook.Worksheet("Resultado"));
        Assert.NotNull(workbook.Worksheet("Detalle por zonas"));
        Assert.Equal("EPA", workbook.Worksheet("Resumen").Cell("B1").GetString());
        Assert.Equal("2026-06-08", workbook.Worksheet("Resumen").Cell("B2").GetString());
        Assert.Equal("2026-06-29", workbook.Worksheet("Resumen").Cell("B3").GetString());
        Assert.Equal("4 semanas", workbook.Worksheet("Resumen").Cell("B4").GetString());
        Assert.Equal("4 semanas", workbook.Worksheet("Resumen").Cell("B5").GetString());
        Assert.Equal("No", workbook.Worksheet("Resumen").Cell("B6").GetString());
        Assert.Equal("2", workbook.Worksheet("Resumen").Cell("B10").GetString());
        Assert.Equal("A123", workbook.Worksheet("Resultado").Cell("A2").GetString());
        Assert.Equal("Si", workbook.Worksheet("Resultado").Cell("B2").GetString());
        Assert.Equal("D", workbook.Worksheet("Resultado").Cell("C2").GetString());
        Assert.Equal("20", workbook.Worksheet("Resultado").Cell("D2").GetString());
        Assert.Equal("10", workbook.Worksheet("Resultado").Cell("E2").GetString());
        Assert.Equal("30", workbook.Worksheet("Resultado").Cell("F2").GetString());
        Assert.Equal("50", workbook.Worksheet("Resultado").Cell("G2").GetString());
        Assert.Equal("-30", workbook.Worksheet("Resultado").Cell("H2").GetString());
        Assert.Equal("Rojo", workbook.Worksheet("Resultado").Cell("I2").GetString());
        Assert.Equal("-40", workbook.Worksheet("Resultado").Cell("J2").GetString());
        Assert.Equal("Rojo", workbook.Worksheet("Resultado").Cell("K2").GetString());
        Assert.Equal("10", workbook.Worksheet("Resultado").Cell("L2").GetString());
        Assert.Equal("20", workbook.Worksheet("Resultado").Cell("M2").GetString());
        Assert.Equal("Parcial", workbook.Worksheet("Resultado").Cell("N2").GetString());
        Assert.Equal("Zona A", workbook.Worksheet("Detalle por zonas").Cell("B2").GetString());
        Assert.Equal("10", workbook.Worksheet("Detalle por zonas").Cell("C2").GetString());
        Assert.Equal("Satelital", workbook.Worksheet("Detalle por zonas").Cell("D2").GetString());
    }

    [Fact]
    public void ExportTransferRequisition_lists_only_transferable_articles_with_external_availability_and_zones_by_quantity()
    {
        var exporter = new AnalysisWorkbookExporter();
        var result = new WeeklyAnalysisResult(
            new DateOnly(2026, 6, 8),
            new DateOnly(2026, 6, 8),
            new DateOnly(2026, 6, 29),
            requestedWeeks: 4,
            analyzedWeeks: 4,
            isIncompleteHorizon: false,
            new[]
            {
                // Needs and can get external stock -> must appear, zones sorted desc.
                new ArticleAnalysisResult(
                    "5203003",
                    principalInventoryQuantity: 324,
                    totalInventoryQuantity: 889,
                    forecastQuantity: 1313,
                    difference: -989,
                    CoverageStatus.Critical,
                    new[] { new ZoneInventoryDetail("ZA01-PRINCIPAL", 324) },
                    satelliteInventoryQuantity: 565,
                    satelliteDifference: -748,
                    satelliteStatus: CoverageStatus.Critical,
                    transferSuggestionQuantity: 565,
                    remainingShortageAfterTransfer: 424,
                    transferRecommendationState: TransferRecommendationState.Partial,
                    satelliteZones: new[]
                    {
                        new ZoneInventoryDetail("ZA48-ALMACENAJE 2", 8, isSatellite: true),
                        new ZoneInventoryDetail("ZA50-ALMACENAJE 4", 557, isSatellite: true),
                    })
                {
                    PendingTransitQuantity = 120,
                    CalculatedTransferNeedQuantity = 989,
                    SuggestedPallets = new[]
                    {
                        new PalletInventoryDetail("PAL-OLD", "ZA50-ALMACENAJE 4", 500, new DateOnly(2026, 1, 10), "Disponible", "LOC-1"),
                        new PalletInventoryDetail("PAL-NEW", "ZA48-ALMACENAJE 2", 65, new DateOnly(2026, 2, 10), "Disponible", "LOC-2"),
                    },
                },
                // Marked as prepared in satellite -> must move to the audit sheet.
                new ArticleAnalysisResult(
                    "PREP1",
                    principalInventoryQuantity: 0,
                    totalInventoryQuantity: 25,
                    forecastQuantity: 25,
                    difference: -25,
                    CoverageStatus.Critical,
                    Array.Empty<ZoneInventoryDetail>(),
                    satelliteInventoryQuantity: 25,
                    satelliteDifference: 0,
                    satelliteStatus: CoverageStatus.Healthy,
                    transferSuggestionQuantity: 25,
                    remainingShortageAfterTransfer: 0,
                    transferRecommendationState: TransferRecommendationState.Suggested,
                    satelliteZones: new[]
                    {
                        new ZoneInventoryDetail("ZA47-ALMACENAJE 1", 25, isSatellite: true),
                    })
                {
                    SuggestedPallets = new[]
                    {
                        new PalletInventoryDetail("PAL-SAT", "ZA47-ALMACENAJE 1", 25, new DateOnly(2026, 1, 5), "Disponible", "SAT-LOC"),
                    },
                },
                // No transfer needed -> must be excluded from the requisition.
                new ArticleAnalysisResult(
                    "B456",
                    principalInventoryQuantity: 55,
                    totalInventoryQuantity: 55,
                    forecastQuantity: 50,
                    difference: 5,
                    CoverageStatus.Healthy,
                    Array.Empty<ZoneInventoryDetail>(),
                    satelliteInventoryQuantity: 0,
                    satelliteDifference: -50,
                    satelliteStatus: CoverageStatus.Critical,
                    transferSuggestionQuantity: 0,
                    remainingShortageAfterTransfer: 0,
                    transferRecommendationState: TransferRecommendationState.NotRequired,
                    satelliteZones: Array.Empty<ZoneInventoryDetail>()),
            },
            criticalCount: 1,
            warningCount: 0,
            healthyCount: 1,
            satelliteCriticalCount: 1,
            satelliteWarningCount: 0,
            satelliteHealthyCount: 1);

        var ordered = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "5203003" };
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["5203003"] = "Inodoro blanco",
            ["PREP1"] = "Articulo alistado",
        };
        var preparedArticles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PREP1" };
        var tarimaSizes = new Dictionary<string, TarimaSizeEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["5203003"] = new("5203003", "Inodoro blanco", "T"),
            ["PREP1"] = new("PREP1", "Articulo alistado", "S"),
        };
        var pendingPalletKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PalletIdentity.Build("5203003", "PAL-OLD", "ZA50-ALMACENAJE 4", "LOC-1"),
        };

        var bytes = exporter.ExportTransferRequisition("Cofersa", result, ordered, descriptions, preparedArticles, tarimaSizes, pendingPalletKeys: pendingPalletKeys);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Requisicion");

        Assert.Equal("Alistado satelital", sheet.Cell("C4").GetString());
        Assert.Equal("Tamano tarima", sheet.Cell("D4").GetString());
        Assert.Equal("Disponible externas", sheet.Cell("E4").GetString());
        Assert.Equal("Transito pendiente", sheet.Cell("F4").GetString());
        Assert.Equal("Necesidad calculada", sheet.Cell("G4").GetString());
        Assert.Equal("Cantidad a traer", sheet.Cell("H4").GetString());
        Assert.Equal("Sobrante tarima", sheet.Cell("I4").GetString());
        Assert.Equal("Cantidad palets", sheet.Cell("J4").GetString());
        Assert.Equal("Palets FIFO", sheet.Cell("K4").GetString());
        Assert.Equal("Ubicaciones", sheet.Cell("L4").GetString());
        Assert.Equal("Zonas externas", sheet.Cell("M4").GetString());
        Assert.Equal("Pendiente", sheet.Cell("N4").GetString());
        Assert.Equal("Estado transito", sheet.Cell("O4").GetString());
        Assert.Equal("Pedido", sheet.Cell("P4").GetString());

        Assert.Equal("5203003", sheet.Cell("A5").GetString());
        Assert.Equal("Inodoro blanco", sheet.Cell("B5").GetString());
        Assert.Equal("No", sheet.Cell("C5").GetString());
        Assert.Equal("T", sheet.Cell("D5").GetString());
        Assert.Equal("565", sheet.Cell("E5").GetString());
        Assert.Equal("120", sheet.Cell("F5").GetString());
        Assert.Equal("989", sheet.Cell("G5").GetString());
        Assert.Equal("565", sheet.Cell("H5").GetString());
        Assert.Equal("0", sheet.Cell("I5").GetString());
        Assert.Equal("2", sheet.Cell("J5").GetString());
        Assert.Equal("2026-01-10 | PAL-OLD | 500 | LOC-1 | ZA50-ALMACENAJE 4 | COCO; 2026-02-10 | PAL-NEW | 65 | LOC-2 | ZA48-ALMACENAJE 2 | COCO", sheet.Cell("K5").GetString());
        Assert.Equal("LOC-1; LOC-2", sheet.Cell("L5").GetString());
        // Sorted by quantity descending so the most convenient zone leads.
        Assert.Equal("ZA50-ALMACENAJE 4 (557); ZA48-ALMACENAJE 2 (8)", sheet.Cell("M5").GetString());
        Assert.Equal("424", sheet.Cell("N5").GetString());
        Assert.Equal("Parcial (1/2)", sheet.Cell("O5").GetString());
        Assert.Equal("Si", sheet.Cell("P5").GetString());

        // Only the non-prepared transferable article is listed; prepared and no-transfer rows are excluded.
        Assert.True(sheet.Cell("A6").IsEmpty());

        var preparedSheet = workbook.Worksheet("Alistado satelital");
        Assert.Equal("Cantidad calculada", preparedSheet.Cell("G4").GetString());
        Assert.Equal("PREP1", preparedSheet.Cell("A5").GetString());
        Assert.Equal("Articulo alistado", preparedSheet.Cell("B5").GetString());
        Assert.Equal("Si", preparedSheet.Cell("C5").GetString());
        Assert.Equal("S", preparedSheet.Cell("D5").GetString());
        Assert.Equal("25", preparedSheet.Cell("E5").GetString());
        Assert.Equal("0", preparedSheet.Cell("F5").GetString());
        Assert.Equal("25", preparedSheet.Cell("G5").GetString());
        Assert.Equal("1", preparedSheet.Cell("H5").GetString());
        Assert.Equal("2026-01-05 | PAL-SAT | 25 | SAT-LOC | ZA47-ALMACENAJE 1 | COCO", preparedSheet.Cell("I5").GetString());
        Assert.Equal("SAT-LOC", preparedSheet.Cell("J5").GetString());
        Assert.Equal("ZA47-ALMACENAJE 1 (25)", preparedSheet.Cell("K5").GetString());
        Assert.Equal("0", preparedSheet.Cell("L5").GetString());
        Assert.Equal("Nuevo", preparedSheet.Cell("M5").GetString());
        Assert.True(preparedSheet.Cell("A6").IsEmpty());

        Assert.NotNull(workbook.Worksheet("SERVICA"));
        var cocoSheet = workbook.Worksheet("COCO");
        Assert.Equal("5203003", cocoSheet.Cell("A2").GetString());
        Assert.Equal("PAL-OLD", cocoSheet.Cell("C2").GetString());
        Assert.Equal("LOC-1", cocoSheet.Cell("E2").GetString());
        Assert.Equal("Pendiente", cocoSheet.Cell("J2").GetString());
        Assert.Equal("PAL-NEW", cocoSheet.Cell("C3").GetString());
        Assert.Equal("Nuevo", cocoSheet.Cell("J3").GetString());
        Assert.NotNull(workbook.Worksheet("OTRAS"));
    }
}
