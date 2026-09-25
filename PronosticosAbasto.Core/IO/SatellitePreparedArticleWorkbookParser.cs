using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace PronosticosAbasto.Core.IO;

public sealed record SatellitePreparedArticle(string Article, string Description);

public sealed record SatellitePreparedArticleWorkbook(IReadOnlyList<SatellitePreparedArticle> Articles);

public sealed class SatellitePreparedArticleWorkbookParser
{
    public SatellitePreparedArticleWorkbook Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var workbook = new XLWorkbook(stream);
        if (!workbook.Worksheets.Any())
        {
            throw new WorkbookValidationException("El archivo de articulos alistados no contiene hojas.");
        }

        IXLWorksheet? worksheet = null;
        var articleColumn = 0;
        foreach (var candidate in workbook.Worksheets)
        {
            if (TryFindColumn(candidate, out var article, "Articulo", "Artículo"))
            {
                worksheet = candidate;
                articleColumn = article;
                break;
            }
        }

        if (worksheet is null)
        {
            throw new WorkbookValidationException(
                "No se encontro una hoja con la columna requerida: Articulo.");
        }

        TryFindColumn(worksheet, out var descriptionColumn, "Descripcion", "Descripción", "Description");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var articles = new Dictionary<string, SatellitePreparedArticle>(StringComparer.OrdinalIgnoreCase);

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var article = worksheet.Cell(rowNumber, articleColumn).GetString().Trim();
            if (string.IsNullOrWhiteSpace(article))
            {
                continue;
            }

            var description = descriptionColumn > 0
                ? worksheet.Cell(rowNumber, descriptionColumn).GetString().Trim()
                : string.Empty;

            if (!articles.TryGetValue(article, out var existing) ||
                (string.IsNullOrWhiteSpace(existing.Description) && !string.IsNullOrWhiteSpace(description)))
            {
                articles[article] = new SatellitePreparedArticle(article, description);
            }
        }

        return new SatellitePreparedArticleWorkbook(articles.Values.ToArray());
    }

    private static bool TryFindColumn(IXLWorksheet worksheet, out int column, params string[] headerCandidates)
    {
        var headerRow = worksheet.Row(1);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var normalizedCandidates = headerCandidates
            .Select(NormalizeHeader)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var candidate = 1; candidate <= lastColumn; candidate++)
        {
            var value = headerRow.Cell(candidate).GetString();
            if (normalizedCandidates.Contains(NormalizeHeader(value)))
            {
                column = candidate;
                return true;
            }
        }

        column = 0;
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
}
