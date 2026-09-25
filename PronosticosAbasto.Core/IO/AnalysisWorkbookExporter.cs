using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PronosticosAbasto.Core.Analysis;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PronosticosAbasto.Core.IO;

public sealed class AnalysisWorkbookExporter
{
    public byte[] Export(
        string companyName,
        WeeklyAnalysisResult analysisResult,
        IReadOnlySet<string>? preparedArticles = null,
        IReadOnlyDictionary<string, TarimaSizeEntry>? tarimaSizes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentNullException.ThrowIfNull(analysisResult);

        using var workbook = new XLWorkbook();
        CreateSummarySheet(workbook, companyName, analysisResult);
        CreateResultsSheet(workbook, analysisResult, preparedArticles, tarimaSizes);
        CreateZonesSheet(workbook, analysisResult);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportWarehouseComparison(
        string companyName,
        WarehouseComparisonResult comparison,
        IEnumerable<WarehouseComparisonRow> visibleRows,
        IReadOnlyDictionary<string, string>? comments = null,
        IEnumerable<WarehouseComparisonHistogramBucket>? histogramBuckets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(visibleRows);

        var histogramBucketArray = histogramBuckets?.ToArray();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Comparativa");
        sheet.Cell("A1").Value = "Articulo";
        sheet.Cell("B1").Value = "Descripcion";
        sheet.Cell("C1").Value = "Inv OLO";
        sheet.Cell("D1").Value = "Inv Servica";
        sheet.Cell("E1").Value = "Semanas OLO";
        sheet.Cell("F1").Value = "Comentarios";
        StyleHeaderRange(sheet.Range("A1:F1"));

        var row = 2;
        foreach (var item in visibleRows)
        {
            sheet.Cell(row, 1).Value = item.Article;
            sheet.Cell(row, 2).Value = item.Description;
            sheet.Cell(row, 3).Value = item.OloQuantity;
            sheet.Cell(row, 4).Value = item.ServicaQuantity;
            if (item.CoveragePeriods is decimal coverage)
            {
                sheet.Cell(row, 5).Value = coverage;
            }
            else
            {
                sheet.Cell(row, 5).Value = "—";
            }

            sheet.Cell(row, 6).Value = ResolveComparisonComment(item, comments);
            row++;
        }

        if (row > 2)
        {
            sheet.Range(1, 1, row - 1, 6).SetAutoFilter();
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        CreateWarehouseComparisonHistogramSheet(workbook, histogramBucketArray);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        if (histogramBucketArray is { Length: > 0 })
        {
            AddWarehouseComparisonHistogramChart(stream, histogramBucketArray.Length);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Exports the transfer requisition: only the articles that need (and can
    /// get) stock from the external/satellite warehouse, with how much to bring,
    /// from which zones, what stays pending, and whether it was marked as ordered.
    /// </summary>
    public byte[] ExportTransferRequisition(
        string companyName,
        WeeklyAnalysisResult analysisResult,
        IReadOnlySet<string>? orderedArticles = null,
        IReadOnlyDictionary<string, string>? descriptions = null,
        IReadOnlySet<string>? preparedArticles = null,
        IReadOnlyDictionary<string, TarimaSizeEntry>? tarimaSizes = null,
        IReadOnlyDictionary<string, IReadOnlyList<PalletInventoryDetail>>? selectedPalletsByArticle = null,
        IReadOnlySet<string>? pendingPalletKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentNullException.ThrowIfNull(analysisResult);

        var ordered = orderedArticles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Requisicion");
        sheet.Cell("A1").Value = "Empresa";
        sheet.Cell("B1").Value = companyName;
        sheet.Cell("A2").Value = "Periodo";
        sheet.Cell("B2").Value = $"{analysisResult.HorizonStartWeek:dd.MM.yy} a {analysisResult.HorizonEndWeek:dd.MM.yy} ({analysisResult.HorizonDescription})";
        StyleHeaderRange(sheet.Range("A1:A2"));

        sheet.Cell("A4").Value = "Articulo";
        sheet.Cell("B4").Value = "Descripcion";
        sheet.Cell("C4").Value = "Alistado satelital";
        sheet.Cell("D4").Value = "Tamano tarima";
        sheet.Cell("E4").Value = "Disponible externas";
        sheet.Cell("F4").Value = "Transito pendiente";
        sheet.Cell("G4").Value = "Necesidad calculada";
        sheet.Cell("H4").Value = "Cantidad a traer";
        sheet.Cell("I4").Value = "Sobrante tarima";
        sheet.Cell("J4").Value = "Cantidad palets";
        sheet.Cell("K4").Value = "Palets FIFO";
        sheet.Cell("L4").Value = "Ubicaciones";
        sheet.Cell("M4").Value = "Zonas externas";
        sheet.Cell("N4").Value = "Pendiente";
        sheet.Cell("O4").Value = "Estado transito";
        sheet.Cell("P4").Value = "Pedido";
        StyleHeaderRange(sheet.Range("A4:P4"));

        var row = 5;
        foreach (var article in analysisResult.Articles
            .Where(article =>
                article.TransferSuggestionQuantity > 0 &&
                !IsPreparedArticle(article.Article, preparedArticles) &&
                ResolvePallets(article, selectedPalletsByArticle).Count > 0)
            .OrderByDescending(article => article.TransferSuggestionQuantity)
            .ThenBy(article => article.Article, StringComparer.OrdinalIgnoreCase))
        {
            var pallets = ResolvePallets(article, selectedPalletsByArticle);
            var transferQuantity = pallets.Sum(pallet => pallet.Quantity);
            sheet.Cell(row, 1).Value = article.Article;
            sheet.Cell(row, 2).Value = descriptions is not null && descriptions.TryGetValue(article.Article, out var description) ? description : string.Empty;
            sheet.Cell(row, 3).Value = ToPreparedText(article.Article, preparedArticles);
            sheet.Cell(row, 4).Value = ToTarimaSizeText(article.Article, tarimaSizes);
            sheet.Cell(row, 5).Value = article.SatelliteInventoryQuantity;
            sheet.Cell(row, 6).Value = article.PendingTransitQuantity;
            sheet.Cell(row, 7).Value = article.CalculatedTransferNeedQuantity;
            sheet.Cell(row, 8).Value = transferQuantity;
            sheet.Cell(row, 9).Value = Math.Max(0m, transferQuantity - article.CalculatedTransferNeedQuantity);
            sheet.Cell(row, 10).Value = pallets.Count;
            sheet.Cell(row, 11).Value = DescribeSuggestedPallets(pallets);
            sheet.Cell(row, 12).Value = DescribeLocations(pallets);
            sheet.Cell(row, 13).Value = DescribeSatelliteZones(article);
            sheet.Cell(row, 14).Value = Math.Max(0m, article.CalculatedTransferNeedQuantity - transferQuantity);
            sheet.Cell(row, 15).Value = ToTransitState(article.Article, pallets, pendingPalletKeys);
            sheet.Cell(row, 16).Value = ordered.Contains(article.Article) ? "Si" : "No";
            row++;
        }

        sheet.Columns().AdjustToContents();
        CreatePreparedSatelliteRequisitionSheet(workbook, companyName, analysisResult, descriptions, preparedArticles, tarimaSizes, pendingPalletKeys);
        CreateOriginSheets(workbook, analysisResult, descriptions, preparedArticles, tarimaSizes, selectedPalletsByArticle, pendingPalletKeys);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void CreatePreparedSatelliteRequisitionSheet(
        XLWorkbook workbook,
        string companyName,
        WeeklyAnalysisResult analysisResult,
        IReadOnlyDictionary<string, string>? descriptions,
        IReadOnlySet<string>? preparedArticles,
        IReadOnlyDictionary<string, TarimaSizeEntry>? tarimaSizes,
        IReadOnlySet<string>? pendingPalletKeys)
    {
        var sheet = workbook.AddWorksheet("Alistado satelital");
        sheet.Cell("A1").Value = "Empresa";
        sheet.Cell("B1").Value = companyName;
        sheet.Cell("A2").Value = "Periodo";
        sheet.Cell("B2").Value = $"{analysisResult.HorizonStartWeek:dd.MM.yy} a {analysisResult.HorizonEndWeek:dd.MM.yy} ({analysisResult.HorizonDescription})";
        StyleHeaderRange(sheet.Range("A1:A2"));

        sheet.Cell("A4").Value = "Articulo";
        sheet.Cell("B4").Value = "Descripcion";
        sheet.Cell("C4").Value = "Alistado satelital";
        sheet.Cell("D4").Value = "Tamano tarima";
        sheet.Cell("E4").Value = "Disponible externas";
        sheet.Cell("F4").Value = "Transito pendiente";
        sheet.Cell("G4").Value = "Cantidad calculada";
        sheet.Cell("H4").Value = "Cantidad palets";
        sheet.Cell("I4").Value = "Palets FIFO";
        sheet.Cell("J4").Value = "Ubicaciones";
        sheet.Cell("K4").Value = "Zonas externas";
        sheet.Cell("L4").Value = "Pendiente";
        sheet.Cell("M4").Value = "Estado transito";
        StyleHeaderRange(sheet.Range("A4:M4"));

        var row = 5;
        foreach (var article in analysisResult.Articles
            .Where(article => article.TransferSuggestionQuantity > 0 && IsPreparedArticle(article.Article, preparedArticles))
            .OrderByDescending(article => article.TransferSuggestionQuantity)
            .ThenBy(article => article.Article, StringComparer.OrdinalIgnoreCase))
        {
            sheet.Cell(row, 1).Value = article.Article;
            sheet.Cell(row, 2).Value = descriptions is not null && descriptions.TryGetValue(article.Article, out var description) ? description : string.Empty;
            sheet.Cell(row, 3).Value = "Si";
            sheet.Cell(row, 4).Value = ToTarimaSizeText(article.Article, tarimaSizes);
            sheet.Cell(row, 5).Value = article.SatelliteInventoryQuantity;
            sheet.Cell(row, 6).Value = article.PendingTransitQuantity;
            sheet.Cell(row, 7).Value = article.TransferSuggestionQuantity;
            sheet.Cell(row, 8).Value = article.SuggestedPalletCount;
            sheet.Cell(row, 9).Value = DescribeSuggestedPallets(article.SuggestedPallets);
            sheet.Cell(row, 10).Value = DescribeLocations(article.SuggestedPallets);
            sheet.Cell(row, 11).Value = DescribeSatelliteZones(article);
            sheet.Cell(row, 12).Value = article.RemainingShortageAfterTransfer;
            sheet.Cell(row, 13).Value = ToTransitState(article.Article, article.SuggestedPallets, pendingPalletKeys);
            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void CreateOriginSheets(
        XLWorkbook workbook,
        WeeklyAnalysisResult analysisResult,
        IReadOnlyDictionary<string, string>? descriptions,
        IReadOnlySet<string>? preparedArticles,
        IReadOnlyDictionary<string, TarimaSizeEntry>? tarimaSizes,
        IReadOnlyDictionary<string, IReadOnlyList<PalletInventoryDetail>>? selectedPalletsByArticle,
        IReadOnlySet<string>? pendingPalletKeys)
    {
        var rows = analysisResult.Articles
            .Where(article =>
                article.TransferSuggestionQuantity > 0 &&
                !IsPreparedArticle(article.Article, preparedArticles))
            .SelectMany(article =>
                ResolvePallets(article, selectedPalletsByArticle).Select(pallet => new OriginExportRow(
                    article.Article,
                    descriptions is not null && descriptions.TryGetValue(article.Article, out var description) ? description : string.Empty,
                    pallet,
                    ToTarimaSizeText(article.Article, tarimaSizes),
                    ToTransitState(article.Article, new[] { pallet }, pendingPalletKeys))))
            .GroupBy(row => StorageOriginClassifier.Resolve(row.Pallet.StorageZone), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var origin in new[] { "SERVICA", "COCO", "OTRAS" })
        {
            var sheet = workbook.AddWorksheet(origin);
            sheet.Cell("A1").Value = "Articulo";
            sheet.Cell("B1").Value = "Descripcion";
            sheet.Cell("C1").Value = "Pallet";
            sheet.Cell("D1").Value = "Cantidad";
            sheet.Cell("E1").Value = "Ubicacion";
            sheet.Cell("F1").Value = "Zona";
            sheet.Cell("G1").Value = "Fecha validacion";
            sheet.Cell("H1").Value = "Tamano tarima";
            sheet.Cell("I1").Value = "Cantidad palets";
            sheet.Cell("J1").Value = "Estado transito";
            StyleHeaderRange(sheet.Range("A1:J1"));

            if (rows.TryGetValue(origin, out var originRows))
            {
                var rowNumber = 2;
                foreach (var row in originRows
                    .OrderBy(row => row.Pallet.Location, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.Pallet.StorageZone, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.Pallet.ValidationDate)
                    .ThenBy(row => row.Pallet.Pallet, StringComparer.OrdinalIgnoreCase))
                {
                    sheet.Cell(rowNumber, 1).Value = row.Article;
                    sheet.Cell(rowNumber, 2).Value = row.Description;
                    sheet.Cell(rowNumber, 3).Value = row.Pallet.Pallet;
                    sheet.Cell(rowNumber, 4).Value = row.Pallet.Quantity;
                    sheet.Cell(rowNumber, 5).Value = FormatLocation(row.Pallet.Location);
                    sheet.Cell(rowNumber, 6).Value = row.Pallet.StorageZone;
                    sheet.Cell(rowNumber, 7).Value = FormatDate(row.Pallet.ValidationDate);
                    sheet.Cell(rowNumber, 8).Value = row.TarimaSize;
                    sheet.Cell(rowNumber, 9).Value = 1;
                    sheet.Cell(rowNumber, 10).Value = row.TransitState;
                    rowNumber++;
                }
            }

            sheet.Columns().AdjustToContents();
        }
    }

    private static string DescribeSatelliteZones(ArticleAnalysisResult article) =>
        string.Join(
            "; ",
            article.SatelliteZones
                .Where(zone => zone.Quantity > 0)
                .OrderByDescending(zone => zone.Quantity)
                .Select(zone => $"{zone.StorageZone} ({zone.Quantity:N0})"));

    private static string DescribeSuggestedPallets(IReadOnlyList<PalletInventoryDetail> pallets) =>
        string.Join(
            "; ",
            pallets.Select(pallet =>
                $"{FormatDate(pallet.ValidationDate)} | {pallet.Pallet} | {pallet.Quantity:N0} | {FormatLocation(pallet.Location)} | {pallet.StorageZone} | {StorageOriginClassifier.Resolve(pallet.StorageZone)}"));

    private static string DescribeLocations(IReadOnlyList<PalletInventoryDetail> pallets) =>
        string.Join(
            "; ",
            pallets
                .Select(pallet => FormatLocation(pallet.Location))
                .Where(location => !string.IsNullOrWhiteSpace(location))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(location => location, StringComparer.OrdinalIgnoreCase));

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd") ?? "Sin fecha";

    private static string FormatLocation(string location) =>
        string.IsNullOrWhiteSpace(location) ? "Sin ubicacion" : location.Trim();

    private static IReadOnlyList<PalletInventoryDetail> ResolvePallets(
        ArticleAnalysisResult article,
        IReadOnlyDictionary<string, IReadOnlyList<PalletInventoryDetail>>? selectedPalletsByArticle)
    {
        if (selectedPalletsByArticle is not null &&
            (selectedPalletsByArticle.TryGetValue(article.Article, out var selected) ||
                selectedPalletsByArticle.TryGetValue(InventoryZoneClassifier.NormalizeArticleKey(article.Article), out selected)))
        {
            return selected;
        }

        return article.SuggestedPallets;
    }

    private static string ToTransitState(
        string article,
        IReadOnlyList<PalletInventoryDetail> pallets,
        IReadOnlySet<string>? pendingPalletKeys)
    {
        if (pallets.Count == 0)
        {
            return "Sin palets";
        }

        if (pendingPalletKeys is null || pendingPalletKeys.Count == 0)
        {
            return "Nuevo";
        }

        var pending = pallets.Count(pallet => pendingPalletKeys.Contains(PalletIdentity.Build(article, pallet)));
        return pending == pallets.Count
            ? "Pendiente"
            : pending == 0
                ? "Nuevo"
                : $"Parcial ({pending:N0}/{pallets.Count:N0})";
    }

    private static void CreateSummarySheet(XLWorkbook workbook, string companyName, WeeklyAnalysisResult analysisResult)
    {
        var sheet = workbook.AddWorksheet("Resumen");
        sheet.Cell("A1").Value = "Empresa";
        sheet.Cell("B1").Value = companyName;
        sheet.Cell("A2").Value = "Semana inicio";
        sheet.Cell("B2").Value = analysisResult.HorizonStartWeek.ToString("yyyy-MM-dd");
        sheet.Cell("A3").Value = "Semana fin";
        sheet.Cell("B3").Value = analysisResult.HorizonEndWeek.ToString("yyyy-MM-dd");
        sheet.Cell("A4").Value = "Horizonte solicitado";
        sheet.Cell("B4").Value = analysisResult.HorizonDescription;
        sheet.Cell("A5").Value = "Horizonte analizado";
        sheet.Cell("B5").Value = $"{analysisResult.AnalyzedWeeks} semanas";
        sheet.Cell("A6").Value = "Horizonte incompleto";
        sheet.Cell("B6").Value = analysisResult.IsIncompleteHorizon ? "Si" : "No";
        sheet.Cell("A7").Value = "Rojos principales";
        sheet.Cell("B7").Value = analysisResult.CriticalCount;
        sheet.Cell("A8").Value = "Amarillos principales";
        sheet.Cell("B8").Value = analysisResult.WarningCount;
        sheet.Cell("A9").Value = "Verdes principales";
        sheet.Cell("B9").Value = analysisResult.HealthyCount;
        sheet.Cell("A10").Value = "Rojos satelitales";
        sheet.Cell("B10").Value = analysisResult.SatelliteCriticalCount;
        sheet.Cell("A11").Value = "Amarillos satelitales";
        sheet.Cell("B11").Value = analysisResult.SatelliteWarningCount;
        sheet.Cell("A12").Value = "Verdes satelitales";
        sheet.Cell("B12").Value = analysisResult.SatelliteHealthyCount;
        StyleHeaderRange(sheet.Range("A1:A12"));
        sheet.Columns().AdjustToContents();
    }

    private static void CreateResultsSheet(
        XLWorkbook workbook,
        WeeklyAnalysisResult analysisResult,
        IReadOnlySet<string>? preparedArticles,
        IReadOnlyDictionary<string, TarimaSizeEntry>? tarimaSizes)
    {
        var sheet = workbook.AddWorksheet("Resultado");
        sheet.Cell("A1").Value = "Articulo";
        sheet.Cell("B1").Value = "Alistado satelital";
        sheet.Cell("C1").Value = "Tamano tarima";
        sheet.Cell("D1").Value = "Inventario principal";
        sheet.Cell("E1").Value = "Inventario satelital";
        sheet.Cell("F1").Value = "Inventario total";
        sheet.Cell("G1").Value = $"Forecast {analysisResult.HorizonDescription}";
        sheet.Cell("H1").Value = "Diferencia principal";
        sheet.Cell("I1").Value = "Estado principal";
        sheet.Cell("J1").Value = "Diferencia satelital";
        sheet.Cell("K1").Value = "Estado satelital";
        sheet.Cell("L1").Value = "Traslado sugerido";
        sheet.Cell("M1").Value = "Faltante restante";
        sheet.Cell("N1").Value = "Estado de traslado";
        StyleHeaderRange(sheet.Range("A1:N1"));

        for (var index = 0; index < analysisResult.Articles.Count; index++)
        {
            var article = analysisResult.Articles[index];
            var row = index + 2;
            sheet.Cell(row, 1).Value = article.Article;
            sheet.Cell(row, 2).Value = ToPreparedText(article.Article, preparedArticles);
            sheet.Cell(row, 3).Value = ToTarimaSizeText(article.Article, tarimaSizes);
            sheet.Cell(row, 4).Value = article.PrincipalInventoryQuantity;
            sheet.Cell(row, 5).Value = article.SatelliteInventoryQuantity;
            sheet.Cell(row, 6).Value = article.TotalInventoryQuantity;
            sheet.Cell(row, 7).Value = article.ForecastQuantity;
            sheet.Cell(row, 8).Value = article.Difference;
            sheet.Cell(row, 9).Value = ToSpanishStatus(article.Status);
            sheet.Cell(row, 10).Value = article.SatelliteDifference;
            sheet.Cell(row, 11).Value = ToSpanishStatus(article.SatelliteStatus);
            sheet.Cell(row, 12).Value = article.TransferSuggestionQuantity;
            sheet.Cell(row, 13).Value = article.RemainingShortageAfterTransfer;
            sheet.Cell(row, 14).Value = ToSpanishTransferState(article.TransferRecommendationState);
            ApplyStatusColor(sheet.Range(row, 1, row, 14), MostSevereStatus(article.Status, article.SatelliteStatus));
        }

        sheet.Columns().AdjustToContents();
    }

    private static void CreateZonesSheet(XLWorkbook workbook, WeeklyAnalysisResult analysisResult)
    {
        var sheet = workbook.AddWorksheet("Detalle por zonas");
        sheet.Cell("A1").Value = "Articulo";
        sheet.Cell("B1").Value = "Zona de almacenaje";
        sheet.Cell("C1").Value = "Cantidad";
        sheet.Cell("D1").Value = "Tipo de zona";
        StyleHeaderRange(sheet.Range("A1:D1"));

        var row = 2;
        foreach (var article in analysisResult.Articles)
        {
            foreach (var zone in article.Zones)
            {
                sheet.Cell(row, 1).Value = article.Article;
                sheet.Cell(row, 2).Value = zone.StorageZone;
                sheet.Cell(row, 3).Value = zone.Quantity;
                sheet.Cell(row, 4).Value = zone.IsSatellite ? "Satelital" : "Principal";
                row++;
            }
        }

        sheet.Columns().AdjustToContents();
    }

    private static void StyleHeaderRange(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F2");
    }

    private static void ApplyStatusColor(IXLRange range, CoverageStatus status)
    {
        range.Style.Fill.BackgroundColor = status switch
        {
            CoverageStatus.Critical => XLColor.FromHtml("#FDE7E9"),
            CoverageStatus.Warning => XLColor.FromHtml("#FFF4D6"),
            _ => XLColor.FromHtml("#E5F4EA"),
        };
    }

    private static string ToSpanishStatus(CoverageStatus status) =>
        status switch
        {
            CoverageStatus.Critical => "Rojo",
            CoverageStatus.Warning => "Amarillo",
            _ => "Verde",
        };

    private static string ToPreparedText(string article, IReadOnlySet<string>? preparedArticles) =>
        IsPreparedArticle(article, preparedArticles) ? "Si" : "No";

    private static bool IsPreparedArticle(string article, IReadOnlySet<string>? preparedArticles) =>
        preparedArticles is not null &&
        (preparedArticles.Contains(article) ||
            preparedArticles.Contains(InventoryZoneClassifier.NormalizeArticleKey(article)));

    private static string ResolveComparisonComment(
        WarehouseComparisonRow row,
        IReadOnlyDictionary<string, string>? comments)
    {
        if (comments is not null &&
            (comments.TryGetValue(row.Article, out var comment) ||
                comments.TryGetValue(InventoryZoneClassifier.NormalizeArticleKey(row.Article), out comment)))
        {
            return comment?.Trim() ?? string.Empty;
        }

        return row.AutomaticComment;
    }

    private static void CreateWarehouseComparisonHistogramSheet(
        XLWorkbook workbook,
        IEnumerable<WarehouseComparisonHistogramBucket>? histogramBuckets)
    {
        if (histogramBuckets is null)
        {
            return;
        }

        var buckets = histogramBuckets.ToArray();
        var sheet = workbook.AddWorksheet("Histograma");
        var titleRange = sheet.Range("A1:C1");
        titleRange.Merge();
        titleRange.Value = "Reporte de Distribucion de Articulos por Periodo";
        titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#00A88F");
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(1).Height = 28;

        sheet.Cell("A3").Value = "Periodo";
        sheet.Cell("B3").Value = "Cantidad de articulos";
        sheet.Cell("C3").Value = "% del total";
        StyleHeaderRange(sheet.Range("A3:C3"));
        sheet.Range("A3:C3").Style.Fill.BackgroundColor = XLColor.FromHtml("#DDF3F0");

        for (var index = 0; index < buckets.Length; index++)
        {
            var bucket = buckets[index];
            var row = index + 4;
            sheet.Cell(row, 1).Value = bucket.Label;
            sheet.Cell(row, 2).Value = bucket.ArticleCount;
            sheet.Cell(row, 3).Value = bucket.Percentage / 100m;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
            sheet.Range(row, 1, row, 3).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            sheet.Range(row, 1, row, 3).Style.Border.BottomBorderColor = XLColor.FromHtml("#D9D9D9");
            sheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        var totalRow = buckets.Length + 4;
        sheet.Cell(totalRow, 1).Value = "Total";
        sheet.Cell(totalRow, 2).Value = buckets.Sum(bucket => bucket.ArticleCount);
        sheet.Cell(totalRow, 3).Value = buckets.Sum(bucket => bucket.Percentage) / 100m;
        sheet.Cell(totalRow, 3).Style.NumberFormat.Format = "0.0%";
        sheet.Range(totalRow, 1, totalRow, 3).Style.Font.Bold = true;
        sheet.Range(totalRow, 1, totalRow, 3).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        sheet.Range(totalRow, 1, totalRow, 3).Style.Border.BottomBorder = XLBorderStyleValues.Double;

        if (buckets.Length > 0)
        {
            sheet.Range(3, 1, totalRow - 1, 3).SetAutoFilter();
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    private static void AddWarehouseComparisonHistogramChart(MemoryStream stream, int bucketCount)
    {
        if (bucketCount <= 0)
        {
            return;
        }

        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, true);
        var workbookPart = document.WorkbookPart;
        var sheet = workbookPart?.Workbook.Sheets?.Elements<S.Sheet>()
            .FirstOrDefault(item => string.Equals(item.Name?.Value, "Histograma", StringComparison.OrdinalIgnoreCase));
        if (workbookPart is null || sheet?.Id?.Value is not string sheetRelationshipId)
        {
            return;
        }

        if (workbookPart.GetPartById(sheetRelationshipId) is not WorksheetPart worksheetPart)
        {
            return;
        }

        var drawingsPart = worksheetPart.DrawingsPart ?? worksheetPart.AddNewPart<DrawingsPart>();
        drawingsPart.WorksheetDrawing ??= new Xdr.WorksheetDrawing();
        EnsureWorksheetHasDrawing(worksheetPart, drawingsPart);

        var chartPart = drawingsPart.AddNewPart<ChartPart>();
        chartPart.ChartSpace = CreateWarehouseComparisonChartSpace(bucketCount);
        chartPart.ChartSpace.Save();

        var chartRelationshipId = drawingsPart.GetIdOfPart(chartPart);
        var drawingId = drawingsPart.WorksheetDrawing
            .Descendants<Xdr.NonVisualDrawingProperties>()
            .Select(item => item.Id?.Value ?? 0U)
            .DefaultIfEmpty(0U)
            .Max() + 1U;
        drawingsPart.WorksheetDrawing.Append(CreateWarehouseComparisonChartAnchor(chartRelationshipId, drawingId));
        drawingsPart.WorksheetDrawing.Save();
        worksheetPart.Worksheet.Save();
        stream.Position = 0;
    }

    private static void EnsureWorksheetHasDrawing(WorksheetPart worksheetPart, DrawingsPart drawingsPart)
    {
        var relationshipId = worksheetPart.GetIdOfPart(drawingsPart);
        var worksheet = worksheetPart.Worksheet;
        if (worksheet.Elements<S.Drawing>().FirstOrDefault() is { } existingDrawing)
        {
            existingDrawing.Id = relationshipId;
            return;
        }

        var drawing = new S.Drawing { Id = relationshipId };
        if (worksheet.Elements<S.TableParts>().FirstOrDefault() is { } tableParts)
        {
            worksheet.InsertBefore(drawing, tableParts);
            return;
        }

        if (worksheet.Elements<S.ExtensionList>().FirstOrDefault() is { } extensionList)
        {
            worksheet.InsertBefore(drawing, extensionList);
            return;
        }

        worksheet.Append(drawing);
    }

    private static Xdr.TwoCellAnchor CreateWarehouseComparisonChartAnchor(string chartRelationshipId, uint drawingId) =>
        new(
            new Xdr.FromMarker(
                new Xdr.ColumnId("4"),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId("2"),
                new Xdr.RowOffset("0")),
            new Xdr.ToMarker(
                new Xdr.ColumnId("14"),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId("20"),
                new Xdr.RowOffset("0")),
            new Xdr.GraphicFrame(
                new Xdr.NonVisualGraphicFrameProperties(
                    new Xdr.NonVisualDrawingProperties { Id = drawingId, Name = "Cantidad de articulos por Periodo" },
                    new Xdr.NonVisualGraphicFrameDrawingProperties()),
                new Xdr.Transform(
                    new A.Offset { X = 0L, Y = 0L },
                    new A.Extents { Cx = 0L, Cy = 0L }),
                new A.Graphic(
                    new A.GraphicData(
                        new C.ChartReference { Id = chartRelationshipId })
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart",
                    })),
            new Xdr.ClientData());

    private static C.ChartSpace CreateWarehouseComparisonChartSpace(int bucketCount)
    {
        var lastDataRow = bucketCount + 3;
        var categoryFormula = $"'Histograma'!$A$4:$A${lastDataRow}";
        var valuesFormula = $"'Histograma'!$B$4:$B${lastDataRow}";
        const uint categoryAxisId = 48650112U;
        const uint valueAxisId = 48672768U;

        return new C.ChartSpace(
            new C.EditingLanguage { Val = "es-CR" },
            new C.RoundedCorners { Val = false },
            new C.Chart(
                CreateChartTitle("Cantidad de articulos por Periodo"),
                new C.PlotArea(
                    new C.Layout(),
                    new C.BarChart(
                        new C.BarDirection { Val = C.BarDirectionValues.Column },
                        new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
                        CreateWarehouseComparisonBarSeries(categoryFormula, valuesFormula),
                        new C.DataLabels(
                            new C.ShowLegendKey { Val = false },
                            new C.ShowValue { Val = true },
                            new C.ShowCategoryName { Val = false },
                            new C.ShowSeriesName { Val = false },
                            new C.ShowPercent { Val = false },
                            new C.ShowBubbleSize { Val = false }),
                        new C.GapWidth { Val = (UInt16Value)(ushort)80 },
                        new C.AxisId { Val = categoryAxisId },
                        new C.AxisId { Val = valueAxisId }),
                    CreateCategoryAxis(categoryAxisId, valueAxisId),
                    CreateValueAxis(valueAxisId, categoryAxisId)),
                new C.PlotVisibleOnly { Val = true },
                new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Gap }),
            new C.PrintSettings(
                new C.HeaderFooter(),
                new C.PageMargins
                {
                    Left = 0.7d,
                    Right = 0.7d,
                    Top = 0.75d,
                    Bottom = 0.75d,
                    Header = 0.3d,
                    Footer = 0.3d,
                },
                new C.PageSetup()));
    }

    private static C.BarChartSeries CreateWarehouseComparisonBarSeries(string categoryFormula, string valuesFormula) =>
        new(
            new C.Index { Val = 0U },
            new C.Order { Val = 0U },
            new C.SeriesText(
                new C.StringReference(
                    new C.Formula("'Histograma'!$B$3"))),
            new C.ChartShapeProperties(
                new A.SolidFill(new A.RgbColorModelHex { Val = "00A88F" }),
                new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "008C78" }))),
            new C.CategoryAxisData(
                new C.StringReference(
                    new C.Formula(categoryFormula))),
            new C.Values(
                new C.NumberReference(
                    new C.Formula(valuesFormula))));

    private static C.CategoryAxis CreateCategoryAxis(uint categoryAxisId, uint valueAxisId) =>
        new(
            new C.AxisId { Val = categoryAxisId },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Bottom },
            new C.NumberingFormat { FormatCode = "General", SourceLinked = true },
            new C.MajorTickMark { Val = C.TickMarkValues.None },
            new C.MinorTickMark { Val = C.TickMarkValues.None },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = valueAxisId },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.AutoLabeled { Val = true },
            new C.LabelAlignment { Val = C.LabelAlignmentValues.Center },
            new C.LabelOffset { Val = (UInt16Value)(ushort)100 });

    private static C.ValueAxis CreateValueAxis(uint valueAxisId, uint categoryAxisId) =>
        new(
            new C.AxisId { Val = valueAxisId },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Left },
            new C.MajorGridlines(),
            new C.NumberingFormat { FormatCode = "General", SourceLinked = true },
            new C.MajorTickMark { Val = C.TickMarkValues.Outside },
            new C.MinorTickMark { Val = C.TickMarkValues.None },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = categoryAxisId },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.CrossBetween { Val = C.CrossBetweenValues.Between });

    private static C.Title CreateChartTitle(string text) =>
        new(
            new C.ChartText(
                new C.RichText(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.Run(
                            new A.RunProperties { Language = "es-CR", FontSize = 1400 },
                            new A.Text(text))))),
            new C.Layout(),
            new C.Overlay { Val = false });

    private static string ToTarimaSizeText(string article, IReadOnlyDictionary<string, TarimaSizeEntry>? tarimaSizes) =>
        tarimaSizes is not null &&
        (tarimaSizes.TryGetValue(article, out var size) ||
            tarimaSizes.TryGetValue(InventoryZoneClassifier.NormalizeArticleKey(article), out size)) &&
        !string.IsNullOrWhiteSpace(size.SizeCode)
            ? size.SizeCode
            : string.Empty;

    private static string ToSpanishTransferState(TransferRecommendationState state) =>
        state switch
        {
            TransferRecommendationState.Suggested => "Sugerido",
            TransferRecommendationState.Partial => "Parcial",
            TransferRecommendationState.Unavailable => "Sin satelital",
            _ => "No requerido",
        };

    private static CoverageStatus MostSevereStatus(CoverageStatus first, CoverageStatus second) =>
        (CoverageStatus)Math.Min((int)first, (int)second);

    private sealed record OriginExportRow(
        string Article,
        string Description,
        PalletInventoryDetail Pallet,
        string TarimaSize,
        string TransitState);
}
