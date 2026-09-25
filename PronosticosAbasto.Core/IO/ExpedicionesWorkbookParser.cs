using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace PronosticosAbasto.Core.IO;

/// <summary>
/// Reads the dispatch report's pending detail lines. Full reports are reduced to
/// requested minus prepared quantity; already covered lines and cancelled
/// expeditions are ignored. Simplified files with expedition/article/quantity are
/// still accepted.
/// </summary>
public sealed class ExpedicionesWorkbookParser
{
    private const int MaxHeaderScanRows = 20;

    private static readonly string[] ArticleHeaders = ["Articulo", "Item code", "Codigo", "Prod"];
    private static readonly string[] QuantityHeaders =
    [
        "Cantidad pedida presentacion minima",
        "Cantidad pedida presentación mínima",
        "Cantidad a expedir",
        "Cantidad",
        "Pedidas",
        "Unidades",
        "Expedido",
    ];

    private static readonly string[] DescriptionHeaders = ["Descripcion", "Description"];
    private static readonly string[] PreparedHeaders =
    [
        "Cantidad preparada presentacion minima",
        "Cantidad preparada presentación mínima",
        "Cantidad preparada",
        "Preparadas",
        "Preparada",
        "Preparado",
    ];

    private static readonly string[] ExpeditionHeaders =
    [
        "Expedicion",
        "Expedición",
        "Numero expedicion",
        "Numero de expedicion",
        "No expedicion",
    ];

    private static readonly string[] SituationHeaders =
    [
        "Situacion",
        "Situación",
    ];

    public ExpedicionesWorkbook Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var workbook = new XLWorkbook(stream);
        if (!workbook.Worksheets.Any())
        {
            throw new WorkbookValidationException("El archivo de expediciones no contiene hojas.");
        }

        IXLWorksheet? worksheet = null;

        foreach (var candidate in workbook.Worksheets)
        {
            if (TryFindFirstDetailHeader(candidate, out _))
            {
                worksheet = candidate;
                break;
            }
        }

        if (worksheet is null)
        {
            throw new WorkbookValidationException(
                "No se encontro una hoja de expediciones con las columnas requeridas: Articulo y Cantidad.");
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var lines = new List<ExpeditionLine>();
        DetailColumns? detailColumns = null;
        var currentExpedition = string.Empty;
        var currentSituation = string.Empty;
        var pendingSummaryValues = false;
        var summaryExpeditionColumn = 0;
        var summarySituationColumn = 0;

        for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
        {
            if (pendingSummaryValues)
            {
                currentExpedition = GetCellText(worksheet.Cell(rowNumber, summaryExpeditionColumn));
                currentSituation = GetCellText(worksheet.Cell(rowNumber, summarySituationColumn));
                pendingSummaryValues = false;
                continue;
            }

            if (TryFindDetailHeader(worksheet, rowNumber, out var foundDetailColumns))
            {
                detailColumns = foundDetailColumns;
                continue;
            }

            if (TryFindSummaryHeader(
                worksheet,
                rowNumber,
                out summaryExpeditionColumn,
                out summarySituationColumn))
            {
                pendingSummaryValues = true;
                continue;
            }

            if (detailColumns is not { } columns)
            {
                continue;
            }

            var article = GetCellText(worksheet.Cell(rowNumber, columns.ArticleColumn));
            if (string.IsNullOrWhiteSpace(article))
            {
                continue;
            }

            var quantityCell = worksheet.Cell(rowNumber, columns.QuantityColumn);
            if (!TryGetDecimal(quantityCell, out var requestedQuantity))
            {
                continue;
            }

            var preparedQuantity = 0m;
            if (columns.PreparedColumn > 0)
            {
                TryGetDecimal(worksheet.Cell(rowNumber, columns.PreparedColumn), out preparedQuantity);
            }

            var missingQuantity = columns.PreparedColumn > 0
                ? Math.Max(0m, requestedQuantity - preparedQuantity)
                : requestedQuantity;
            if (missingQuantity <= 0m)
            {
                continue;
            }

            var lineSituation = columns.SituationColumn > 0
                ? GetCellText(worksheet.Cell(rowNumber, columns.SituationColumn))
                : string.Empty;
            if (IsCancelled(currentSituation) || IsCancelled(lineSituation))
            {
                continue;
            }

            var description = columns.DescriptionColumn > 0
                ? GetCellText(worksheet.Cell(rowNumber, columns.DescriptionColumn))
                : string.Empty;
            var expeditionNumber = columns.ExpeditionColumn > 0
                ? GetCellText(worksheet.Cell(rowNumber, columns.ExpeditionColumn))
                : currentExpedition;

            lines.Add(new ExpeditionLine(
                article,
                description,
                missingQuantity,
                expeditionNumber,
                requestedQuantity,
                preparedQuantity,
                string.IsNullOrWhiteSpace(lineSituation) ? currentSituation : lineSituation));
        }

        return new ExpedicionesWorkbook(lines);
    }

