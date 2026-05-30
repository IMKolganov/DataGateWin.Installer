using System.Text.Json;

namespace DataGateWin.Installer;

internal static class InstallerReleaseAssetResolver
{
    public static string SelectZipDownloadUrl(
        JsonElement root,
        string assetNamePrefix,
        string assetNameSuffix)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("GitHub latest release response does not contain assets.");

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
                continue;

            var name = nameProp.GetString() ?? string.Empty;
            if (!name.StartsWith(assetNamePrefix, StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(assetNameSuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (asset.TryGetProperty("browser_download_url", out var urlProp) &&
                urlProp.ValueKind == JsonValueKind.String)
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }
        }

        throw new InvalidOperationException($"No ZIP asset found matching {assetNamePrefix}*{assetNameSuffix}.");
    }
}
