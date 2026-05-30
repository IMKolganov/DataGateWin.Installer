using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace DataGateWin.Installer;

internal static class InstallerDownloader
{
    public static HttpClient CreateGitHubHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DataGateWin.Installer");
        return http;
    }

    public static async Task DownloadFileAsync(
        HttpClient http,
        string url,
        string destinationPath,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destinationPath);

        var buffer = new byte[81920];
        long readTotal = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            readTotal += read;

            if (total.HasValue && total.Value > 0)
            {
                var pct = (double)readTotal / total.Value * 100.0;
                progress.Report(Math.Min(100.0, pct));
            }
        }

        if (!total.HasValue)
            progress.Report(100.0);
    }

    public static async Task<string> ResolveLatestReleaseZipUrlAsync(
        HttpClient http,
        string latestReleaseApiUrl,
        string assetNamePrefix,
        string assetNameSuffix,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(latestReleaseApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return InstallerReleaseAssetResolver.SelectZipDownloadUrl(
            doc.RootElement,
            assetNamePrefix,
            assetNameSuffix);
    }
}
