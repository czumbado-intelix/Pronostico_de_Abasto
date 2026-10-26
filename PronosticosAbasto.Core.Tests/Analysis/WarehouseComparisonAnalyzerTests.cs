using PronosticosAbasto.Core.Analysis;
using PronosticosAbasto.Core.Configuration;
using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Core.Tests.Analysis;

public class WarehouseComparisonAnalyzerTests
{
    private static readonly DateOnly StartWeek = new(2026, 8, 31);

    [Fact]
    public void Analyze_splits_valid_inventory_between_olo_and_servica()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[]
        {
            StartWeek,
            StartWeek.AddDays(7),
            StartWeek.AddDays(14),
        };
        var inventory = new[]
        {
            new InventoryPosition("100", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 5, "Articulo 100"),
            new InventoryPosition("100", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 70, "Articulo 100"),
        };
        var forecast = new[]
        {
            new ForecastEntry("100", StartWeek, 25),
            new ForecastEntry("100", StartWeek.AddDays(7), 25),
            new ForecastEntry("100", StartWeek.AddDays(14), 25),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal("100", row.Article);
        Assert.Equal("Articulo 100", row.Description);
        Assert.Equal(5, row.OloQuantity);
        Assert.Equal(70, row.ServicaQuantity);
        Assert.Equal(75, row.TotalQuantity);
        Assert.Equal(75, row.ForecastQuantity);
        Assert.Equal(0, row.CoveragePeriods);
        Assert.True(row.HasFutureForecast);
        Assert.Equal(string.Empty, row.AutomaticComment);
    }

    [Fact]
    public void Analyze_does_not_comment_when_article_requests_from_start()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = Enumerable.Range(0, 6).Select(offset => StartWeek.AddDays(offset * 7)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("100022301", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 20),
        };
        var forecast = new[]
        {
            new ForecastEntry("100022301", StartWeek, 5),
            new ForecastEntry("100022301", StartWeek.AddDays(14), 5),
            new ForecastEntry("100022301", StartWeek.AddDays(21), 5),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.True(row.HasFutureForecast);
        Assert.Equal(string.Empty, row.AutomaticComment);
    }

    [Fact]
    public void Analyze_does_not_comment_requested_period_before_four_week_minimum()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = Enumerable.Range(0, 6).Select(offset => StartWeek.AddDays(offset * 7)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("100022301", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 20),
        };
        var forecast = new[]
        {
            new ForecastEntry("100022301", StartWeek.AddDays(14), 5),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.True(row.HasFutureForecast);
        Assert.Equal(string.Empty, row.AutomaticComment);
    }

    [Fact]
    public void Analyze_comments_first_requested_period_after_initial_gap()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var start = new DateOnly(2026, 10, 5);
        var periods = Enumerable.Range(0, 13).Select(offset => start.AddDays(offset * 7)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("100022301", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 113),
        };
        var forecast = new[]
        {
            new ForecastEntry("100022301", new DateOnly(2026, 12, 21), 1),
            new ForecastEntry("100022301", new DateOnly(2026, 12, 28), 1),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, start);

