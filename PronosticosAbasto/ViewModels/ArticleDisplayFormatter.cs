namespace PronosticosAbasto.ViewModels;

internal static class ArticleDisplayFormatter
{
    public static string FormatWithDescription(string article, string? description)
    {
        var code = string.IsNullOrWhiteSpace(article) ? "Sin articulo" : article.Trim();
        var text = string.IsNullOrWhiteSpace(description) || description.Trim() == "\u2014"
            ? "Sin descripcion"
            : description.Trim();

        return $"{code} - {text}";
    }
}
