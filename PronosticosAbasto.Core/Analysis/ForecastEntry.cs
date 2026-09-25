namespace PronosticosAbasto.Core.Analysis;

public sealed record ForecastEntry(string Article, DateOnly Week, decimal Quantity);
