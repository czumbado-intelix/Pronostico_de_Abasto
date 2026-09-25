namespace PronosticosAbasto.Core.Tables;

/// <summary>
/// Una columna filtrable y ordenable de una tabla: como se lee la celda, con que
/// filtro se compara y, si es numerica, con que clave se ordena.
/// </summary>
/// <typeparam name="TRow">Tipo de fila que proyecta la tabla.</typeparam>
public sealed class TableColumn<TRow>
{
    /// <param name="key">Identificador estable de la columna. Solo para diagnostico y pruebas; la UI referencia la columna por su filtro, no por este texto.</param>
    /// <param name="text">Texto visible de la celda: alimenta el filtro, la lista de valores y el orden alfabetico.</param>
    /// <param name="filter">Filtro de la columna.</param>
    /// <param name="number">Clave numerica opcional. Si esta presente, la columna ordena por numero en vez de por texto.</param>
    public TableColumn(
        string key,
        Func<TRow, string> text,
        IColumnValueFilter filter,
        Func<TRow, decimal?>? number = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(filter);

        Key = key;
        Text = text;
        Filter = filter;
        Number = number;
    }

    public string Key { get; }

    public Func<TRow, string> Text { get; }

    public IColumnValueFilter Filter { get; }

    public Func<TRow, decimal?>? Number { get; }

    /// <summary>La columna ordena por numero cuando tiene clave numerica.</summary>
    public bool IsNumeric => Number is not null;

    internal bool Matches(TRow row) => Filter.Matches(Text(row));
}
