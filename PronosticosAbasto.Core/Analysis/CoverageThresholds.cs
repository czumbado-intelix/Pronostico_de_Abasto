namespace PronosticosAbasto.Core.Analysis;

/// <summary>
/// Configurable traffic-light cutoffs as a percentage of the forecast.
/// Coverage = inventory / forecast. Three bands:
/// Red when there is no stock (inventory &lt;= 0) or coverage &lt; <see cref="RedPercent"/>%,
/// Green when coverage &gt;= <see cref="HealthyPercent"/>%, and Yellow in between.
/// Defaults (red 0%, green 100%) mean red only when there is no stock at all.
/// </summary>
public readonly record struct CoverageThresholds
{
    public CoverageThresholds(decimal redPercent, decimal healthyPercent)
    {
        if (redPercent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(redPercent), redPercent, "El umbral de rojo no puede ser negativo.");
        }

        if (healthyPercent < redPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(healthyPercent), healthyPercent, "El umbral de amarillo no puede ser menor que el de rojo.");
        }

        RedPercent = redPercent;
        HealthyPercent = healthyPercent;
    }

    /// <summary>Coverage below this % is Red. 0 means red only when there is no stock.</summary>
    public decimal RedPercent { get; }

    /// <summary>Coverage at or above this % is Green.</summary>
    public decimal HealthyPercent { get; }

    public static CoverageThresholds Default => new(0m, 100m);
}
