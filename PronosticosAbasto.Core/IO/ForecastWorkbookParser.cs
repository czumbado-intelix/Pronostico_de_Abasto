using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.IO;

public sealed class ForecastWorkbookParser
{
    private const int MaxHeaderScanRows = 12;

    private static readonly string[] ArticleHeaderCandidates =
    {
        "Articulo", "Articulos", "Prod", "Producto", "Item code", "Item", "Codigo", "Cod articulo",
    };

    private static readonly string[] DescriptionHeaderCandidates =
    {
        "Descripcion", "Description", "Descripción",
    };

    private static readonly CultureInfo[] MonthCultures =
    {
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("es-ES"),
        CultureInfo.GetCultureInfo("es-CR"),
    };

    public ForecastWorkbook Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new WorkbookValidationException("El archivo de forecast no contiene hojas.");

        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        var (headerRow, articleColumn) = FindHeader(worksheet, lastColumn, lastRow);
        var descriptionColumn = FindOptionalColumn(worksheet, headerRow, lastColumn, DescriptionHeaderCandidates);

        var periodColumns = new List<PeriodColumn>();
        for (var column = 1; column <= lastColumn; column++)
        {
            if (column == articleColumn)
            {
                continue;
            }

            if (TryParsePeriodHeader(worksheet.Cell(headerRow, column), out var period, out var isMonthly))
            {
                periodColumns.Add(new PeriodColumn(column, period, isMonthly));
            }
        }

        if (periodColumns.Count == 0)
        {
            throw new WorkbookValidationException(
                "No se encontraron columnas de semana o mes validas en el forecast.");
        }

        var entries = new List<ForecastEntry>();
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var article = worksheet.Cell(rowNumber, articleColumn).GetString().Trim();
            if (string.IsNullOrWhiteSpace(article))
            {
                continue;
            }

            CaptureDescription(worksheet, rowNumber, descriptionColumn, article, descriptions);

            foreach (var periodColumn in periodColumns)
            {
                var cell = worksheet.Cell(rowNumber, periodColumn.Column);
                if (cell.IsEmpty())
                {
                    continue;
                }

                if (!TryGetDecimal(cell, out var quantity))
                {
                    throw new WorkbookValidationException(
                        $"La cantidad de la fila {rowNumber}, columna {periodColumn.Column} no es valida en el forecast.");
                }

                if (quantity == 0)
                {
                    continue;
                }

                entries.Add(new ForecastEntry(article, periodColumn.Period, quantity));
            }
        }

        var periods = periodColumns.Select(column => column.Period).Distinct().OrderBy(period => period).ToArray();
        return new ForecastWorkbook(entries, periods, InferGranularity(periodColumns, periods))
        {
            Descriptions = descriptions,
        };
    }

    private static int FindOptionalColumn(IXLWorksheet worksheet, int headerRow, int lastColumn, string[] headerCandidates)
    {
        var candidates = headerCandidates.Select(NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastColumn; column++)
        {
            if (candidates.Contains(NormalizeHeader(worksheet.Cell(headerRow, column).GetString())))
            {
                return column;
            }
        }

        return 0;
    }

    private static void CaptureDescription(
        IXLWorksheet worksheet,
        int rowNumber,
        int descriptionColumn,
        string article,
        Dictionary<string, string> descriptions)
    {
        if (descriptionColumn <= 0 || descriptions.ContainsKey(article))
        {
            return;
        }

        var description = worksheet.Cell(rowNumber, descriptionColumn).GetString().Trim();
        if (!string.IsNullOrWhiteSpace(description))
        {
            descriptions[article] = description;
        }
    }

    private static (int HeaderRow, int ArticleColumn) FindHeader(IXLWorksheet worksheet, int lastColumn, int lastRow)
    {
        var candidates = ArticleHeaderCandidates
            .Select(NormalizeHeader)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scanLimit = Math.Min(lastRow, MaxHeaderScanRows);
        for (var row = 1; row <= scanLimit; row++)
        {
            for (var column = 1; column <= lastColumn; column++)
            {
                var value = worksheet.Cell(row, column).GetString();
                if (!string.IsNullOrWhiteSpace(value) && candidates.Contains(NormalizeHeader(value)))
                {
                    return (row, column);
                }
            }
        }

        throw new WorkbookValidationException(
            "No se encontro la columna de articulo ('Articulo', 'Prod' o 'Item code') en el forecast.");
    }

    private static ForecastGranularity InferGranularity(IReadOnlyList<PeriodColumn> periodColumns, IReadOnlyList<DateOnly> periods)
    {
        if (periodColumns.Count > 0 && periodColumns.All(column => column.IsMonthly))
        {
            return ForecastGranularity.Monthly;
        }

        // Months stored as real dates land on the first of each month across distinct months.
        if (periods.Count >= 2 && periods.All(period => period.Day == 1))
        {
            return ForecastGranularity.Monthly;
        }

        return ForecastGranularity.Weekly;
    }

    private static bool TryParsePeriodHeader(IXLCell cell, out DateOnly period, out bool isMonthly)
    {
        isMonthly = false;

        if (cell.TryGetValue<DateTime>(out var dateTime))
        {
            period = DateOnly.FromDateTime(dateTime);
            return true;
        }

        var text = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            period = default;
            return false;
        }

        var dateFormats = new[] { "dd.MM.yy", "d.M.yy", "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd" };
        if (DateOnly.TryParseExact(text, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out period))
        {
            return true;
        }

        // Month-year headers like "Jun 2026" / "June 2026" / Spanish month names.
        var monthFormats = new[] { "MMM yyyy", "MMMM yyyy", "MMM-yyyy", "MMMM-yyyy", "MMM. yyyy", "MMM yy" };
        foreach (var culture in MonthCultures)
        {
            if (DateOnly.TryParseExact(text, monthFormats, culture, DateTimeStyles.None, out var monthDate))
            {
                period = new DateOnly(monthDate.Year, monthDate.Month, 1);
                isMonthly = true;
                return true;
            }
        }

        period = default;
        return false;
    }

    private static bool TryGetDecimal(IXLCell cell, out decimal value)
    {
        if (cell.TryGetValue<double>(out var numericValue))
        {
            value = Convert.ToDecimal(numericValue, CultureInfo.InvariantCulture);
            return true;
        }

        if (decimal.TryParse(cell.GetString().Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value) ||
            decimal.TryParse(cell.GetString().Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
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

    private readonly record struct PeriodColumn(int Column, DateOnly Period, bool IsMonthly);
}
