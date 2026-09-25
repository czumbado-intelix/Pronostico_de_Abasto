namespace PronosticosAbasto.Core.IO;

/// <summary>
/// A single expedition/dispatch line. Quantity is the missing amount that still
/// needs coverage; requested/prepared preserve the source columns when present.
/// </summary>
public sealed record ExpeditionLine(
    string Article,
    string Description,
    decimal Quantity,
    string ExpeditionNumber = "",
    decimal RequestedQuantity = 0m,
    decimal PreparedQuantity = 0m,
    string Situation = "");

/// <summary>
/// The "EXPEDICIONES" sheet of the dispatch report: one line per expedition
/// (articles repeat and are NOT aggregated).
/// </summary>
public sealed record ExpedicionesWorkbook(IReadOnlyList<ExpeditionLine> Lines);
