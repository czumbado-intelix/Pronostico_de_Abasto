using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.IO;

public sealed class InventoryWorkbookParser
{
    public InventoryWorkbook Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var workbook = new XLWorkbook(stream);
        if (!workbook.Worksheets.Any())
        {
            throw new WorkbookValidationException("El archivo de inventario no contiene hojas.");
        }

        // Pick the first sheet that actually carries the inventory columns. Some
        // exports (e.g. Cofersa) put a pivot/summary sheet first and the detail
        // on a later sheet, so we cannot assume the first worksheet is the data.
        IXLWorksheet? worksheet = null;
        var articleColumn = 0;
        var zoneColumn = 0;
        var quantityColumn = 0;

        foreach (var candidate in workbook.Worksheets)
        {
            if (TryFindColumn(candidate, out var article, "Articulo") &&
                TryFindColumn(candidate, out var zone, "Zona de almacenaje", "Zona Almacenaje") &&
                TryFindColumn(candidate, out var quantity, "Cantidad unidades"))
            {
                worksheet = candidate;
                articleColumn = article;
                zoneColumn = zone;
                quantityColumn = quantity;
                break;
            }
        }

        if (worksheet is null)
        {
            throw new WorkbookValidationException(
                "No se encontro una hoja de inventario con las columnas requeridas: Articulo, Zona de almacenaje y Cantidad unidades.");
        }

        TryFindColumn(worksheet, out var descriptionColumn, "Descripcion", "Description");
        TryFindColumn(worksheet, out var palletColumn, "Pallet", "Paleta", "Palet");
        TryFindColumn(worksheet, out var locationColumn, "Ubicacion", "Ubicación");
        TryFindColumn(worksheet, out var validationDateColumn, "Fecha de validacion", "Fecha validacion", "Fecha de validación");
        TryFindColumn(worksheet, out var palletTypeColumn, "Tipo pallet", "Tipo de pallet", "Tipo paleta", "Tipo de paleta");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var positions = new List<InventoryPosition>();
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            var pallet = palletColumn > 0
                ? worksheet.Cell(rowNumber, palletColumn).GetString().Trim()
                : string.Empty;
            var location = locationColumn > 0
                ? worksheet.Cell(rowNumber, locationColumn).GetString().Trim()
                : string.Empty;
            var palletType = palletTypeColumn > 0
                ? worksheet.Cell(rowNumber, palletTypeColumn).GetString().Trim()
                : string.Empty;

            if (IsMerma(palletType))
            {
                continue;
            }

            var zone = worksheet.Cell(rowNumber, zoneColumn).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(description) && !descriptions.ContainsKey(article))
            {
                descriptions[article] = description;
            }

            var quantityCell = worksheet.Cell(rowNumber, quantityColumn);
            if (!TryGetDecimal(quantityCell, out var quantity))
            {
                throw new WorkbookValidationException(
                    $"La cantidad de la fila {rowNumber} no es valida en el archivo de inventario.");
            }

            DateOnly? validationDate = null;
            if (validationDateColumn > 0 &&
                !TryGetDate(worksheet.Cell(rowNumber, validationDateColumn), out validationDate))
            {
                throw new WorkbookValidationException(
                    $"La fecha de validacion de la fila {rowNumber} no es valida en el archivo de inventario.");
            }

            positions.Add(new InventoryPosition(
                article,
                string.IsNullOrWhiteSpace(zone) ? "Sin zona" : zone,
                quantity,
                description,
                pallet,
                validationDate,
                palletType,
                location));
        }

        return new InventoryWorkbook(positions)
        {
            Descriptions = descriptions,
        };
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

        if (decimal.TryParse(cell.GetString().Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value) ||
            decimal.TryParse(cell.GetString().Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetDate(IXLCell cell, out DateOnly? value)
    {
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString()))
        {
            value = null;
            return true;
        }

        if (cell.TryGetValue<DateTime>(out var dateValue))
        {
            value = DateOnly.FromDateTime(dateValue);
            return true;
        }

        var text = cell.GetString().Trim();
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateValue) ||
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateValue))
        {
            value = DateOnly.FromDateTime(dateValue);
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsMerma(string palletType) =>
        NormalizeHeader(palletType).Contains("merma", StringComparison.OrdinalIgnoreCase);

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
