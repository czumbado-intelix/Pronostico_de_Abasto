using PronosticosAbasto.Core.Analysis;
using PronosticosAbasto.Core.IO;

namespace PronosticosAbasto.Core.Tests.Analysis;

public class ExpedicionesAnalyzerTests
{
    private static readonly string[] SatelliteZones = ["ZA47"];

    private static InventoryPosition[] Inventory() =>
    [
        // Article A: 100 in principal, 50 in satellite.
        new InventoryPosition("A", "ZP", 100),
        new InventoryPosition("A", "ZA47", 50),
        // Article B: only satellite stock.
        new InventoryPosition("B", "ZA47", 20),
    ];

    [Fact]
    public void Principal_covering_demand_needs_no_transfer()
    {
        var result = new ExpedicionesAnalyzer().Analyze(
            [new ExpeditionLine("A", "A", 80)],
            Inventory(),
            SatelliteZones);

        var line = Assert.Single(result.Lines);
        Assert.Equal(100, line.PrincipalInventoryQuantity);
        Assert.Equal(0, line.TransferSuggestionQuantity);
        Assert.Equal(0, line.RemainingShortageAfterTransfer);
        Assert.Equal(TransferRecommendationState.NotRequired, line.TransferRecommendationState);
        Assert.Equal(1, result.Covered);
        Assert.Equal(0, result.ToTransfer);
    }

    [Fact]
    public void Shortage_with_enough_satellite_brings_the_full_pallet_and_leaves_nothing_pending()
    {
        var result = new ExpedicionesAnalyzer().Analyze(
            [new ExpeditionLine("A", "A", 130)],
            Inventory(),
            SatelliteZones);

        var line = Assert.Single(result.Lines);
        Assert.Equal(30, line.CalculatedTransferNeedQuantity);
        Assert.Equal(50, line.TransferSuggestionQuantity);
        Assert.Equal(20, line.PalletRoundUpSurplusQuantity);
        Assert.Equal(0, line.RemainingShortageAfterTransfer);
        Assert.Equal(50, line.SatelliteInventoryQuantity);
        Assert.Equal(TransferRecommendationState.Suggested, line.TransferRecommendationState);
        Assert.Equal(1, result.ToTransfer);
    }

    [Fact]
    public void Shortage_with_insufficient_satellite_leaves_a_pending_amount()
    {
        var result = new ExpedicionesAnalyzer().Analyze(
            [new ExpeditionLine("A", "A", 200)],
            Inventory(),
            SatelliteZones);

        var line = Assert.Single(result.Lines);
        Assert.Equal(50, line.TransferSuggestionQuantity);
        Assert.Equal(50, line.RemainingShortageAfterTransfer);
        Assert.Equal(TransferRecommendationState.Partial, line.TransferRecommendationState);
    }

    [Fact]
    public void Pending_transit_is_added_to_expedition_transfer_and_pallets_are_fifo()
    {
        var inventory = new[]
        {
            new InventoryPosition("A", "ZP", 100),
            new InventoryPosition("A", "ZA47", 30, "A", "PAL-2", new DateOnly(2026, 2, 1), "Disponible"),
            new InventoryPosition("A", "ZA47", 30, "A", "PAL-1", new DateOnly(2026, 1, 1), "Disponible"),
        };
        var pendingTransit = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 20,
        };

        var result = new ExpedicionesAnalyzer().Analyze(
            [new ExpeditionLine("A", "A", 120)],
            inventory,
            SatelliteZones,
            pendingTransit);

