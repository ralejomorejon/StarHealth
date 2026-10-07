using System.Reflection;
using System.Text.Json;

namespace StarHealth.App;

/// <summary>
/// Installed-vs-GitHub-release version compare. Tags look like v0.1.6-alpha
/// (stamped into release builds via -p:InformationalVersion); local dev builds
/// report the assembly version (1.0.0.0). UI-free so the headless harness covers it.
/// </summary>
internal static class AppVersion
{
    public static string Current =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "dev";

    public static (Version Num, string Suffix, bool Ok) Parse(string? tag)
    {
        var t = (tag ?? "").Trim();
        if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t[1..];
        string suffix = "";
        int dash = t.IndexOf('-');
        if (dash >= 0)
        {
            suffix = t[(dash + 1)..];
            t = t[..dash];
        }
        return Version.TryParse(t, out var v) && v is not null
            ? (v, suffix, true)
            : (new Version(0, 0), suffix, false);
    }

    public static bool IsNewer(string latestTag, string currentTag)
    {
        var (ln, ls, lok) = Parse(latestTag);
        var (cn, cs, cok) = Parse(currentTag);
        if (!lok) return false;
        if (!cok) return true;
        int cmp = ln.CompareTo(cn);
        if (cmp != 0) return cmp > 0;
        if (ls.Length == 0) return cs.Length != 0; // stable beats prerelease
        if (cs.Length == 0) return false;
        return string.Compare(ls, cs, StringComparison.Ordinal) > 0;
    }
}

/// <summary>
/// GitHub Releases feed parsing (JsonDocument: trim-safe, unlike Deserialize).
/// The releases list endpoint is newest-first and includes prereleases, which
/// matters because every build here is an alpha prerelease (/latest would 404).
/// </summary>
internal static class UpdateFeed
{
    public const string ReleasesUrl =
        "https://api.github.com/repos/ralejomorejon/StarHealth/releases?per_page=5";

    /// <summary>Newest published release carrying a Windows installer, plus its
    /// .sha256 sidecar when present. Null when nothing qualifies.</summary>
    public static (string Tag, string ExeUrl, string? HashUrl)? PickInstaller(JsonElement releases)
    {
        if (releases.ValueKind != JsonValueKind.Array) return null;
        foreach (var rel in releases.EnumerateArray())
        {
            if (rel.ValueKind != JsonValueKind.Object) continue;
            if (!rel.TryGetProperty("tag_name", out var tagEl)) continue;
            string? tag = tagEl.GetString();
            if (string.IsNullOrWhiteSpace(tag)) continue;
            if (!rel.TryGetProperty("assets", out var assets)
                || assets.ValueKind != JsonValueKind.Array) continue;
            string? exe = null, exeName = null;
            foreach (var a in assets.EnumerateArray())
            {
                if (!a.TryGetProperty("name", out var n)
                    || !a.TryGetProperty("browser_download_url", out var u)) continue;
                string? name = n.GetString(), url = u.GetString();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) continue;
                if (name.StartsWith("StarHealth-Setup-", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith("-win-x64.exe", StringComparison.OrdinalIgnoreCase))
                {
                    exe = url;
                    exeName = name;
                }
            }
            if (exe is null || exeName is null) continue;
            string? hash = null;
            foreach (var a in assets.EnumerateArray())
            {
                if (!a.TryGetProperty("name", out var n)
                    || !a.TryGetProperty("browser_download_url", out var u)) continue;
                if (string.Equals(n.GetString(), exeName + ".sha256", StringComparison.OrdinalIgnoreCase))
                    hash = u.GetString();
            }
            return (tag, exe, hash);
        }
        return null;
    }
}
