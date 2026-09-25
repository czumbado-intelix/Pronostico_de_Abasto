using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PronosticosAbasto.Core.Analysis;
using Color = Windows.UI.Color;

namespace PronosticosAbasto.ViewModels;

public sealed class ArticleResultRowViewModel : IClipboardRow
{
    public ArticleResultRowViewModel(
        string article,
        string description,
        decimal principalInventoryQuantity,
        decimal totalInventoryQuantity,
        decimal forecastQuantity,
        decimal difference,
        CoverageStatus status,
        decimal satelliteInventoryQuantity,
        decimal satelliteDifference,
        CoverageStatus satelliteStatus,
        decimal transferSuggestionQuantity,
        decimal remainingShortageAfterTransfer,
        TransferRecommendationState transferRecommendationState,
        IReadOnlyList<ZoneDetailRowViewModel> zones)
    {
        Article = article;
        Description = string.IsNullOrWhiteSpace(description) ? "Sin descripcion" : description.Trim();
        ArticleDescriptionText = ArticleDisplayFormatter.FormatWithDescription(Article, Description);
        InventoryQuantity = principalInventoryQuantity;
        PrincipalInventoryQuantity = principalInventoryQuantity;
        TotalInventoryQuantity = totalInventoryQuantity;
        ForecastQuantity = forecastQuantity;
        Difference = difference;
        Status = status;
        SatelliteInventoryQuantity = satelliteInventoryQuantity;
        SatelliteDifference = satelliteDifference;
        SatelliteStatus = satelliteStatus;
        TransferSuggestionQuantity = transferSuggestionQuantity;
        RemainingShortageAfterTransfer = remainingShortageAfterTransfer;
        TransferRecommendationState = transferRecommendationState;
        Zones = zones;
        ZoneCount = zones.Count;
        SatelliteZoneCount = zones.Count(zone => zone.IsSatellite);
        StatusLabel = ToStatusLabel(status);
        SatelliteStatusLabel = ToStatusLabel(satelliteStatus);
        TransferStateLabel = ToTransferStateLabel(transferRecommendationState);
        TransferInstructionText = BuildTransferInstructionText(
            transferRecommendationState,
            transferSuggestionQuantity,
            remainingShortageAfterTransfer);
        TransferInstructionVisibility = string.IsNullOrWhiteSpace(TransferInstructionText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var highlightPalette = CreatePalette(status);
        StatusAccentBrush = new SolidColorBrush(highlightPalette.AccentColor);

        var principalPalette = CreatePalette(status);
        GeneralPanelBrush = new SolidColorBrush(principalPalette.PanelColor);
        GeneralBorderBrush = new SolidColorBrush(principalPalette.BorderColor);
        GeneralAccentBrush = new SolidColorBrush(principalPalette.AccentColor);
        GeneralPillBrush = new SolidColorBrush(principalPalette.PillColor);

        var satellitePalette = CreatePalette(satelliteStatus);
        SatellitePanelBrush = new SolidColorBrush(satellitePalette.PanelColor);
        SatelliteBorderBrush = new SolidColorBrush(satellitePalette.BorderColor);
        SatelliteAccentBrush = new SolidColorBrush(satellitePalette.AccentColor);
        SatellitePillBrush = new SolidColorBrush(satellitePalette.PillColor);

        TransferAccentBrush = new SolidColorBrush(CreateTransferAccentColor(transferRecommendationState));
    }

    public string Article { get; }

    public string Description { get; }

    public string ArticleDescriptionText { get; }

    public decimal InventoryQuantity { get; }

    public decimal PrincipalInventoryQuantity { get; }

    public decimal TotalInventoryQuantity { get; }

    public decimal ForecastQuantity { get; }

    public decimal Difference { get; }

    public CoverageStatus Status { get; }

    public decimal SatelliteInventoryQuantity { get; }

    public decimal SatelliteDifference { get; }

    public CoverageStatus SatelliteStatus { get; }

    public decimal TransferSuggestionQuantity { get; }

    public decimal RemainingShortageAfterTransfer { get; }

    public TransferRecommendationState TransferRecommendationState { get; }

    public IReadOnlyList<ZoneDetailRowViewModel> Zones { get; }

    public int ZoneCount { get; }

    public int SatelliteZoneCount { get; }

    public Brush StatusAccentBrush { get; }

    public Brush GeneralPanelBrush { get; }

    public Brush GeneralBorderBrush { get; }

    public Brush GeneralAccentBrush { get; }

    public Brush GeneralPillBrush { get; }

    public Brush SatellitePanelBrush { get; }

    public Brush SatelliteBorderBrush { get; }

    public Brush SatelliteAccentBrush { get; }

    public Brush SatellitePillBrush { get; }

    public Brush TransferAccentBrush { get; }

    public string StatusLabel { get; }

    public string SatelliteStatusLabel { get; }

    public string TransferStateLabel { get; }

    public string TransferInstructionText { get; }

    public Visibility TransferInstructionVisibility { get; }

    public string InventoryText => PrincipalInventoryQuantity.ToString("N0");

    public string PrincipalInventoryText => PrincipalInventoryQuantity.ToString("N0");

    public string TotalInventoryText => TotalInventoryQuantity.ToString("N0");

    public string ForecastText => ForecastQuantity.ToString("N0");

    public string DifferenceText => Difference.ToString("N0");

    public string SatelliteInventoryText => SatelliteInventoryQuantity.ToString("N0");

    public string SatelliteDifferenceText => SatelliteDifference.ToString("N0");

    public string TransferSuggestionText => TransferSuggestionQuantity.ToString("N0");

    public string RemainingShortageText => RemainingShortageAfterTransfer.ToString("N0");

    public string ZoneCountText => $"{ZoneCount} zonas";

    public string SatelliteZoneCountText => $"{SatelliteZoneCount} satelitales";

    public string ZoneSummaryText => $"{ZoneCount} zonas | {SatelliteZoneCount} satelitales";

    public string ToClipboardText() => string.Join(
        "\t",
        Article,
        Description,
        ForecastText,
        PrincipalInventoryText,
        DifferenceText,
        StatusLabel,
        SatelliteInventoryText);

    private static string ToStatusLabel(CoverageStatus status) =>
        status switch
        {
            CoverageStatus.Critical => "Rojo",
            CoverageStatus.Warning => "Amarillo",
            _ => "Verde",
        };

    private static string ToTransferStateLabel(TransferRecommendationState state) =>
        state switch
        {
            TransferRecommendationState.Suggested => "Sugerido",
            TransferRecommendationState.Partial => "Parcial",
            TransferRecommendationState.Unavailable => "Sin satelital",
            _ => "No requerido",
        };

    private static string BuildTransferInstructionText(
        TransferRecommendationState state,
        decimal transferSuggestionQuantity,
        decimal remainingShortageAfterTransfer) =>
        state switch
        {
            TransferRecommendationState.Suggested =>
                $"Mandar a traer {transferSuggestionQuantity:N0} desde satelital",
            TransferRecommendationState.Partial =>
                $"Mandar a traer {transferSuggestionQuantity:N0} | Pendiente {remainingShortageAfterTransfer:N0}",
            TransferRecommendationState.Unavailable =>
                $"Sin satelital | Pendiente {remainingShortageAfterTransfer:N0}",
            _ => string.Empty,
        };

    private static Color CreateTransferAccentColor(TransferRecommendationState state) =>
        state switch
        {
            TransferRecommendationState.Suggested => ColorHelper.FromArgb(255, 34, 197, 94),
            TransferRecommendationState.Partial => ColorHelper.FromArgb(255, 245, 158, 11),
            TransferRecommendationState.Unavailable => ColorHelper.FromArgb(255, 239, 68, 68),
            _ => ColorHelper.FromArgb(255, 148, 163, 184),
        };

    private static StatusPalette CreatePalette(CoverageStatus status) =>
        status switch
        {
            CoverageStatus.Critical => new StatusPalette(
                PanelColor: ColorHelper.FromArgb(255, 25, 16, 24),
                BorderColor: ColorHelper.FromArgb(96, 239, 68, 68),
                AccentColor: ColorHelper.FromArgb(255, 239, 68, 68),
                PillColor: ColorHelper.FromArgb(40, 239, 68, 68)),
            CoverageStatus.Warning => new StatusPalette(
                PanelColor: ColorHelper.FromArgb(255, 31, 24, 11),
                BorderColor: ColorHelper.FromArgb(96, 245, 158, 11),
                AccentColor: ColorHelper.FromArgb(255, 245, 158, 11),
                PillColor: ColorHelper.FromArgb(40, 245, 158, 11)),
            _ => new StatusPalette(
                PanelColor: ColorHelper.FromArgb(255, 11, 27, 24),
                BorderColor: ColorHelper.FromArgb(96, 16, 185, 129),
                AccentColor: ColorHelper.FromArgb(255, 16, 185, 129),
                PillColor: ColorHelper.FromArgb(40, 16, 185, 129)),
        };

    private readonly record struct StatusPalette(
        Color PanelColor,
        Color BorderColor,
        Color AccentColor,
        Color PillColor);
}
