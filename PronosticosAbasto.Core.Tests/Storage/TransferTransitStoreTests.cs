using PronosticosAbasto.Core.Storage;

namespace PronosticosAbasto.Core.Tests.Storage;

public sealed class TransferTransitStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pa-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "transfer-transit.json");

    private static TransferTransitItem Item(
        string article,
        string pallet,
        decimal quantity = 10m,
        string company = "EPA",
        string periodKey = "2026-09-21",
        string zone = "ZA23") =>
        new(
            Id: string.Empty,
            Company: company,
            PeriodKey: periodKey,
            Article: article,
            Description: "Descripcion",
            Pallet: pallet,
            Quantity: quantity,
            StorageZone: zone,
            Location: "A-01",
            ValidationDate: new DateOnly(2026, 9, 1),
            TarimaSize: "Estandar",
            Origin: string.Empty,
            CreatedAt: new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Pending_pallets_survive_reopening_the_store()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending([Item("100002040", "P-1"), Item("100002040", "P-2")]);

        var reopened = new TransferTransitStore(FilePath);

        Assert.Equal(2, reopened.GetPending("EPA").Count);
        Assert.Equal(20m, reopened.GetPendingTransit("EPA")["100002040"]);
    }

    [Fact]
    public void AddPending_assigns_an_id_and_skips_the_same_pallet_twice()
    {
        var store = new TransferTransitStore(FilePath);

        var first = store.AddPending([Item("100002040", "P-1")]);
        var second = store.AddPending([Item("100002040", "P-1")]);

        Assert.Equal(1, first.Added);
        Assert.True(first.Saved);
        Assert.Equal(0, second.Added);
        Assert.Equal(1, second.Skipped);
        Assert.All(store.GetPending("EPA"), item => Assert.NotEmpty(item.Id));
    }

    [Fact]
    public void AddPending_skips_rows_without_article_pallet_or_quantity()
    {
        var store = new TransferTransitStore(FilePath);

        var result = store.AddPending(
        [
            Item("  ", "P-1"),
            Item("100002040", "  "),
            Item("100002041", "P-2", quantity: 0m),
        ]);

        Assert.Equal(0, result.Added);
        Assert.Equal(3, result.Skipped);
    }

    [Fact]
    public void Received_pallets_leave_the_pending_view_and_stop_counting_as_active_transit()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending([Item("100002040", "P-1"), Item("100002040", "P-2")]);
        var firstId = store.GetPending("EPA")[0].Id;

        Assert.Equal(1, store.MarkReceived([firstId]));

        var reopened = new TransferTransitStore(FilePath);
        Assert.Single(reopened.GetPending("EPA"));
        Assert.Equal(10m, reopened.GetPendingTransit("EPA")["100002040"]);
    }

    [Fact]
    public void Received_pallets_do_not_count_as_transit_or_block_reordering_same_pallet()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending([Item("100002040", "P-1"), Item("100002040", "P-2")]);
        var firstId = store.GetPending("EPA")[0].Id;

        Assert.Equal(1, store.MarkReceived([firstId]));

        var reopened = new TransferTransitStore(FilePath);
        var pendingTransit = reopened.GetPendingTransit("EPA");
        var unavailablePallets = reopened.GetOutstandingPalletKeys("EPA");

        Assert.Equal(10m, pendingTransit["100002040"]);
        Assert.DoesNotContain(
            PronosticosAbasto.Core.Analysis.PalletIdentity.BuildArticlePallet("100002040", "P-1"),
            unavailablePallets);
        Assert.Contains(
            PronosticosAbasto.Core.Analysis.PalletIdentity.BuildArticlePallet("100002040", "P-2"),
            unavailablePallets);

        var readd = reopened.AddPending([Item("100002040", "P-1")]);
        Assert.Equal(1, readd.Added);
        Assert.Equal(0, readd.Skipped);
    }

    [Fact]
    public void Status_views_keep_pending_received_and_reconciled_visible()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending([Item("100002040", "P-1"), Item("100002040", "P-2"), Item("100002040", "P-3")]);
        var pendingByPallet = store.GetPending("EPA").ToDictionary(item => item.Pallet, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(2, store.MarkReceived([pendingByPallet["P-2"].Id, pendingByPallet["P-3"].Id]));
        Assert.Equal(
            0,
            store.ReconcileReceived(
                "EPA",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    PronosticosAbasto.Core.Analysis.PalletIdentity.BuildArticlePallet("100002040", "P-3"),
                }));

        var reopened = new TransferTransitStore(FilePath);

        Assert.Equal(3, reopened.GetAll("EPA").Count);
        Assert.Equal("P-1", Assert.Single(reopened.GetPending("EPA")).Pallet);
        Assert.Equal(["P-2", "P-3"], reopened.GetReceivedPendingInventory("EPA").Select(item => item.Pallet).Order());
        Assert.Empty(reopened.GetReconciled("EPA"));
        Assert.Equal(10m, reopened.GetPendingTransit("EPA")["100002040"]);
    }

    [Fact]
    public void MarkArticleReceived_clears_every_pending_pallet_of_that_article()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending([Item("100002040", "P-1"), Item("100002040", "P-2"), Item("100002041", "P-3")]);

        Assert.Equal(2, store.MarkArticleReceived("EPA", "100002040"));

        var pending = new TransferTransitStore(FilePath).GetPending("EPA");
        Assert.Equal("100002041", Assert.Single(pending).Article);
    }

    [Fact]
    public void Pending_transit_is_isolated_per_company()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending([Item("100002040", "P-1"), Item("100002041", "P-2", company: "Cofersa")]);

        Assert.Equal("100002040", Assert.Single(store.GetPending("EPA")).Article);
        Assert.Equal("100002041", Assert.Single(store.GetPending("Cofersa")).Article);
    }

    [Fact]
    public void GetPendingTransit_counts_all_outstanding_periods_as_active_transit()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending(
        [
            Item("100002040", "P-1", periodKey: "2026-09-14"),
            Item("100002040", "P-2", periodKey: "2026-09-21"),
            Item("100002040", "P-3", periodKey: "2026-09-28"),
        ]);

        Assert.Equal(30m, store.GetPendingTransit("EPA", "2026-09-21")["100002040"]);
        Assert.Equal(30m, store.GetPendingTransit("EPA")["100002040"]);
        Assert.Equal(3, store.GetOutstandingPalletKeys("EPA", "2026-09-21").Count);
    }

    [Fact]
    public void Received_pallets_already_stop_counting_before_inventory_reconciliation()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending([Item("100002040", "P-1"), Item("100002040", "P-2")]);
        var firstId = store.GetPending("EPA")[0].Id;
        Assert.Equal(1, store.MarkReceived([firstId]));

        var reopened = new TransferTransitStore(FilePath);
        Assert.Equal(10m, reopened.GetPendingTransit("EPA")["100002040"]);
        Assert.Single(reopened.GetPending("EPA"));
    }

    [Fact]
    public void ReconcileReceived_keeps_received_rows_as_history_without_closing_them()
    {
        var store = new TransferTransitStore(FilePath);
        store.AddPending([Item("100002040", "P-1", zone: "ZA47")]);
        var firstId = store.GetPending("EPA")[0].Id;
        Assert.Equal(1, store.MarkReceived([firstId]));

        var principalPalletKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PronosticosAbasto.Core.Analysis.PalletIdentity.BuildArticlePallet("100002040", "P-1"),
        };

        Assert.Equal(0, store.ReconcileReceived("EPA", principalPalletKeys));
        Assert.Empty(new TransferTransitStore(FilePath).GetPendingTransit("EPA"));
        Assert.Single(new TransferTransitStore(FilePath).GetReceivedPendingInventory("EPA"));
        Assert.Empty(new TransferTransitStore(FilePath).GetReconciled("EPA"));
        Assert.DoesNotContain(
            PronosticosAbasto.Core.Analysis.PalletIdentity.BuildArticlePallet("100002040", "P-1"),
            new TransferTransitStore(FilePath).GetOutstandingPalletKeys("EPA"));
    }

    [Fact]
    public void A_corrupt_file_starts_empty_instead_of_throwing()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "[ { \"Article\": \"100002040\" truncado");

        Assert.Empty(new TransferTransitStore(FilePath).GetPending("EPA"));
    }

    [Fact]
    public void Legacy_pending_rows_without_id_are_loaded_as_pending()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            FilePath,
            """
            [
              {
                "Company": "EPA",
                "PeriodKey": "2026-09-21",
                "Article": "100002040",
                "Description": "Descripcion",
                "Pallet": "P-1",
                "Quantity": 10,
                "StorageZone": "ZA23",
                "Location": "A-01",
                "ValidationDate": "2026-09-01",
                "TarimaSize": "Estandar",
                "Origin": "",
                "CreatedAt": "2026-09-21T08:00:00+00:00"
              }
            ]
            """);

        var store = new TransferTransitStore(FilePath);
        var pending = Assert.Single(store.GetPending("EPA"));

        Assert.NotEmpty(pending.Id);
        Assert.Equal(10m, store.GetPendingTransit("EPA", "2026-09-21")["100002040"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
