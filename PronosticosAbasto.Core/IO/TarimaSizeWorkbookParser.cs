using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace PronosticosAbasto.Core.IO;

public sealed record TarimaSizeEntry(string Article, string Description, string SizeCode)
{
    public int SizeNumber => TarimaSizeNormalizer.ToNumber(SizeCode);
}

public sealed record TarimaSizeWorkbook(IReadOnlyList<TarimaSizeEntry> Entries);

public sealed class TarimaSizeWorkbookParser
{
    public TarimaSizeWorkbook Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var workbook = new XLWorkbook(stream);
        if (!workbook.Worksheets.Any())
        {
            throw new WorkbookValidationException("El archivo de tamanos de tarima no contiene hojas.");
        }

        IXLWorksheet? worksheet = null;
        var articleColumn = 0;
        var sizeColumn = 0;
        foreach (var candidate in workbook.Worksheets)
        {
            if (TryFindColumn(candidate, out var article, "Articulo", "Artículo") &&
                TryFindColumn(candidate, out var size, "Tamano tarima", "Tamaño tarima", "Tamano de tarima", "Tamaño de tarima"))
            {
                worksheet = candidate;
                articleColumn = article;
                sizeColumn = size;
                break;
            }
        }

        if (worksheet is null)
        {
            throw new WorkbookValidationException(
                "No se encontro una hoja con las columnas requeridas: Articulo y Tamano Tarima.");
        }

        TryFindColumn(worksheet, out var descriptionColumn, "Descripcion", "Descripción", "Description");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var entries = new Dictionary<string, TarimaSizeEntry>(StringComparer.OrdinalIgnoreCase);

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var article = worksheet.Cell(rowNumber, articleColumn).GetString().Trim();
            if (string.IsNullOrWhiteSpace(article))
            {
                continue;
            }

            var rawSize = worksheet.Cell(rowNumber, sizeColumn).GetString().Trim();
            if (string.IsNullOrWhiteSpace(rawSize))
            {
                continue;
            }

            if (!TarimaSizeNormalizer.TryNormalize(rawSize, out var sizeCode))
            {
                throw new WorkbookValidationException(
                    $"El tamano de tarima de la fila {rowNumber} no es valido. Use S/1/Sencilla, D/2/Doble o T/3/Triple.");
            }

            var description = descriptionColumn > 0
                ? worksheet.Cell(rowNumber, descriptionColumn).GetString().Trim()
                : string.Empty;

            entries[article] = new TarimaSizeEntry(article, description, sizeCode);
        }

        return new TarimaSizeWorkbook(entries.Values.ToArray());
    }

    private static bool TryFindColumn(IXLWorksheet worksheet, out int column, params string[] headerCandidates)
    {
        var headerRow = worksheet.Row(1);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var normalizedCandidates = headerCandidates
            .Select(TarimaSizeNormalizer.NormalizeToken)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var candidate = 1; candidate <= lastColumn; candidate++)
        {
            var value = headerRow.Cell(candidate).GetString();
            if (normalizedCandidates.Contains(TarimaSizeNormalizer.NormalizeToken(value)))
            {
                column = candidate;
                return true;
            }
        }

        column = 0;
        return false;
    }
}

public static class TarimaSizeNormalizer
{
    public static bool TryNormalize(string value, out string sizeCode)
    {
        var token = NormalizeToken(value);
        sizeCode = token switch
        {
            "s" or "1" or "sencilla" or "simple" => "S",
            "d" or "2" or "doble" => "D",
            "t" or "3" or "triple" => "T",
            _ => string.Empty,
        };

        return sizeCode.Length > 0;
    }

    public static string NormalizeOrEmpty(string value) =>
        TryNormalize(value, out var sizeCode) ? sizeCode : string.Empty;

    public static int ToNumber(string sizeCode) =>
        NormalizeOrEmpty(sizeCode) switch
        {
            "S" => 1,
            "D" => 2,
            "T" => 3,
            _ => 0,
        };

    public static string NormalizeToken(string value)
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
