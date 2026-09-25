namespace PronosticosAbasto.Core.Tables;

/// <summary>
/// El conjunto de columnas de una tabla, con su orden vigente.
/// </summary>
/// <remarks>
/// <para>
/// Antes cada tabla (Analisis, Traslados, Expediciones, Comparativa) repetia tres
/// metodos casi identicos en <c>MainPageViewModel</c>: uno para repoblar las
/// opciones de filtro, otro con la cadena de <c>Matches</c> columna por columna y
/// otro con un <c>switch</c> de claves de orden. Doce metodos para cuatro tablas.
/// Aqui se declara la tabla una sola vez y el recorrido es el mismo para todas.
/// </para>
/// <para>
/// El orden se pide por <b>instancia de filtro</b>, no por una clave de texto: la
/// UI ya tiene el filtro a mano en cada encabezado, asi que no hay strings que
/// puedan dejar de coincidir en silencio.
/// </para>
/// </remarks>
/// <typeparam name="TRow">Tipo de fila que proyecta la tabla.</typeparam>
public sealed class TableView<TRow>
{
    private readonly TableColumn<TRow>[] _columns;

    public TableView(params TableColumn<TRow>[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var duplicate = columns
            .GroupBy(column => column.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"La columna '{duplicate.Key}' esta declarada mas de una vez.", nameof(columns));
        }

        _columns = columns;
    }

    public IReadOnlyList<TableColumn<TRow>> Columns => _columns;

    /// <summary>Columna por la que se esta ordenando, o null si no hay orden aplicado.</summary>
    public TableColumn<TRow>? SortColumn { get; private set; }

    public bool SortAscending { get; private set; } = true;

    /// <summary>Si alguna columna esta filtrando.</summary>
    public bool AnyFilterActive => _columns.Any(column => column.Filter.IsActive);

    /// <summary>La columna cuyo filtro es exactamente esta instancia, si pertenece a esta tabla.</summary>
    public TableColumn<TRow>? ColumnFor(IColumnValueFilter filter) =>
        _columns.FirstOrDefault(column => ReferenceEquals(column.Filter, filter));

    /// <summary>
    /// Ordena por la columna dueña de <paramref name="filter"/>. Devuelve false si
    /// el filtro es de otra tabla, para que quien despacha pruebe la siguiente.
    /// </summary>
    public bool TrySetSort(IColumnValueFilter filter, bool ascending)
    {
        if (ColumnFor(filter) is not { } column)
        {
            return false;
        }

        SortColumn = column;
        SortAscending = ascending;
        return true;
    }

    public void ClearSort() => SortColumn = null;

    /// <summary>Deja todas las columnas sin filtro. No toca el orden.</summary>
    public void ClearFilters()
    {
        foreach (var column in _columns)
        {
            column.Filter.ClearFilter();
        }
    }

    /// <summary>Repuebla la lista de valores elegibles de cada columna.</summary>
    public void RefreshOptions(IEnumerable<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var materialized = rows as IReadOnlyCollection<TRow> ?? rows.ToArray();
        foreach (var column in _columns)
        {
            column.Filter.SetOptions(materialized.Select(column.Text));
        }
    }

    /// <summary>Una fila pasa si pasa el filtro de todas las columnas.</summary>
    public bool Matches(TRow row) => Array.TrueForAll(_columns, column => column.Matches(row));

    /// <summary>
    /// Aplica el orden vigente. Las columnas numericas ordenan por numero y dejan
    /// los valores sin dato al final en ambas direcciones; el resto ordena por
    /// texto ignorando mayusculas.
    /// </summary>
    public IEnumerable<TRow> ApplySort(IEnumerable<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (SortColumn is not { } column)
        {
            return rows;
        }

        if (column.Number is not { } number)
        {
            return SortAscending
                ? rows.OrderBy(column.Text, StringComparer.OrdinalIgnoreCase)
                : rows.OrderByDescending(column.Text, StringComparer.OrdinalIgnoreCase);
        }

        var withValueFirst = rows.OrderBy(row => number(row).HasValue ? 0 : 1);
        return SortAscending
            ? withValueFirst.ThenBy(row => number(row).GetValueOrDefault())
            : withValueFirst.ThenByDescending(row => number(row).GetValueOrDefault());
    }

    /// <summary>Filtra y ordena en un paso.</summary>
    public IReadOnlyList<TRow> Apply(IEnumerable<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return ApplySort(rows.Where(Matches)).ToArray();
    }
}
