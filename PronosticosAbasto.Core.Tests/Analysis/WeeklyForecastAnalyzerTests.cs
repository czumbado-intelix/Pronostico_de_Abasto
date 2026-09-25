using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.Core.Tests.Analysis;

public class WeeklyForecastAnalyzerTests
{
    private static readonly DateOnly SelectedWeek = new(2026, 6, 8);

    [Fact]
    public void Coverage_weeks_counts_forecast_weeks_covered_by_principal_stock()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        // Principal 75, demand 50/week over 3 weeks: week 1 covered (25 left),
        // week 2 only half covered so it doesn't count -> 1 whole week of coverage.
        var inventory = new[]
        {
            new InventoryPosition("A", "ZP-PRINCIPAL", 75),
            new InventoryPosition("B", "ZP-PRINCIPAL", 200),
            new InventoryPosition("C", "ZP-PRINCIPAL", 99),
        };
        var forecast = new[]
        {
            new ForecastEntry("A", SelectedWeek, 50),
            new ForecastEntry("A", new DateOnly(2026, 6, 15), 50),
            new ForecastEntry("A", new DateOnly(2026, 6, 22), 50),
            new ForecastEntry("B", SelectedWeek, 50),
            new ForecastEntry("B", new DateOnly(2026, 6, 15), 50),
        };
        var satelliteZones = new[] { "ZA31-SAT" };

        var result = analyzer.Analyze(inventory, forecast, SelectedWeek, satelliteZones);

