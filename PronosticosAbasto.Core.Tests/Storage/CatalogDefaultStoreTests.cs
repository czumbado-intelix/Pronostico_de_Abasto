using PronosticosAbasto.Core.Storage;

namespace PronosticosAbasto.Core.Tests.Storage;

public sealed class CatalogDefaultStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pa-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Prepared_articles_use_packaged_defaults_when_local_catalog_is_missing()
    {
        var localPath = Path.Combine(_directory, "satellite-prepared-articles.json");
        var defaultPath = Path.Combine(_directory, "defaults", "satellite-prepared-articles.json");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        File.WriteAllText(
            defaultPath,
            """
            {
              "EPA": [
                { "Article": "100000001", "Description": "Articulo alistado" }
              ]
            }
            """);

        var store = new SatellitePreparedArticleStore(["EPA"], localPath, defaultPath);

        var item = Assert.Single(store.GetItemsFor("EPA"));
        Assert.Equal("100000001", item.Article);
        Assert.Equal("Articulo alistado", item.Description);
    }

    [Fact]
    public void Tarima_sizes_fill_only_companies_without_local_data()
    {
        var localPath = Path.Combine(_directory, "tarima-sizes.json");
        var defaultPath = Path.Combine(_directory, "defaults", "tarima-sizes.json");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        File.WriteAllText(
            localPath,
            """
            {
              "EPA": [
                { "Article": "200000001", "Description": "Local", "SizeCode": "S" }
              ],
              "Cofersa": []
            }
            """);
        File.WriteAllText(
            defaultPath,
            """
            {
              "EPA": [
                { "Article": "100000001", "Description": "Default EPA", "SizeCode": "D" }
              ],
              "Cofersa": [
                { "Article": "300000001", "Description": "Default Cofersa", "SizeCode": "T" }
              ]
            }
            """);

        var store = new TarimaSizeStore(["EPA", "Cofersa"], localPath, defaultPath);

        Assert.Equal("200000001", Assert.Single(store.GetItemsFor("EPA")).Article);
        Assert.Equal("300000001", Assert.Single(store.GetItemsFor("Cofersa")).Article);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
