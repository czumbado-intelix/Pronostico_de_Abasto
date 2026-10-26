namespace PronosticosAbasto.Core.Analysis;

/// <summary>
/// Desglosa y da formato legible a los numeros de expedicion asociados a un articulo
/// en el modulo de Expediciones sin alterar la cadena completa usada para busqueda y portapapeles.
/// </summary>
public static class ExpeditionNumberFormatter
{
    public const string EmptyPlaceholder = "Sin expedicion";

    private static readonly char[] Separators = [';', ',', '\r', '\n'];

    public static IReadOnlyList<string> ExtractNumbers(string? rawExpeditionNumber)
    {
        if (string.IsNullOrWhiteSpace(rawExpeditionNumber))
        {
            return Array.Empty<string>();
        }

        return rawExpeditionNumber
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !IsPlaceholder(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string FormatPrimary(IReadOnlyList<string> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return numbers.Count > 0 ? numbers[0] : EmptyPlaceholder;
    }

    public static string FormatExtraBadge(IReadOnlyList<string> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return numbers.Count > 1 ? $"+{numbers.Count - 1}" : string.Empty;
    }

    public static string FormatCountTitle(IReadOnlyList<string> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return numbers.Count switch
        {
            0 => "Sin expedición asignada",
            1 => "1 expedición asignada",
            _ => $"{numbers.Count:N0} expediciones asignadas",
        };
    }

    public static string FormatCopyButtonText(IReadOnlyList<string> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return numbers.Count switch
        {
            0 => string.Empty,
            1 => "Copiar expedición",
            _ => $"Copiar las {numbers.Count:N0} expediciones",
        };
    }

    public static string FormatMultiline(IReadOnlyList<string> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return numbers.Count > 0 ? string.Join(Environment.NewLine, numbers) : EmptyPlaceholder;
    }

    public static string FormatTooltip(IReadOnlyList<string> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return numbers.Count switch
        {
            0 => EmptyPlaceholder,
            1 => $"{numbers[0]} (clic para opciones)",
            _ => $"{FormatCountTitle(numbers)} (clic para ver todas):{Environment.NewLine}{string.Join(Environment.NewLine, numbers)}",
        };
    }

    private static bool IsPlaceholder(string token) =>
        string.Equals(token, EmptyPlaceholder, StringComparison.OrdinalIgnoreCase)
        || string.Equals(token, "Sin expedición", StringComparison.OrdinalIgnoreCase)
        || string.Equals(token, "Sin valor", StringComparison.OrdinalIgnoreCase);
}
