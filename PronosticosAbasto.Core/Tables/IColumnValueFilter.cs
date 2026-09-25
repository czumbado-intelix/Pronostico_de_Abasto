namespace PronosticosAbasto.Core.Tables;

/// <summary>
/// Lo unico que el motor de tablas necesita saber de un filtro de columna.
/// </summary>
/// <remarks>
/// La implementacion real vive en la UI (<c>TextColumnFilter</c>, que ademas es
/// observable y tiene el borrador de seleccion tipo Excel). El motor se queda en
/// el Core y por eso solo depende de esta interfaz.
/// </remarks>
public interface IColumnValueFilter
{
    /// <summary>Si el filtro esta restringiendo algo ahora mismo.</summary>
    bool IsActive { get; }

    /// <summary>Decide si un valor de celda pasa el filtro.</summary>
    bool Matches(string? value);

    /// <summary>Repuebla la lista de valores disponibles para elegir.</summary>
    void SetOptions(IEnumerable<string?> values);

    /// <summary>Deja el filtro sin restricciones.</summary>
    void ClearFilter();
}
