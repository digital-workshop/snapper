using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Snapper;

/// <summary>
/// Fragt bei GitHub nach der neuesten Version. Es werden keine Daten über dich gesendet,
/// nur eine normale Abfrage der öffentlichen Release-Seite. Höchstens alle 12 Stunden.
/// </summary>
internal static class UpdateChecker
{
    private const string Repo = "digital-workshop/snapper";
    private const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    public sealed record UpdateInfo(string Version, string Url);

    private sealed class Cache
    {
        public DateTime CheckedUtc { get; set; }
        public string? Version { get; set; }
        public string? Url { get; set; }
    }

    private static string CacheFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapper", "update.json");

    public static Version CurrentVersion => Normalize(Assembly.GetEntryAssembly()?.GetName().Version) ?? new Version(1, 0, 0);

    private static Version? Normalize(Version? v) =>
        v == null ? null : new Version(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    public static bool IsSafeUrl(string? url) =>
        url != null && url.StartsWith("https://github.com/" + Repo, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gibt die neue Version zurück, oder null (kein Update, kein Netz, Fehler).</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var cache = ReadCache();
            if (cache == null || DateTime.UtcNow - cache.CheckedUtc > Interval)
                cache = await FetchAsync();
            return Evaluate(cache?.Version, cache?.Url);
        }
        catch (Exception ex)
        {
            App.Log("Update-Prüfung: " + ex.Message);
            return null;
        }
    }

    /// <summary>Ist die Version aus dem Tag (z. B. "v1.2.0") neuer als die laufende?</summary>
    internal static UpdateInfo? Evaluate(string? tag, string? url, Version? current = null)
    {
        if (tag == null || !IsSafeUrl(url)) return null;
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed)) return null;
        var latest = Normalize(parsed);
        return latest != null && latest > (current ?? CurrentVersion) ? new UpdateInfo(latest.ToString(), url!) : null;
    }

    private static Cache? ReadCache()
    {
        try
        {
            return File.Exists(CacheFile) ? JsonSerializer.Deserialize<Cache>(File.ReadAllText(CacheFile)) : null;
        }
        catch { return null; }
    }

    private static async Task<Cache> FetchAsync()
    {
        var cache = new Cache { CheckedUtc = DateTime.UtcNow };
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Snapper-UpdateCheck");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var resp = await http.GetAsync(Api);
        if (resp.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            cache.Version = doc.RootElement.GetProperty("tag_name").GetString();
            cache.Url = doc.RootElement.GetProperty("html_url").GetString();
        }
        // 404 = noch kein Release. Auch das merken wir uns, damit wir nicht dauernd fragen.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(cache));
        }
        catch { }
        return cache;
    }
}
