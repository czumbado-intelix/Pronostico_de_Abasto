namespace PronosticosAbasto.Core.Analysis;

/// <summary>
/// Result for a single expedition line: how much of its demand the principal
/// warehouse covers, how much to bring from satellite, and what stays pending.
/// </summary>
public sealed class ExpeditionLineResult
{
    public ExpeditionLineResult(
        string article,
        string description,
        string expeditionNumber,
        decimal demandQuantity,
        decimal principalInventoryQuantity,
        decimal satelliteInventoryQuantity,
        decimal transferSuggestionQuantity,
        decimal remainingShortageAfterTransfer,
        TransferRecommendationState transferRecommendationState,
        IReadOnlyList<ZoneInventoryDetail> satelliteZones,
        decimal pendingTransitQuantity = 0m,
        decimal newTransferSuggestionQuantity = 0m,
        IReadOnlyList<PalletInventoryDetail>? suggestedPallets = null,
        decimal calculatedTransferNeedQuantity = 0m,
        decimal palletRoundUpSurplusQuantity = 0m,
        IReadOnlyList<PalletInventoryDetail>? availablePallets = null)
    {
        Article = article;
        Description = description;
        ExpeditionNumber = expeditionNumber;
        DemandQuantity = demandQuantity;
        PrincipalInventoryQuantity = principalInventoryQuantity;
        SatelliteInventoryQuantity = satelliteInventoryQuantity;
        TransferSuggestionQuantity = transferSuggestionQuantity;
        RemainingShortageAfterTransfer = remainingShortageAfterTransfer;
        TransferRecommendationState = transferRecommendationState;
        SatelliteZones = satelliteZones;
        PendingTransitQuantity = pendingTransitQuantity;
        CalculatedTransferNeedQuantity = calculatedTransferNeedQuantity == 0m
            ? transferSuggestionQuantity
            : calculatedTransferNeedQuantity;
        PalletRoundUpSurplusQuantity = palletRoundUpSurplusQuantity;
        NewTransferSuggestionQuantity = newTransferSuggestionQuantity == 0m && pendingTransitQuantity <= 0m
            ? transferSuggestionQuantity
            : newTransferSuggestionQuantity;
        SuggestedPallets = suggestedPallets ?? Array.Empty<PalletInventoryDetail>();
        AvailablePallets = availablePallets ?? Array.Empty<PalletInventoryDetail>();
    }

    public string Article { get; }

    public string Description { get; }

    public string ExpeditionNumber { get; }

    public decimal DemandQuantity { get; }

    public decimal PrincipalInventoryQuantity { get; }

    public decimal SatelliteInventoryQuantity { get; }

    public decimal TransferSuggestionQuantity { get; }

    public decimal CalculatedTransferNeedQuantity { get; }

    public decimal PalletRoundUpSurplusQuantity { get; }

    public decimal RemainingShortageAfterTransfer { get; }

    public TransferRecommendationState TransferRecommendationState { get; }

    public IReadOnlyList<ZoneInventoryDetail> SatelliteZones { get; }

    public decimal PendingTransitQuantity { get; }

    public decimal NewTransferSuggestionQuantity { get; }

    public IReadOnlyList<PalletInventoryDetail> SuggestedPallets { get; }

    public IReadOnlyList<PalletInventoryDetail> AvailablePallets { get; }

    public int SuggestedPalletCount => SuggestedPallets.Count;
}

/// <summary>
/// All expedition lines plus the funnel counts (lines evaluated, those the
/// principal already covers, those to bring from satellite, and those with no
/// external stock at all).
/// </summary>
public sealed class ExpedicionesAnalysisResult
{
    public ExpedicionesAnalysisResult(IReadOnlyList<ExpeditionLineResult> lines)
    {
        Lines = lines;
        Evaluated = lines.Count(line => line.DemandQuantity > 0 || line.PendingTransitQuantity > 0);
        Covered = lines.Count(line =>
            line.DemandQuantity > 0 &&
            line.PendingTransitQuantity <= 0 &&
            line.PrincipalInventoryQuantity >= line.DemandQuantity);
        ToTransfer = lines.Count(line => line.TransferSuggestionQuantity > 0);
        WithoutExternal = lines.Count(line =>
            line.RemainingShortageAfterTransfer > 0 &&
            line.TransferSuggestionQuantity <= 0 &&
            line.SatelliteInventoryQuantity <= 0);
    }

    public IReadOnlyList<ExpeditionLineResult> Lines { get; }

    public int Evaluated { get; }

    public int Covered { get; }

    public int ToTransfer { get; }

    public int WithoutExternal { get; }
}