        var articleA = result.Articles.Single(article => article.Article == "A");
        var articleB = result.Articles.Single(article => article.Article == "B");
        var articleC = result.Articles.Single(article => article.Article == "C");
        Assert.Equal(1m, articleA.CoverageWeeks);
        // B covers every forecast week available in the horizon.
        Assert.Equal(3m, articleB.CoverageWeeks);
        // C has stock but no demand: coverage is not applicable.
        Assert.Null(articleC.CoverageWeeks);
    }

    [Fact]
    public void Analyze_accumulates_selected_week_and_next_three_weeks_and_compares_principal_inventory()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[]
        {
            new InventoryPosition("A123", "ZA15-PRINCIPAL", 30),
            new InventoryPosition("A123", "ZA31-SAT-1", 8),
            new InventoryPosition("A123", "ZA47-SAT-2", 7),
        };
        var forecast = new[]
        {
            new ForecastEntry("A123", SelectedWeek, 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 15), 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 22), 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 29), 10),
        };
        var satelliteZones = new[] { "ZA31-SAT-1", "ZA47-SAT-2" };

        var result = analyzer.Analyze(inventory, forecast, SelectedWeek, satelliteZones);

        var article = Assert.Single(result.Articles);
        Assert.Equal(SelectedWeek, result.SelectedWeek);
        Assert.Equal(SelectedWeek, result.HorizonStartWeek);
        Assert.Equal(new DateOnly(2026, 6, 29), result.HorizonEndWeek);
        Assert.Equal(4, result.RequestedWeeks);
        Assert.Equal(4, result.AnalyzedWeeks);
        Assert.False(result.IsIncompleteHorizon);
        Assert.Equal("A123", article.Article);
        Assert.Equal(40, article.ForecastQuantity);
        Assert.Equal(30, article.PrincipalInventoryQuantity);
        Assert.Equal(45, article.TotalInventoryQuantity);
        Assert.Equal(15, article.SatelliteInventoryQuantity);
        Assert.Equal(-10, article.Difference);
        Assert.Equal(-25, article.SatelliteDifference);
        Assert.Equal(CoverageStatus.Warning, article.Status);
        // Satellite has 15 of 40 (>0 but not full) -> Warning under the "red only when empty" rule.
        Assert.Equal(CoverageStatus.Warning, article.SatelliteStatus);
        Assert.Equal(15, article.TransferSuggestionQuantity);
        Assert.Equal(5, article.PalletRoundUpSurplusQuantity);
        Assert.Equal(0, article.RemainingShortageAfterTransfer);
        Assert.Equal(TransferRecommendationState.Suggested, article.TransferRecommendationState);
        Assert.Equal(0, result.CriticalCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(0, result.HealthyCount);
        Assert.Equal(0, result.SatelliteCriticalCount);
        Assert.Equal(1, result.SatelliteWarningCount);
        Assert.Equal(0, result.SatelliteHealthyCount);
    }

    [Fact]
    public void Analyze_with_five_week_horizon_accumulates_selected_week_and_next_four_weeks()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[]
        {
            new InventoryPosition("A123", "ZA15-PRINCIPAL", 0),
            new InventoryPosition("A123", "ZA31-SAT-1", 60),
        };
        var forecast = new[]
        {
            new ForecastEntry("A123", SelectedWeek, 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 15), 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 22), 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 29), 10),
            new ForecastEntry("A123", new DateOnly(2026, 7, 6), 10),
            new ForecastEntry("A123", new DateOnly(2026, 7, 13), 10),
        };
        var satelliteZones = new[] { "ZA31-SAT-1" };

        var result = analyzer.Analyze(
            inventory,
            forecast,
            SelectedWeek,
            AnalysisHorizon.Weeks(5),
            CoverageThresholds.Default,
            satelliteZones);

        var article = Assert.Single(result.Articles);
        Assert.Equal(HorizonUnit.Weeks, result.Horizon.Unit);
        Assert.Equal(5, result.Horizon.Amount);
        Assert.Equal(5, result.RequestedWeeks);
        Assert.Equal(5, result.AnalyzedWeeks);
        Assert.Equal(new DateOnly(2026, 7, 6), result.HorizonEndWeek);
        Assert.Equal("5 semanas", result.HorizonDescription);
        Assert.Equal(50, article.ForecastQuantity);
        Assert.Equal(60, article.TransferSuggestionQuantity);
        Assert.Equal(10, article.PalletRoundUpSurplusQuantity);
    }

    [Fact]
    public void Analyze_uses_available_weeks_only_and_marks_incomplete_horizon()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[]
        {
            new InventoryPosition("B456", "ZA15-PRINCIPAL", 8),
            new InventoryPosition("B456", "ZA31-SAT-1", 5),
        };
        var forecast = new[]
        {
            new ForecastEntry("B456", new DateOnly(2026, 6, 15), 5),
            new ForecastEntry("B456", new DateOnly(2026, 6, 22), 5),
            new ForecastEntry("B456", new DateOnly(2026, 6, 29), 5),
        };
        var satelliteZones = new[] { "ZA31-SAT-1" };

        var result = analyzer.Analyze(inventory, forecast, new DateOnly(2026, 6, 15), satelliteZones);

        var article = Assert.Single(result.Articles);
        Assert.Equal(new DateOnly(2026, 6, 15), result.HorizonStartWeek);
        Assert.Equal(new DateOnly(2026, 6, 29), result.HorizonEndWeek);
        Assert.Equal(4, result.RequestedWeeks);
        Assert.Equal(3, result.AnalyzedWeeks);
        Assert.True(result.IsIncompleteHorizon);
        Assert.Equal(15, article.ForecastQuantity);
        Assert.Equal(8, article.PrincipalInventoryQuantity);
        Assert.Equal(13, article.TotalInventoryQuantity);
        Assert.Equal(5, article.TransferSuggestionQuantity);
        Assert.Equal(2, article.RemainingShortageAfterTransfer);
        Assert.Equal(TransferRecommendationState.Partial, article.TransferRecommendationState);
    }

    [Fact]
    public void Analyze_does_not_suggest_transfer_when_principal_inventory_covers_the_horizon()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[]
        {
            new InventoryPosition("C789", "ZA15-PRINCIPAL", 50),
            new InventoryPosition("C789", "ZA31-SAT-1", 12),
        };
        var forecast = new[]
        {
            new ForecastEntry("C789", SelectedWeek, 10),
            new ForecastEntry("C789", new DateOnly(2026, 6, 15), 10),
            new ForecastEntry("C789", new DateOnly(2026, 6, 22), 10),
            new ForecastEntry("C789", new DateOnly(2026, 6, 29), 10),
        };
        var satelliteZones = new[] { "ZA31-SAT-1" };

        var result = analyzer.Analyze(inventory, forecast, SelectedWeek, satelliteZones);

        var article = Assert.Single(result.Articles);
        Assert.Equal(CoverageStatus.Healthy, article.Status);
        Assert.Equal(0, article.TransferSuggestionQuantity);
        Assert.Equal(0, article.RemainingShortageAfterTransfer);
        Assert.Equal(TransferRecommendationState.NotRequired, article.TransferRecommendationState);
    }

    [Fact]
    public void Analyze_marks_transfer_as_unavailable_when_satellite_stock_is_missing()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[]
        {
            new InventoryPosition("D010", "ZA15-PRINCIPAL", 6),
        };
        var forecast = new[]
        {
            new ForecastEntry("D010", SelectedWeek, 5),
            new ForecastEntry("D010", new DateOnly(2026, 6, 15), 5),
            new ForecastEntry("D010", new DateOnly(2026, 6, 22), 5),
            new ForecastEntry("D010", new DateOnly(2026, 6, 29), 5),
        };

        var result = analyzer.Analyze(inventory, forecast, SelectedWeek, satelliteZones: new[] { "ZA31-SAT-1" });

        var article = Assert.Single(result.Articles);
        Assert.Equal(20, article.ForecastQuantity);
        Assert.Equal(6, article.PrincipalInventoryQuantity);
        Assert.Equal(0, article.SatelliteInventoryQuantity);
        Assert.Equal(0, article.TransferSuggestionQuantity);
        Assert.Equal(14, article.RemainingShortageAfterTransfer);
        Assert.Equal(TransferRecommendationState.Unavailable, article.TransferRecommendationState);
    }

    [Fact]
    public void Analyze_ignores_configured_storage_zones_before_principal_and_satellite_split()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[]
        {
            new InventoryPosition("A1", "ZA15-ZONA ALMACENAJE ORIGINALES 1 - CLIRO", 10),
            new InventoryPosition("A1", "ZA23-ZONA  ALMACENAJE DE MUELLES EXPEDICION - CLIRO", 100),
            new InventoryPosition("A1", "ZA47-ZONA ALMACENAJE 1 - GUACIMA", 50, "Articulo A", "PAL-1", new DateOnly(2026, 1, 1), "MULART"),
        };
        var forecast = new[]
        {
            new ForecastEntry("A1", SelectedWeek, 60),
        };

        var result = analyzer.Analyze(
            inventory,
            forecast,
            SelectedWeek,
            AnalysisHorizon.Weeks(4),
            CoverageThresholds.Default,
            satelliteZones: new[] { "ZA47-ZONA ALMACENAJE 1 - GUACIMA" },
            pendingTransitQuantities: null,
            ignoredStorageZones: new[] { "ZA23" });

        var article = Assert.Single(result.Articles);
        Assert.Equal(10, article.PrincipalInventoryQuantity);
        Assert.Equal(50, article.SatelliteInventoryQuantity);
        Assert.Equal(60, article.TotalInventoryQuantity);
        Assert.Equal(50, article.TransferSuggestionQuantity);
        Assert.DoesNotContain(article.Zones, zone => zone.StorageZone.Contains("ZA23", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_adds_prior_pending_transit_and_suggests_oldest_pallets_first()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[]
        {
            new InventoryPosition("M001", "ZP", 0),
            new InventoryPosition("M001", "ZA47", 100, "Manguera", "PAL-NEW", new DateOnly(2026, 5, 10), "Disponible"),
            new InventoryPosition("M001", "ZA47", 50, "Manguera", "PAL-OLD", new DateOnly(2026, 4, 10), "Disponible"),
        };
        var forecast = new[]
        {
            new ForecastEntry("M001", SelectedWeek, 30),
        };
        var pendingTransit = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["M001"] = 40,
        };

        var result = analyzer.Analyze(
            inventory,
            forecast,
            SelectedWeek,
            AnalysisHorizon.Weeks(4),
            CoverageThresholds.Default,
            new[] { "ZA47" },
            pendingTransit);

        var article = Assert.Single(result.Articles);
        Assert.Equal(40, article.PendingTransitQuantity);
        Assert.Equal(70, article.CalculatedTransferNeedQuantity);
        Assert.Equal(150, article.TransferSuggestionQuantity);
        Assert.Equal(110, article.NewTransferSuggestionQuantity);
        Assert.Equal(80, article.PalletRoundUpSurplusQuantity);
        Assert.Equal(0, article.RemainingShortageAfterTransfer);
        Assert.Collection(
            article.SuggestedPallets,
            pallet =>
            {
                Assert.Equal("PAL-OLD", pallet.Pallet);
                Assert.Equal(50, pallet.Quantity);
            },
            pallet =>
            {
                Assert.Equal("PAL-NEW", pallet.Pallet);
                Assert.Equal(100, pallet.Quantity);
            });
    }

    [Fact]
    public void Analyze_default_overload_uses_a_four_week_horizon()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[] { new InventoryPosition("A1", "ZA15-PRINCIPAL", 10) };
        var forecast = new[] { new ForecastEntry("A1", SelectedWeek, 5) };

        var result = analyzer.Analyze(inventory, forecast, SelectedWeek);

        Assert.Equal(HorizonUnit.Weeks, result.Horizon.Unit);
        Assert.Equal(4, result.Horizon.Amount);
        Assert.Equal(4, result.RequestedWeeks);
        Assert.Equal("4 semanas", result.HorizonDescription);
    }

    [Fact]
    public void Analyze_with_one_month_horizon_includes_every_week_within_the_rolling_month()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[] { new InventoryPosition("A123", "ZA15-PRINCIPAL", 100) };
        var forecast = new[]
        {
            new ForecastEntry("A123", SelectedWeek, 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 15), 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 22), 10),
            new ForecastEntry("A123", new DateOnly(2026, 6, 29), 10),
            new ForecastEntry("A123", new DateOnly(2026, 7, 6), 10),
            new ForecastEntry("A123", new DateOnly(2026, 7, 13), 10),
        };

        var result = analyzer.Analyze(inventory, forecast, SelectedWeek, AnalysisHorizon.Months(1));

        var article = Assert.Single(result.Articles);
        // From 08.06 a rolling month ends 08.07 (exclusive): 08, 15, 22, 29 jun + 06 jul.
        Assert.Equal(HorizonUnit.Months, result.Horizon.Unit);
        Assert.Equal("1 mes", result.HorizonDescription);
        Assert.Equal(5, result.AnalyzedWeeks);
        Assert.Equal(new DateOnly(2026, 7, 6), result.HorizonEndWeek);
        Assert.False(result.IsIncompleteHorizon);
        Assert.Equal(50, article.ForecastQuantity);
    }

    [Fact]
    public void Analyze_with_one_month_horizon_from_mid_month_crosses_into_the_next_month()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var startWeek = new DateOnly(2026, 6, 22);
        var inventory = new[] { new InventoryPosition("B1", "ZA15-PRINCIPAL", 100) };
        var forecast = new[]
        {
            new ForecastEntry("B1", new DateOnly(2026, 6, 22), 5),
            new ForecastEntry("B1", new DateOnly(2026, 6, 29), 5),
            new ForecastEntry("B1", new DateOnly(2026, 7, 6), 5),
            new ForecastEntry("B1", new DateOnly(2026, 7, 13), 5),
            new ForecastEntry("B1", new DateOnly(2026, 7, 20), 5),
            new ForecastEntry("B1", new DateOnly(2026, 7, 27), 5),
        };

        var result = analyzer.Analyze(inventory, forecast, startWeek, AnalysisHorizon.Months(1));

        var article = Assert.Single(result.Articles);
        // From 22.06 a rolling month ends 22.07 (exclusive): 22, 29 jun + 06, 13, 20 jul.
        Assert.Equal(5, result.AnalyzedWeeks);
        Assert.Equal(new DateOnly(2026, 7, 20), result.HorizonEndWeek);
        Assert.False(result.IsIncompleteHorizon);
        Assert.Equal(25, article.ForecastQuantity);
    }

    [Fact]
    public void Analyze_uses_configurable_red_and_yellow_thresholds()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var forecast = new[] { new ForecastEntry("A1", SelectedWeek, 10) };
        var satelliteZones = new[] { "ZA99-SAT" };
        var partial = new[] { new InventoryPosition("A1", "ZA15-PRINCIPAL", 8) }; // 80% coverage, has stock

        // Default (red 0%, yellow/green 100%): 80% has stock and < 100% -> Amarillo.
        var defaults = analyzer.Analyze(partial, forecast, SelectedWeek, AnalysisHorizon.Weeks(4), satelliteZones);
        Assert.Equal(CoverageStatus.Warning, Assert.Single(defaults.Articles).Status);

        // Green lowered to 80% -> 80% reaches it -> Verde.
        var green80 = analyzer.Analyze(
            partial, forecast, SelectedWeek, AnalysisHorizon.Weeks(4), new CoverageThresholds(0m, 80m), satelliteZones);
        Assert.Equal(CoverageStatus.Healthy, Assert.Single(green80.Articles).Status);

        // Red raised to 90% -> 80% is below it -> Rojo.
        var red90 = analyzer.Analyze(
            partial, forecast, SelectedWeek, AnalysisHorizon.Weeks(4), new CoverageThresholds(90m, 100m), satelliteZones);
        Assert.Equal(CoverageStatus.Critical, Assert.Single(red90.Articles).Status);

        // No stock at all -> Rojo regardless of thresholds.
        var empty = analyzer.Analyze(
            Array.Empty<InventoryPosition>(), forecast, SelectedWeek, AnalysisHorizon.Weeks(4), new CoverageThresholds(0m, 100m), satelliteZones);
        Assert.Equal(CoverageStatus.Critical, Assert.Single(empty.Articles).Status);
    }

    [Fact]
    public void Analyze_with_month_horizon_marks_incomplete_when_forecast_runs_short()
    {
        var analyzer = new WeeklyForecastAnalyzer();
        var inventory = new[] { new InventoryPosition("C1", "ZA15-PRINCIPAL", 100) };
        var forecast = new[]
        {
            new ForecastEntry("C1", SelectedWeek, 5),
            new ForecastEntry("C1", new DateOnly(2026, 6, 15), 5),
        };

        var result = analyzer.Analyze(inventory, forecast, SelectedWeek, AnalysisHorizon.Months(1));

        Assert.Equal(2, result.AnalyzedWeeks);
        Assert.True(result.IsIncompleteHorizon);
        Assert.Equal("1 mes", result.HorizonDescription);
    }
}