    private static bool TryFindFirstDetailHeader(
        IXLWorksheet worksheet,
        out DetailColumns columns)
    {
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        var scanLimit = Math.Min(lastRow, MaxHeaderScanRows);
        for (var rowNumber = 1; rowNumber <= scanLimit; rowNumber++)
        {
            if (TryFindDetailHeader(worksheet, rowNumber, out columns))
            {
                return true;
            }
        }

        columns = default;
        return false;
    }

    private static bool TryFindDetailHeader(IXLWorksheet worksheet, int rowNumber, out DetailColumns columns)
    {
        if (TryFindColumn(worksheet, rowNumber, out var articleColumn, ArticleHeaders) &&
            TryFindColumn(worksheet, rowNumber, out var quantityColumn, QuantityHeaders))
        {
            TryFindColumn(worksheet, rowNumber, out var descriptionColumn, DescriptionHeaders);
            TryFindColumn(worksheet, rowNumber, out var preparedColumn, PreparedHeaders);
            TryFindColumn(worksheet, rowNumber, out var expeditionColumn, ExpeditionHeaders);
            TryFindColumn(worksheet, rowNumber, out var situationColumn, SituationHeaders);

            columns = new DetailColumns(
                articleColumn,
                descriptionColumn,
                quantityColumn,
                preparedColumn,
                expeditionColumn,
                situationColumn);
            return true;
        }

        columns = default;
        return false;
    }

    private static bool TryFindSummaryHeader(
        IXLWorksheet worksheet,
        int rowNumber,
        out int expeditionColumn,
        out int situationColumn)
    {
        var hasExpedition = TryFindColumn(worksheet, rowNumber, out expeditionColumn, ExpeditionHeaders);
        var hasSituation = TryFindColumn(worksheet, rowNumber, out situationColumn, SituationHeaders);
        return hasExpedition && hasSituation;
    }

    private static bool TryFindColumn(IXLWorksheet worksheet, int rowNumber, out int column, params string[] headerCandidates)
    {
        var headerRow = worksheet.Row(rowNumber);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        foreach (var headerCandidate in headerCandidates)
        {
            var normalizedCandidate = NormalizeHeader(headerCandidate);
            for (var candidate = 1; candidate <= lastColumn; candidate++)
            {
                var value = headerRow.Cell(candidate).GetString();
                if (string.Equals(NormalizeHeader(value), normalizedCandidate, StringComparison.OrdinalIgnoreCase))
                {
                    column = candidate;
                    return true;
                }
            }
        }

        column = 0;
        return false;
    }

    private static bool TryGetDecimal(IXLCell cell, out decimal value)
    {
        if (cell.IsEmpty())
        {
            value = 0;
            return false;
        }

        if (cell.TryGetValue<double>(out var numericValue))
        {
            value = Convert.ToDecimal(numericValue, CultureInfo.InvariantCulture);
            return true;
        }

        var text = cell.GetString().Trim();
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
            TryGetLeadingDecimal(text, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static string GetCellText(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.TryGetValue<double>(out var numericValue))
        {
            return numericValue % 1 == 0
                ? numericValue.ToString("0", CultureInfo.InvariantCulture)
                : numericValue.ToString(CultureInfo.InvariantCulture);
        }

        return cell.GetString().Trim();
    }

    private static bool IsCancelled(string value) =>
        string.Equals(NormalizeHeader(value), "anul", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetLeadingDecimal(string text, out decimal value)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            value = 0;
            return false;
        }

        var builder = new StringBuilder();
        foreach (var character in trimmed)
        {
            if (char.IsDigit(character) || character is '-' or '+' or '.' or ',')
            {
                builder.Append(character);
                continue;
            }

            break;
        }

        var candidate = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            value = 0;
            return false;
        }

        return decimal.TryParse(candidate, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ||
            decimal.TryParse(candidate, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private readonly record struct DetailColumns(
        int ArticleColumn,
        int DescriptionColumn,
        int QuantityColumn,
        int PreparedColumn,
        int ExpeditionColumn,
        int SituationColumn);
}
