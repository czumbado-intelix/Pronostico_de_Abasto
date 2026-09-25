namespace PronosticosAbasto.Core.Analysis;

public sealed record TransferPlan(
    decimal CalculatedNeedQuantity,
    decimal TransferSuggestionQuantity,
    decimal NewTransferSuggestionQuantity,
    decimal PalletRoundUpSurplusQuantity,
    decimal RemainingShortageAfterTransfer,
    TransferRecommendationState RecommendationState,
    IReadOnlyList<PalletInventoryDetail> SuggestedPallets);

internal static class TransferPlanner
{
    public static TransferPlan Plan(
        decimal demandQuantity,
        decimal principalInventoryQuantity,
        decimal satelliteInventoryQuantity,
        decimal pendingTransitQuantity,
        IReadOnlyList<PalletInventoryDetail> satellitePallets)
    {
        pendingTransitQuantity = Math.Max(0m, pendingTransitQuantity);
        var principalShortage = Math.Max(0m, demandQuantity - principalInventoryQuantity);
        var totalNeed = principalShortage + pendingTransitQuantity;
        var exactTransferQuantity = Math.Min(totalNeed, satelliteInventoryQuantity);
        var suggestedPallets = SelectPallets(satellitePallets, exactTransferQuantity);
        var transferSuggestionQuantity = suggestedPallets.Sum(pallet => pallet.Quantity);
        var newTransferSuggestionQuantity = Math.Max(0m, transferSuggestionQuantity - pendingTransitQuantity);
        var palletRoundUpSurplusQuantity = Math.Max(0m, transferSuggestionQuantity - totalNeed);
        var remainingShortageAfterTransfer = Math.Max(0m, totalNeed - transferSuggestionQuantity);
        var state = ResolveTransferRecommendationState(totalNeed, satelliteInventoryQuantity, remainingShortageAfterTransfer);

        return new TransferPlan(
            totalNeed,
            transferSuggestionQuantity,
            newTransferSuggestionQuantity,
            palletRoundUpSurplusQuantity,
            remainingShortageAfterTransfer,
            state,
            suggestedPallets);
    }

    private static IReadOnlyList<PalletInventoryDetail> SelectPallets(
        IReadOnlyList<PalletInventoryDetail> satellitePallets,
        decimal requestedQuantity)
    {
        if (requestedQuantity <= 0m || satellitePallets.Count == 0)
        {
            return Array.Empty<PalletInventoryDetail>();
        }

        var remaining = requestedQuantity;
        var selected = new List<PalletInventoryDetail>();
        foreach (var pallet in satellitePallets
            .Where(pallet => pallet.Quantity > 0)
            .OrderBy(pallet => pallet.ValidationDate ?? DateOnly.MaxValue)
            .ThenBy(pallet => pallet.Pallet, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pallet => pallet.StorageZone, StringComparer.OrdinalIgnoreCase))
        {
            if (remaining <= 0m)
            {
                break;
            }

            selected.Add(pallet);
            remaining -= pallet.Quantity;
        }

        return selected;
    }

    private static TransferRecommendationState ResolveTransferRecommendationState(
        decimal totalNeed,
        decimal satelliteInventoryQuantity,
        decimal remainingShortageAfterTransfer)
    {
        if (totalNeed <= 0m)
        {
            return TransferRecommendationState.NotRequired;
        }

        if (satelliteInventoryQuantity <= 0m)
        {
            return TransferRecommendationState.Unavailable;
        }

        return remainingShortageAfterTransfer <= 0m
            ? TransferRecommendationState.Suggested
            : TransferRecommendationState.Partial;
    }
}
