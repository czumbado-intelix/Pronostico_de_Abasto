using PronosticosAbasto.Core.Configuration;

namespace PronosticosAbasto.Core.Tests.Configuration;

public class SatelliteZoneSettingsTests
{
    [Fact]
    public void Create_seeds_each_company_with_default_zones()
    {
        var settings = SatelliteZoneSettings.Create(
            new[] { "EPA", "Cofersa" },
            new[] { "ZONA-1", "ZONA-2" });

        Assert.Equal(new[] { "ZONA-1", "ZONA-2" }, settings.GetZonesFor("EPA"));
        Assert.Equal(new[] { "ZONA-1", "ZONA-2" }, settings.GetZonesFor("Cofersa"));
    }

    [Fact]
    public void Create_applies_overrides_per_company_without_touching_others()
    {
        var settings = SatelliteZoneSettings.Create(
            new[] { "EPA", "Cofersa" },
            new[] { "DEFAULT" },
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Cofersa"] = new[] { "COF-1", "COF-2" },
            });

        Assert.Equal(new[] { "DEFAULT" }, settings.GetZonesFor("EPA"));
        Assert.Equal(new[] { "COF-1", "COF-2" }, settings.GetZonesFor("Cofersa"));
    }

    [Fact]
    public void GetZonesFor_is_case_insensitive_and_returns_empty_for_unknown_company()
    {
        var settings = SatelliteZoneSettings.Create(new[] { "EPA" }, new[] { "ZONA" });

        Assert.Equal(new[] { "ZONA" }, settings.GetZonesFor("epa"));
        Assert.Empty(settings.GetZonesFor("Desconocida"));
    }

    [Fact]
    public void WithZonesFor_trims_dedupes_and_drops_blanks_per_company()
    {
        var settings = SatelliteZoneSettings
            .Create(new[] { "EPA", "Cofersa" }, new[] { "DEFAULT" })
            .WithZonesFor("Cofersa", new[] { "  COF-1 ", "cof-1", "", "COF-2", "   " });

        Assert.Equal(new[] { "COF-1", "COF-2" }, settings.GetZonesFor("Cofersa"));
        Assert.Equal(new[] { "DEFAULT" }, settings.GetZonesFor("EPA"));
    }

    [Fact]
    public void ToDictionary_round_trips_through_create_overrides()
    {
        var original = SatelliteZoneSettings
            .Create(new[] { "EPA", "Cofersa" }, new[] { "DEFAULT" })
            .WithZonesFor("EPA", new[] { "EPA-1" });

        var rebuilt = SatelliteZoneSettings.Create(
            new[] { "EPA", "Cofersa" },
            new[] { "DEFAULT" },
            original.ToDictionary());

        Assert.Equal(new[] { "EPA-1" }, rebuilt.GetZonesFor("EPA"));
        Assert.Equal(new[] { "DEFAULT" }, rebuilt.GetZonesFor("Cofersa"));
    }

    [Fact]
    public void InventoryExcludedZoneDefaults_include_guacima_za55()
    {
        Assert.Contains(
            InventoryExcludedZoneDefaults.Zones,
            zone => zone.StartsWith("ZA55", StringComparison.OrdinalIgnoreCase));
    }
}