        var line = Assert.Single(result.Lines);
        Assert.Equal(20, line.PendingTransitQuantity);
        Assert.Equal(40, line.CalculatedTransferNeedQuantity);
        Assert.Equal(60, line.TransferSuggestionQuantity);
        Assert.Equal(40, line.NewTransferSuggestionQuantity);
        Assert.Equal(20, line.PalletRoundUpSurplusQuantity);
        Assert.Equal(0, line.RemainingShortageAfterTransfer);
        Assert.Collection(
            line.SuggestedPallets,
            pallet =>
            {
                Assert.Equal("PAL-1", pallet.Pallet);
                Assert.Equal(30, pallet.Quantity);
            },
            pallet =>
            {
                Assert.Equal("PAL-2", pallet.Pallet);
                Assert.Equal(30, pallet.Quantity);
            });
    }

    [Fact]
    public void Article_without_principal_brings_everything_from_satellite()
    {
        var result = new ExpedicionesAnalyzer().Analyze(
            [new ExpeditionLine("B", "B", 15)],
            Inventory(),
            SatelliteZones);

        var line = Assert.Single(result.Lines);
        Assert.Equal(0, line.PrincipalInventoryQuantity);
        Assert.Equal(15, line.CalculatedTransferNeedQuantity);
        Assert.Equal(20, line.TransferSuggestionQuantity);
        Assert.Equal(5, line.PalletRoundUpSurplusQuantity);
        Assert.Equal(0, line.RemainingShortageAfterTransfer);
    }

    [Fact]
    public void Article_with_no_inventory_at_all_has_no_external_source()
    {
        var result = new ExpedicionesAnalyzer().Analyze(
            [new ExpeditionLine("C", "C", 10)],
            Inventory(),
            SatelliteZones);

        var line = Assert.Single(result.Lines);
        Assert.Equal(0, line.PrincipalInventoryQuantity);
        Assert.Equal(0, line.SatelliteInventoryQuantity);
        Assert.Equal(10, line.RemainingShortageAfterTransfer);
        Assert.Equal(TransferRecommendationState.Unavailable, line.TransferRecommendationState);
        Assert.Equal(1, result.WithoutExternal);
    }

    [Fact]
    public void Covered_line_without_satellite_is_not_counted_as_without_external()
    {
        var result = new ExpedicionesAnalyzer().Analyze(
            [new ExpeditionLine("A", "A", 80)],
            [new InventoryPosition("A", "ZP", 100)],
            SatelliteZones);

        Assert.Equal(1, result.Covered);
        Assert.Equal(0, result.WithoutExternal);
    }

    [Fact]
    public void Duplicate_article_lines_are_totaled_before_comparing_with_principal_inventory()
    {
        var result = new ExpedicionesAnalyzer().Analyze(
            [
                new ExpeditionLine("100020936", "SPA TORONTO 190X190X70 CM BESTWAY", 1, "2800000239"),
                new ExpeditionLine("100020936", "SPA TORONTO 190X190X70 CM BESTWAY", 3, "2800000241"),
                new ExpeditionLine("100020936", "SPA TORONTO 190X190X70 CM BESTWAY", 2, "2800000242"),
                new ExpeditionLine("100020936", "SPA TORONTO 190X190X70 CM BESTWAY", 1, "2800000243"),
                new ExpeditionLine("100020936", "SPA TORONTO 190X190X70 CM BESTWAY", 5, "2800000244"),
            ],
            [
                new InventoryPosition("100020936", "ZP", 10),
                new InventoryPosition("100020936", "ZA47", 2, Pallet: "PAL-1", ValidationDate: new DateOnly(2026, 1, 1)),
                new InventoryPosition("100020936", "ZA47", 109, Pallet: "PAL-2", ValidationDate: new DateOnly(2026, 1, 2)),
            ],
            SatelliteZones);

        var line = Assert.Single(result.Lines);
        Assert.Equal("100020936", line.Article);
        Assert.Equal("2800000239; 2800000241; 2800000242; 2800000243; 2800000244", line.ExpeditionNumber);
        Assert.Equal(12, line.DemandQuantity);
        Assert.Equal(10, line.PrincipalInventoryQuantity);
        Assert.Equal(111, line.SatelliteInventoryQuantity);
        Assert.Equal(2, line.CalculatedTransferNeedQuantity);
        Assert.Equal(2, line.TransferSuggestionQuantity);
        Assert.Equal(0, line.RemainingShortageAfterTransfer);
        Assert.Equal(TransferRecommendationState.Suggested, line.TransferRecommendationState);
        Assert.Equal(1, result.Evaluated);
        Assert.Equal(0, result.Covered);
        Assert.Equal(1, result.ToTransfer);
    }

    [Fact]
    public void Expedition_number_is_kept_on_analysis_result()
    {
        var result = new ExpedicionesAnalyzer().Analyze(
            [new ExpeditionLine("B", "B", 15, "2000017658")],
            Inventory(),
            SatelliteZones);

        var line = Assert.Single(result.Lines);
        Assert.Equal("2000017658", line.ExpeditionNumber);
    }
}
