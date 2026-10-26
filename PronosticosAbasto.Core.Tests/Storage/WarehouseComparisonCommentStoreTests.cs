using PronosticosAbasto.Core.Storage;

namespace PronosticosAbasto.Core.Tests.Storage;

public sealed class WarehouseComparisonCommentStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pa-comment-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "comments.json");

    [Fact]
    public void ClearCommentsFor_removes_only_the_selected_company_comments()
    {
        var store = new WarehouseComparisonCommentStore(["EPA", "Cofersa"], FilePath);
        store.SaveComment("EPA", "100002822", "revisar");
        store.SaveComment("Cofersa", "100002822", "mantener");

        store.ClearCommentsFor("EPA");

        Assert.Empty(store.GetCommentsFor("EPA"));
        Assert.Equal("mantener", store.GetComment("Cofersa", "100002822"));
    }

    [Fact]
    public void ClearCommentsFor_persists_the_removed_comments()
    {
        var store = new WarehouseComparisonCommentStore(["EPA"], FilePath);
        store.SaveComment("EPA", "100002822", "revisar");

        store.ClearCommentsFor("EPA");
        var reloaded = new WarehouseComparisonCommentStore(["EPA"], FilePath);

        Assert.Empty(reloaded.GetCommentsFor("EPA"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
