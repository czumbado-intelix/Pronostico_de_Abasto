using System.Runtime.CompilerServices;

namespace PronosticosAbasto.Core.Tables;

/// <summary>
/// Texto de busqueda de cada fila, normalizado una sola vez.
/// </summary>
/// <remarks>
/// <para>
/// Normalizar (mayusculas, sin tildes) es lo caro de buscar. Con 16k-34k filas,
/// hacerlo de nuevo en cada tecla para cada campo de cada fila se nota. Aqui se
/// calcula la primera vez que se pide y se reutiliza mientras la fila exista.
/// </para>
/// <para>
/// La cache es debil (<see cref="ConditionalWeakTable{TKey, TValue}"/>): cuando el
/// modulo reconstruye sus filas, las viejas se liberan solas con su texto. Si un
/// campo buscable de una fila cambia en el lugar (un comentario editado), quien
/// lo cambia llama a <see cref="Invalidate"/>, o a <see cref="InvalidateAll"/> si
/// cambio muchas filas de una vez.
/// </para>
/// </remarks>
/// <typeparam name="TRow">Tipo de fila del modulo.</typeparam>
public sealed class SearchIndex<TRow>
    where TRow : class
{
    private readonly Func<TRow, IEnumerable<string?>> _fields;
    private ConditionalWeakTable<TRow, SearchKey> _keys = new();

    /// <param name="fields">Campos de la fila en los que busca el modulo.</param>
    public SearchIndex(Func<TRow, IEnumerable<string?>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = fields;
    }

    /// <summary>Texto normalizado de la fila (calculado una vez).</summary>
    public SearchKey KeyFor(TRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return _keys.GetValue(row, Build);
    }

    /// <summary>Olvida el texto de una fila cuyo contenido buscable cambio.</summary>
    public void Invalidate(TRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _keys.Remove(row);
    }

    /// <summary>Olvida el texto de todas las filas (cambio masivo sin pasar fila por fila).</summary>
    public void InvalidateAll() => _keys = new ConditionalWeakTable<TRow, SearchKey>();

    /// <summary>Las filas que coinciden con la consulta, en el mismo orden.</summary>
    public IEnumerable<TRow> Filter(IEnumerable<TRow> rows, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(query);
        return query.IsEmpty ? rows : Search(rows as IReadOnlyList<TRow> ?? rows.ToArray(), query).Rows;
    }

    /// <summary>Las alternativas que no encontraron ninguna fila, en el orden en que se escribieron.</summary>
    public IReadOnlyList<string> UnmatchedAlternatives(IEnumerable<TRow> rows, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(query);
        return query.IsEmpty ? [] : Search(rows as IReadOnlyList<TRow> ?? rows.ToArray(), query).Unmatched;
    }

    /// <summary>
    /// Filtra y, de paso, anota que alternativas no encontraron nada; todo en
    /// una sola pasada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los codigos de una lista pegada se buscan en un diccionario por palabra
    /// completa, no uno por uno: pegar 500 articulos cuesta lo mismo por fila
    /// que pegar 5. Las alternativas de texto ("codo pvc") se comparan una por
    /// una, pero una vez que una alternativa ya encontro fila y la fila ya entro,
    /// no se vuelve a probar.
    /// </para>
    /// <para>
    /// Si la consulta trae una lectura "todas las palabras"
    /// (<see cref="SearchQuery.AllWordsReading"/>) y alguna fila la cumple, gana
    /// esa lectura.
    /// </para>
    /// </remarks>
    public SearchResult<TRow> Search(IReadOnlyList<TRow> rows, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(query);

        if (query.IsEmpty)
        {
            return new SearchResult<TRow>(rows, [], query);
        }

        if (query.AllWordsReading is { } allWords && rows.Any(row => allWords.Matches(KeyFor(row))))
        {
            query = allWords;
        }

        var count = query.AlternativeCount;
        var found = new bool[count];
        var byToken = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var byText = new List<int>();
        for (var index = 0; index < count; index++)
        {
            if (query.IsWholeToken(index))
            {
                var term = query.Alternatives[index][0];
                if (!byToken.TryGetValue(term, out var indexes))
                {
                    byToken[term] = indexes = [];
                }

                indexes.Add(index);
            }
            else
            {
                byText.Add(index);
            }
        }

        var matches = new List<TRow>();
        foreach (var row in rows)
        {
            var key = KeyFor(row);
            var included = false;

            if (byToken.Count > 0)
            {
                foreach (var token in key.Tokens)
                {
                    if (byToken.TryGetValue(token, out var indexes))
                    {
                        included = true;
                        foreach (var index in indexes)
                        {
                            found[index] = true;
                        }
                    }
                }
            }

            foreach (var index in byText)
            {
                if (included && found[index])
                {
                    continue;
                }

                if (query.MatchesAlternative(index, key))
                {
                    included = true;
                    found[index] = true;
                }
            }

            if (included)
            {
                matches.Add(row);
            }
        }

        var unmatched = Enumerable.Range(0, count)
            .Where(index => !found[index])
            .Select(query.DisplayText)
            .ToArray();
        return new SearchResult<TRow>(matches, unmatched, query);
    }

    private SearchKey Build(TRow row) => new(SearchQuery.Normalize(string.Join(' ', _fields(row))));
}

/// <summary>Filas encontradas y alternativas sin resultado de una busqueda.</summary>
/// <param name="Rows">Filas que coinciden, en el orden original.</param>
/// <param name="Unmatched">Alternativas que no encontraron ninguna fila.</param>
/// <param name="AppliedQuery">La lectura de la consulta que se aplico (la lista, o "todas las palabras").</param>
public sealed record SearchResult<TRow>(IReadOnlyList<TRow> Rows, IReadOnlyList<string> Unmatched, SearchQuery AppliedQuery);
