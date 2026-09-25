namespace PronosticosAbasto.Core.Analysis;

public sealed class ArticleAnalysisResult
{
    public ArticleAnalysisResult(
        string article,
        decimal inventoryQuantity,
        decimal forecastQuantity,
        decimal difference,
        CoverageStatus status,
        IReadOnlyList<ZoneInventoryDetail> zones)
        : this(
            article,
            inventoryQuantity,
            inventoryQuantity,
            forecastQuantity,
            difference,
            status,
            zones,
            inventoryQuantity,
            difference,
            status,
            transferSuggestionQuantity: 0m,
            remainingShortageAfterTransfer: 0m,
            transferRecommendationState: TransferRecommendationState.NotRequired,
            zones)
    {
    }

    public ArticleAnalysisResult(
        string article,
        decimal principalInventoryQuantity,
        decimal forecastQuantity,
        decimal difference,
        CoverageStatus status,
        IReadOnlyList<ZoneInventoryDetail> zones,
        decimal satelliteInventoryQuantity,
        decimal satelliteDifference,
        CoverageStatus satelliteStatus,
        IReadOnlyList<ZoneInventoryDetail> satelliteZones)
        : this(
            article,
            principalInventoryQuantity,
            totalInventoryQuantity: principalInventoryQuantity,
            forecastQuantity,
            difference,
            status,
            zones,
            satelliteInventoryQuantity,
            satelliteDifference,
            satelliteStatus,
            transferSuggestionQuantity: 0m,
            remainingShortageAfterTransfer: Math.Max(0m, forecastQuantity - principalInventoryQuantity),
            transferRecommendationState: TransferRecommendationState.NotRequired,
            satelliteZones)
    {
    }

    public ArticleAnalysisResult(
        string article,
        decimal principalInventoryQuantity,
        decimal totalInventoryQuantity,
        decimal forecastQuantity,
        decimal difference,
        CoverageStatus status,
        IReadOnlyList<ZoneInventoryDetail> zones,
        decimal satelliteInventoryQuantity,
        decimal satelliteDifference,
        CoverageStatus satelliteStatus,
        decimal transferSuggestionQuantity,
        decimal remainingShortageAfterTransfer,
        TransferRecommendationState transferRecommendationState,
        IReadOnlyList<ZoneInventoryDetail> satelliteZones)
    {
        Article = article;
        InventoryQuantity = principalInventoryQuantity;
        PrincipalInventoryQuantity = principalInventoryQuantity;
        TotalInventoryQuantity = totalInventoryQuantity;
        ForecastQuantity = forecastQuantity;
        Difference = difference;
        Status = status;
        Zones = zones;
        SatelliteInventoryQuantity = satelliteInventoryQuantity;
        SatelliteDifference = satelliteDifference;
        SatelliteStatus = satelliteStatus;
        TransferSuggestionQuantity = transferSuggestionQuantity;
        CalculatedTransferNeedQuantity = transferSuggestionQuantity;
        NewTransferSuggestionQuantity = transferSuggestionQuantity;
        RemainingShortageAfterTransfer = remainingShortageAfterTransfer;
        TransferRecommendationState = transferRecommendationState;
        SatelliteZones = satelliteZones;
    }

    public string Article { get; }

    public decimal InventoryQuantity { get; }

    public decimal PrincipalInventoryQuantity { get; }

    public decimal TotalInventoryQuantity { get; }

    public decimal ForecastQuantity { get; }

    public decimal Difference { get; }

    public CoverageStatus Status { get; }

    public IReadOnlyList<ZoneInventoryDetail> Zones { get; }

    public decimal SatelliteInventoryQuantity { get; }

    public decimal SatelliteDifference { get; }

    public CoverageStatus SatelliteStatus { get; }

    public decimal TransferSuggestionQuantity { get; }

    public decimal CalculatedTransferNeedQuantity { get; init; }

    public decimal PalletRoundUpSurplusQuantity { get; init; }

    public decimal RemainingShortageAfterTransfer { get; }

    public TransferRecommendationState TransferRecommendationState { get; }

    public IReadOnlyList<ZoneInventoryDetail> SatelliteZones { get; }

    public decimal PendingTransitQuantity { get; init; }

    public decimal NewTransferSuggestionQuantity { get; init; }

    public IReadOnlyList<PalletInventoryDetail> SuggestedPallets { get; init; } =
        Array.Empty<PalletInventoryDetail>();

    public IReadOnlyList<PalletInventoryDetail> AvailablePallets { get; init; } =
        Array.Empty<PalletInventoryDetail>();

    public int SuggestedPalletCount => SuggestedPallets.Count;

    /// <summary>
    /// How many forecast periods (weeks or months, per the file's granularity)
    /// of demand the PRINCIPAL stock covers within the analyzed horizon.
    /// Null when the article has no demand in the horizon.
    /// </summary>
    public decimal? CoverageWeeks { get; init; }
}