        var row = Assert.Single(result.Rows);
        Assert.True(row.HasFutureForecast);
        Assert.Equal("solicita hasta 21.12.2026", row.AutomaticComment);
    }

    [Fact]
    public void Analyze_comments_first_requested_period_per_article()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var start = new DateOnly(2026, 10, 5);
        var periods = Enumerable.Range(0, 13).Select(offset => start.AddDays(offset * 7)).ToArray();
        var forecast = new[]
        {
            new ForecastEntry("SKU-A", periods[9], 1),
            new ForecastEntry("SKU-B", periods[11], 1),
            new ForecastEntry("SKU-C", periods[12], 1),
        };

        var result = analyzer.Analyze(Array.Empty<InventoryPosition>(), forecast, periods, start);

        var rows = result.Rows.ToDictionary(row => row.Article, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("solicita hasta 07.12.2026", rows["SKU-A"].AutomaticComment);
        Assert.Equal("solicita hasta 21.12.2026", rows["SKU-B"].AutomaticComment);
        Assert.Equal("solicita hasta 28.12.2026", rows["SKU-C"].AutomaticComment);
    }

    [Fact]
    public void Analyze_shows_dash_when_zero_olo_and_weekly_demand_starts_after_five_weeks()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = Enumerable.Range(0, 8).Select(offset => StartWeek.AddDays(offset * 7)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("100022301", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 113),
        };
        var forecast = new[]
        {
            new ForecastEntry("100022301", periods[5], 1),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal(0, row.OloQuantity);
        Assert.True(row.HasFutureForecast);
        Assert.Null(row.CoveragePeriods);
        Assert.Equal("solicita hasta 05.10.2026", row.AutomaticComment);
    }

    [Fact]
    public void Analyze_keeps_zero_coverage_when_zero_olo_and_weekly_demand_starts_within_five_weeks()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = Enumerable.Range(0, 8).Select(offset => StartWeek.AddDays(offset * 7)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("100022301", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 113),
        };
        var forecast = new[]
        {
            new ForecastEntry("100022301", periods[4], 1),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal(0, row.OloQuantity);
        Assert.True(row.HasFutureForecast);
        Assert.Equal(0, row.CoveragePeriods);
        Assert.Equal("solicita hasta 28.09.2026", row.AutomaticComment);
    }

    [Fact]
    public void Analyze_keeps_zero_coverage_for_monthly_forecast()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = Enumerable.Range(0, 8).Select(offset => StartWeek.AddMonths(offset)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("100022301", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 113),
        };
        var forecast = new[]
        {
            new ForecastEntry("100022301", periods[5], 1),
        };

        var result = analyzer.Analyze(
            inventory,
            forecast,
            periods,
            StartWeek,
            granularity: ForecastGranularity.Monthly);

        var row = Assert.Single(result.Rows);
        Assert.Equal(0, row.OloQuantity);
        Assert.True(row.HasFutureForecast);
        Assert.Equal(0, row.CoveragePeriods);
    }

    [Fact]
    public void Analyze_coverage_uses_olo_inventory_not_servica()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[]
        {
            StartWeek,
            StartWeek.AddDays(7),
            StartWeek.AddDays(14),
        };
        var inventory = new[]
        {
            new InventoryPosition("100", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 25),
            new InventoryPosition("100", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 1000),
        };
        var forecast = new[]
        {
            new ForecastEntry("100", StartWeek, 10),
            new ForecastEntry("100", StartWeek.AddDays(7), 10),
            new ForecastEntry("100", StartWeek.AddDays(14), 10),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal(25, row.OloQuantity);
        Assert.Equal(1000, row.ServicaQuantity);
        Assert.Equal(2, row.CoveragePeriods);
    }

    [Fact]
    public void Analyze_does_not_count_guacima_zones_as_servica_inventory()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[] { StartWeek };
        var inventory = new[]
        {
            new InventoryPosition("100", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 10),
            new InventoryPosition("100", "ZA47-ZA47 - ZONA  ALMACENAJE 1 - GUACIMA", 1),
            new InventoryPosition("100", "ZA48-ZA48 - ZONA  ALMACENAJE 2 - GUACIMA", 2),
            new InventoryPosition("100", "ZA49-ZA49 - ZONA  ALMACENAJE 3 - GUACIMA", 3),
            new InventoryPosition("100", "ZA50-ZA50 - ZONA  ALMACENAJE 4 - GUACIMA", 4),
            new InventoryPosition("100", "ZA52-ZONA ALMACENAJE MUELLE RECEPCION - GUACIMA", 5),
            new InventoryPosition("100", "ZA55-ZA55 - ZONA  ALMACENAJE 5 - GUACIMA", 6),
            new InventoryPosition("100", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 7),
        };

        var result = analyzer.Analyze(inventory, Array.Empty<ForecastEntry>(), periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal(31, row.OloQuantity);
        Assert.Equal(7, row.ServicaQuantity);
    }

    [Fact]
    public void Analyze_sums_duplicate_forecast_rows_for_comparison_coverage()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[]
        {
            StartWeek,
            StartWeek.AddDays(7),
        };
        var inventory = new[]
        {
            new InventoryPosition("100", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 50),
        };
        var forecast = new[]
        {
            new ForecastEntry("100", StartWeek, 20),
            new ForecastEntry("100", StartWeek, 60),
            new ForecastEntry("100", StartWeek.AddDays(7), 20),
            new ForecastEntry("100", StartWeek.AddDays(7), 20),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal(120, row.ForecastQuantity);
        Assert.Equal(0, row.CoveragePeriods);
    }

    [Fact]
    public void Analyze_duplicate_rows_can_leave_article_without_first_week_coverage()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[]
        {
            StartWeek,
            StartWeek.AddDays(7),
        };
        var inventory = new[]
        {
            new InventoryPosition("100000860", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 90),
        };
        var forecast = new[]
        {
            new ForecastEntry("100000860", StartWeek, 26),
            new ForecastEntry("100000860", StartWeek, 49),
            new ForecastEntry("100000860", StartWeek, 59),
            new ForecastEntry("100000860", StartWeek, 38),
            new ForecastEntry("100000860", StartWeek, 18),
            new ForecastEntry("100000860", StartWeek, 32),
            new ForecastEntry("100000860", StartWeek.AddDays(7), 17),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal(90, row.OloQuantity);
        Assert.Equal(239, row.ForecastQuantity);
        Assert.Equal(0, row.CoveragePeriods);
        Assert.True(row.HasFutureForecast);
        Assert.Equal(string.Empty, row.AutomaticComment);
    }

    [Fact]
    public void Analyze_excludes_merma_and_configured_storage_zones()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[] { StartWeek };
        var inventory = new[]
        {
            new InventoryPosition("100", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 10),
            new InventoryPosition("100", "ZA30-ZONA ALMACENAJE DANADOS - CLIRO", 900),
            new InventoryPosition("100", "ZA38", 800),
            new InventoryPosition("100", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 700, PalletType: "Tipo pallet Merma"),
        };
        var forecast = new[] { new ForecastEntry("100", StartWeek, 10) };
        var ignoredZones = new[]
        {
            "ZA30-ZONA ALMACENAJE DAÑADOS - CLIRO",
            "ZA38-ZONA ALMACENAJE CONTROL INVENTARIO - CLIRO",
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek, ignoredZones);

        var row = Assert.Single(result.Rows);
        Assert.Equal(10, row.OloQuantity);
        Assert.Equal(0, row.ServicaQuantity);
        Assert.Equal(1, row.CoveragePeriods);
    }

    [Fact]
    public void Analyze_excludes_guacima_za55_when_in_default_inventory_excluded_zones()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[] { StartWeek };
        var inventory = new[]
        {
            new InventoryPosition("100", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 10),
            new InventoryPosition("100", "ZA55-ZA55   - ZONA ALMACENAJE 5 - GUACIMA", 900),
            new InventoryPosition("100", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 5),
        };
        var forecast = new[] { new ForecastEntry("100", StartWeek, 8) };

        var result = analyzer.Analyze(
            inventory,
            forecast,
            periods,
            StartWeek,
            InventoryExcludedZoneDefaults.Zones);

        var row = Assert.Single(result.Rows);
        Assert.Equal(10, row.OloQuantity);
        Assert.Equal(5, row.ServicaQuantity);
        Assert.Equal(15, row.TotalQuantity);
        Assert.Equal(1, row.CoveragePeriods);
    }

    [Fact]
    public void Analyze_forecast_without_inventory_appears_with_zero_coverage()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[]
        {
            StartWeek,
            StartWeek.AddDays(7),
        };
        var forecast = new[] { new ForecastEntry("200", StartWeek.AddDays(7), 12) };

        var result = analyzer.Analyze(Array.Empty<InventoryPosition>(), forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal("200", row.Article);
        Assert.Equal(0, row.OloQuantity);
        Assert.Equal(0, row.ServicaQuantity);
        Assert.Equal(12, row.ForecastQuantity);
        Assert.Equal(0, row.CoveragePeriods);
        Assert.True(row.HasFutureForecast);
    }

    [Fact]
    public void Analyze_inventory_without_future_forecast_appears_as_no_solicita()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[] { StartWeek };
        var inventory = new[]
        {
            new InventoryPosition("300", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 18, "Articulo inventario"),
        };

        var result = analyzer.Analyze(inventory, Array.Empty<ForecastEntry>(), periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal("300", row.Article);
        Assert.Equal(18, row.OloQuantity);
        Assert.Equal(0, row.ServicaQuantity);
        Assert.Null(row.CoveragePeriods);
        Assert.False(row.HasFutureForecast);
        Assert.Equal("no solicita", row.AutomaticComment);
    }

    [Fact]
    public void Analyze_zero_future_forecast_appears_as_no_solicita()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = Enumerable.Range(0, 4).Select(offset => StartWeek.AddDays(offset * 7)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("350", "ZA46-ZA45 - ZONA ALMACENAJE - SERVICA", 24, "Articulo sin solicitud"),
        };
        var forecast = periods.Select(period => new ForecastEntry("350", period, 0)).ToArray();

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal("350", row.Article);
        Assert.Null(row.CoveragePeriods);
        Assert.False(row.HasFutureForecast);
        Assert.Equal("no solicita", row.AutomaticComment);
    }

    [Fact]
    public void Analyze_counts_zero_demand_periods_when_article_has_future_forecast()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = Enumerable.Range(0, 5).Select(offset => StartWeek.AddDays(offset * 7)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("360", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 18),
        };
        var forecast = new[]
        {
            new ForecastEntry("360", periods[0], 0),
            new ForecastEntry("360", periods[1], 8),
            new ForecastEntry("360", periods[2], 0),
            new ForecastEntry("360", periods[3], 10),
            new ForecastEntry("360", periods[4], 0),
        };

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal(5, row.CoveragePeriods);
        Assert.True(row.HasFutureForecast);
        Assert.Equal(18, row.ForecastQuantity);
    }

    [Fact]
    public void Analyze_uses_reference_description_without_creating_description_only_rows()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = new[] { StartWeek };
        var forecast = new[] { new ForecastEntry("0002001", StartWeek, 8) };
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["2001"] = "Bomba manual",
            ["9999"] = "Articulo sin inventario ni forecast",
        };

        var result = analyzer.Analyze(
            Array.Empty<InventoryPosition>(),
            forecast,
            periods,
            StartWeek,
            referenceDescriptions: descriptions);

        var row = Assert.Single(result.Rows);
        Assert.Equal("0002001", row.Article);
        Assert.Equal("Bomba manual", row.Description);
    }

    [Fact]
    public void Analyze_uses_all_future_periods_not_only_the_classic_horizon()
    {
        var analyzer = new WarehouseComparisonAnalyzer();
        var periods = Enumerable.Range(0, 6).Select(offset => StartWeek.AddDays(offset * 7)).ToArray();
        var inventory = new[]
        {
            new InventoryPosition("400", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 60),
        };
        var forecast = periods.Select(period => new ForecastEntry("400", period, 10)).ToArray();

        var result = analyzer.Analyze(inventory, forecast, periods, StartWeek);

        var row = Assert.Single(result.Rows);
        Assert.Equal(6, result.PeriodCount);
        Assert.Equal(6, row.CoveragePeriods);
    }
}
