using PronosticosAbasto.Core.Storage;

namespace PronosticosAbasto.Core.Tests.Storage;

public sealed class TransferOrderStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pa-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "transfer-orders.json");

    [Fact]
    public void Ordered_marks_survive_reopening_the_store()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("EPA", "2026-09-21", "100002040", ordered: true, quantity: 120m);
        store.SetOrdered("EPA", "2026-09-21", "100002041", ordered: true, quantity: 40m);

        var reopened = new TransferOrderStore(FilePath);

        Assert.Equal(
            new[] { "100002040", "100002041" },
            reopened.GetOrdered("EPA", "2026-09-21").OrderBy(article => article));
        Assert.Equal(120m, reopened.GetOrderedQuantities("EPA", "2026-09-21")["100002040"]);
    }

    [Fact]
    public void Unmarking_removes_the_article()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("EPA", "2026-09-21", "100002040", ordered: true, quantity: 120m);
        store.SetOrdered("EPA", "2026-09-21", "100002040", ordered: false);

        Assert.Empty(new TransferOrderStore(FilePath).GetOrdered("EPA", "2026-09-21"));
    }

    [Fact]
    public void Marks_are_isolated_per_company_and_period()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("EPA", "2026-09-21", "100002040", ordered: true, quantity: 10m);
        store.SetOrdered("Cofersa", "2026-09-21", "100002041", ordered: true, quantity: 20m);
        store.SetOrdered("EPA", "2026-09-28", "100002042", ordered: true, quantity: 30m);

        Assert.Equal(new[] { "100002040" }, store.GetOrdered("EPA", "2026-09-21"));
        Assert.Equal(new[] { "100002041" }, store.GetOrdered("Cofersa", "2026-09-21"));
        Assert.Equal(new[] { "100002042" }, store.GetOrdered("EPA", "2026-09-28"));
    }

    [Fact]
    public void Company_lookup_ignores_case_and_surrounding_spaces()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("  EPA  ", "2026-09-21", " 100002040 ", ordered: true, quantity: 10m);

        Assert.Equal(new[] { "100002040" }, store.GetOrdered("epa", "2026-09-21"));
    }

    [Fact]
    public void ReplacePeriod_overwrites_the_whole_period_in_one_write()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("EPA", "2026-09-21", "viejo-1", ordered: true, quantity: 10m);
        store.SetOrdered("EPA", "2026-09-21", "viejo-2", ordered: true, quantity: 20m);

        store.ReplacePeriod("EPA", "2026-09-21", new Dictionary<string, decimal> { ["nuevo"] = 99m });

        var reopened = new TransferOrderStore(FilePath);
        Assert.Equal(new[] { "nuevo" }, reopened.GetOrdered("EPA", "2026-09-21"));
        Assert.Equal(99m, reopened.GetOrderedQuantities("EPA", "2026-09-21")["nuevo"]);
    }

    [Fact]
    public void ReplacePeriod_with_an_empty_map_clears_the_period()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("EPA", "2026-09-21", "100002040", ordered: true, quantity: 10m);

        store.ReplacePeriod("EPA", "2026-09-21", new Dictionary<string, decimal>());

        Assert.Empty(new TransferOrderStore(FilePath).GetOrdered("EPA", "2026-09-21"));
    }

    [Fact]
    public void ReplacePeriod_does_not_touch_other_periods()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("EPA", "2026-09-14", "anterior", ordered: true, quantity: 5m);
        store.SetOrdered("EPA", "2026-09-21", "actual", ordered: true, quantity: 10m);

        store.ReplacePeriod("EPA", "2026-09-21", new Dictionary<string, decimal>());

        Assert.Equal(new[] { "anterior" }, store.GetOrdered("EPA", "2026-09-14"));
    }

    [Fact]
    public void ClearPeriod_removes_only_that_period()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("EPA", "2026-09-14", "anterior", ordered: true, quantity: 5m);
        store.SetOrdered("EPA", "2026-09-21", "actual", ordered: true, quantity: 10m);

        store.ClearPeriod("EPA", "2026-09-21");

        var reopened = new TransferOrderStore(FilePath);
        Assert.Empty(reopened.GetOrdered("EPA", "2026-09-21"));
        Assert.Equal(new[] { "anterior" }, reopened.GetOrdered("EPA", "2026-09-14"));
    }

    [Fact]
    public void GetPendingTransit_only_sums_periods_before_the_current_one()
    {
        var store = new TransferOrderStore(FilePath);
        store.SetOrdered("EPA", "2026-09-07", "100002040", ordered: true, quantity: 10m);
        store.SetOrdered("EPA", "2026-09-14", "100002040", ordered: true, quantity: 5m);
        store.SetOrdered("EPA", "2026-09-21", "100002040", ordered: true, quantity: 100m);

        var pending = store.GetPendingTransit("EPA", "2026-09-21");

        Assert.Equal(15m, pending["100002040"]);
    }

    [Fact]
    public void A_corrupt_file_starts_empty_instead_of_throwing()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ \"EPA\": { \"2026-09-21\": { truncado");

        var store = new TransferOrderStore(FilePath);

        Assert.Empty(store.GetOrdered("EPA", "2026-09-21"));
    }

    [Fact]
    public void The_legacy_array_format_still_loads()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ \"EPA\": { \"2026-09-21\": [\"100002040\", \"100002041\"] } }");

        var store = new TransferOrderStore(FilePath);

        Assert.Equal(
            new[] { "100002040", "100002041" },
            store.GetOrdered("EPA", "2026-09-21").OrderBy(article => article));
        Assert.Equal(0m, store.GetOrderedQuantities("EPA", "2026-09-21")["100002040"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
