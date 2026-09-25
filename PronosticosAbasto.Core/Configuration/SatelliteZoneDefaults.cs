namespace PronosticosAbasto.Core.Configuration;

/// <summary>
/// Default external (satellite) storage zones used to seed a company the first
/// time it is configured. The operator can override these per company from the
/// app; the defaults only guarantee the principal/external split is never empty.
/// </summary>
public static class SatelliteZoneDefaults
{
    public static IReadOnlyList<string> Zones { get; } =
    [
        "ZA31-ZONA ALMACENAJE ALGEFISA - CLIRO",
        "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA",
        "ZA47-ZA47 - ZONA ALMACENAJE 1 - GUACIMA",
        "ZA48-ZA48 - ZONA ALMACENAJE 2 - GUACIMA",
        "ZA49-ZA49 - ZONA ALMACENAJE 3 - GUACIMA",
        "ZA50-ZA50 - ZONA ALMACENAJE 4 - GUACIMA",
        "ZA52-ZONA ALMACENAJE MUELLE RECEPCION - GUACIMA",
        "ZA55-ZA55 - ZONA ALMACENAJE 5 - GUACIMA",
    ];
}
