namespace PronosticosAbasto.Core.Analysis;

public sealed class ZoneInventoryDetail
{
    public ZoneInventoryDetail(string storageZone, decimal quantity)
        : this(storageZone, quantity, isSatellite: false)
    {
    }

    public ZoneInventoryDetail(string storageZone, decimal quantity, bool isSatellite)
    {
        StorageZone = storageZone;
        Quantity = quantity;
        IsSatellite = isSatellite;
    }

    public string StorageZone { get; }

    public decimal Quantity { get; }

    public bool IsSatellite { get; }
}
