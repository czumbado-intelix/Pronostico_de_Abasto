namespace PronosticosAbasto.Core.Configuration;

/// <summary>
/// Storage zones that should not count as available inventory for replenishment.
/// The app seeds these per company and lets operators edit the list in Ajustes.
/// </summary>
public static class InventoryExcludedZoneDefaults
{
    public static IReadOnlyList<string> Zones { get; } =
    [
        "ZA23-ZONA ALMACENAJE DE MUELLES EXPEDICION - CLIRO",
        "ZA30-ZONA ALMACENAJE DAÑADOS - CLIRO",
        "ZA38-ZONA ALMACENAJE CONTROL INVENTARIO - CLIRO",
        "ZA39-ZONA ALMACENAJE CONTENEDOR - CLIRO",
        "ZA40-ZONA ALMACENAJE TARIMAS - CLIRO",
        "ZA55-ZA55 - ZONA ALMACENAJE 5 - GUACIMA",
    ];
}
