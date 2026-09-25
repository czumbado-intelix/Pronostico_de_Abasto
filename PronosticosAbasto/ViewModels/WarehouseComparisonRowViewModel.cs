using CommunityToolkit.Mvvm.ComponentModel;
using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.ViewModels;

public sealed partial class WarehouseComparisonRowViewModel : ObservableObject, IClipboardRow
{
    private readonly Action<WarehouseComparisonRowViewModel> _commentChanged;
    private bool _suppressCommentChanged;

    public WarehouseComparisonRowViewModel(
        WarehouseComparisonRow source,
        string comment,
        string periodUnit,
        Action<WarehouseComparisonRowViewModel> commentChanged)
    {
        Source = source;
        Article = source.Article;
        Description = string.IsNullOrWhiteSpace(source.Description) ? "Sin descripcion" : source.Description.Trim();
        ArticleDescriptionText = ArticleDisplayFormatter.FormatWithDescription(Article, Description);
        OloQuantity = source.OloQuantity;
        ServicaQuantity = source.ServicaQuantity;
        ForecastQuantity = source.ForecastQuantity;
        CoveragePeriods = source.CoveragePeriods;
        CoverageText = FormatCoverage(source.CoveragePeriods, periodUnit);
        AutomaticComment = source.AutomaticComment;
        _commentChanged = commentChanged;

        _suppressCommentChanged = true;
        CommentText = string.IsNullOrWhiteSpace(comment) ? source.AutomaticComment : comment.Trim();
        _suppressCommentChanged = false;
    }

    public WarehouseComparisonRow Source { get; }

    public string Article { get; }

    public string Description { get; }

    public string ArticleDescriptionText { get; }

    public decimal OloQuantity { get; }

    public decimal ServicaQuantity { get; }

    public decimal ForecastQuantity { get; }

    public decimal? CoveragePeriods { get; }

    public string CoverageText { get; }

    public string AutomaticComment { get; }

    public string OloQuantityText => OloQuantity.ToString("N0");

    public string ServicaQuantityText => ServicaQuantity.ToString("N0");

    [ObservableProperty]
    public partial string CommentText { get; set; } = string.Empty;

    partial void OnCommentTextChanged(string value)
    {
        if (!_suppressCommentChanged)
        {
            _commentChanged(this);
        }
    }

    public string ToClipboardText() => string.Join(
        '\t',
        Article,
        Description,
        OloQuantityText,
        ServicaQuantityText,
        CoverageText,
        CommentText);

    private static string FormatCoverage(decimal? coverage, string periodUnit) =>
        coverage is decimal value ? $"{value:0.#} {periodUnit}" : "—";
}
