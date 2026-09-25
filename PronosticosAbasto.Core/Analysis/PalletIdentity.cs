using System.Text;

namespace PronosticosAbasto.Core.Analysis;

public static class PalletIdentity
{
    public static string Build(string article, PalletInventoryDetail pallet) =>
        Build(article, pallet.Pallet, pallet.StorageZone, pallet.Location);

    public static string Build(string article, string pallet, string storageZone, string location) =>
        string.Join(
            "|",
            Normalize(article),
            Normalize(pallet),
            Normalize(storageZone),
            Normalize(location));

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }
}
