using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Tests.Analysis;

public class ScenarioSimulatorTests
{
    private static readonly string[] SatelliteZones = ["ZA47"];

    private static ScenarioCompanyData Epa() => new(
        Forecast:
        [
            new ForecastEntry("A", new DateOnly(2026, 6, 1), 100),
            new ForecastEntry("A", new DateOnly(2026, 6, 8), 50),
        ],
        Inventory:
        [
            new InventoryPosition("A", "ZP", 30),
            new InventoryPosition("A", "ZA47", 200),
        ],
        SatelliteZones: SatelliteZones,
        IgnoredStorageZones: [],
        StartWeek: new DateOnly(2026, 6, 1));

    [Fact]
    public void Simulate_projects_demand_and_feasible_transfer_load_per_period()
    {
        var result = new ScenarioSimulator().Simulate(Epa(), cofersa: null, horizonMonths: 1, safetyBufferPercent: 0);

        Assert.True(result.HasEpa);
        Assert.False(result.HasCofersa);
        Assert.Equal(2, result.Periods.Count);

        // Week 1: demand 100, principal 30 -> shortage 70, satellite 200 -> load 70.
        Assert.Equal(100m, result.Periods[0].EpaDemand);
        Assert.Equal(70m, result.Periods[0].TransferLoad);
        // Week 2: demand 50 -> shortage 20 -> load 20.
        Assert.Equal(50m, result.Periods[1].EpaDemand);
        Assert.Equal(20m, result.Periods[1].TransferLoad);
    }

    [Fact]
    public void Safety_buffer_inflates_demand_and_load()
    {
        var result = new ScenarioSimulator().Simulate(Epa(), cofersa: null, horizonMonths: 1, safetyBufferPercent: 100);

        // Buffer doubles demand: week 1 demand 200, shortage 170, satellite 200 -> load 170.
        Assert.Equal(200m, result.Periods[0].EpaDemand);
        Assert.Equal(170m, result.Periods[0].TransferLoad);
    }
}
