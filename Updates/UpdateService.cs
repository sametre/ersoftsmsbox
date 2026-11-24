using System.Net;
using System.Text.Json;

namespace ERSoftSMSBox.Updates;

public sealed record VersionInfo(string Version, string ReleaseDate, string ReleaseNotes, long FileSize);

public sealed class UpdateService : IDisposable
{
    private readonly HttpClient http;
    private readonly string baseUrl;

    public UpdateService() : this(AppVersion.UpdateServerBase) { }

    public UpdateService(string baseUrl)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<VersionInfo?> CheckForUpdateAsync(CancellationToken cancellation)
    {
        using HttpResponseMessage response = await http.GetAsync($"{baseUrl}/version", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellation);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (root.TryGetProperty("error", out _)) return null;
        return new VersionInfo(
            root.GetProperty("version").GetString() ?? "",
            root.TryGetProperty("releaseDate", out JsonElement rd) ? rd.GetString() ?? "" : "",
            root.TryGetProperty("releaseNotes", out JsonElement rn) ? rn.GetString() ?? "" : "",
            root.TryGetProperty("fileSize", out JsonElement fs) ? fs.GetInt64() : 0);
    }

    public async Task DownloadUpdateAsync(string destinationPath, IProgress<int>? progress, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await http.GetAsync(
            $"{baseUrl}/download/latest", HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? -1;
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellation);
        await using FileStream file = File.Create(destinationPath);
        byte[] buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellation)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellation);
            downloaded += read;
            if (total > 0) progress?.Report((int)(downloaded * 100 / total));
        }
    }

    public void Dispose() => http.Dispose();
}
