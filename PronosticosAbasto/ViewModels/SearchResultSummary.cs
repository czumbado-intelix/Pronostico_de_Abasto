namespace PronosticosAbasto.ViewModels;

/// <summary>
/// Resultado de una busqueda con varias alternativas (tipicamente, una lista de
/// articulos pegada): cuantas aparecieron y cuales no.
/// </summary>
/// <param name="Badge">Texto corto para mostrar dentro de la caja, por ejemplo "18/20". Corto a proposito: va dentro de la caja de texto.</param>
/// <param name="Detail">Explicacion completa para el panel desplegable.</param>
/// <param name="Unmatched">Alternativas sin ninguna fila, en el orden en que se escribieron.</param>
public sealed record SearchResultSummary(string Badge, string Detail, IReadOnlyList<string> Unmatched)
{
    /// <summary>Busqueda simple o vacia: no hay nada que resumir.</summary>
    public static SearchResultSummary None { get; } = new(string.Empty, string.Empty, []);

    public bool IsVisible => Badge.Length > 0;

    public bool HasUnmatched => Unmatched.Count > 0;

    public static SearchResultSummary From(int alternativeCount, IReadOnlyList<string> unmatched)
    {
        ArgumentNullException.ThrowIfNull(unmatched);

        // Una sola busqueda: el conteo de filas visible ya dice si encontro algo.
        if (alternativeCount < 2)
        {
            return None;
        }

        var found = alternativeCount - unmatched.Count;
        if (unmatched.Count == 0)
        {
            return new SearchResultSummary(
                $"{found:N0}/{alternativeCount:N0}",
                $"Se encontraron los {alternativeCount:N0} buscados.",
                unmatched);
        }

        return new SearchResultSummary(
            $"{found:N0}/{alternativeCount:N0}",
            $"Se encontraron {found:N0} de {alternativeCount:N0}. {unmatched.Count:N0} sin resultado en esta pestaña:",
            unmatched);
    }
}
