using System.IO;

namespace StarHealth.App;

/// <summary>
/// Package-identity-safe local storage. The Inno installer ships an UNPACKAGED
/// build (no MSIX identity), where Windows.Storage.ApplicationData throws
/// APPMODEL_ERROR_NO_PACKAGE (0x80073D54) — which killed the app on launch
/// with zero UI. Every accessor here falls back to
/// %LOCALAPPDATA%\StarHealth so installed (unpackaged), dev (dotnet run
/// registers a debug identity), and future MSIX builds share one code path.
/// Nothing in here ever throws: this sits on the startup path.
/// </summary>
internal static class LocalData
{
    private static string? _fallbackDir;

    private static string FallbackDir =>
        _fallbackDir ??= Directory.CreateDirectory(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarHealth")).FullName;

    /// <summary>Folder for history.db. LocalFolder when packaged, %LOCALAPPDATA%\StarHealth otherwise.</summary>
    public static string FolderPath()
    {
        try
        {
            return Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            try { return FallbackDir; }
            catch { return Path.GetTempPath(); }
        }
    }

    public static string? GetString(string key)
    {
        try { return Windows.Storage.ApplicationData.Current.LocalSettings.Values[key] as string; }
        catch { return ReadFile(key); }
    }

    public static void SetString(string key, string value)
    {
        try { Windows.Storage.ApplicationData.Current.LocalSettings.Values[key] = value; }
        catch { /* file fallback below still persists it */ }
        WriteFile(key, value);
    }

    public static bool GetBool(string key, bool def)
    {
        try
        {
            if (Windows.Storage.ApplicationData.Current.LocalSettings.Values[key] is bool b) return b;
        }
        catch { /* fall through to file */ }
        var s = ReadFile(key)?.Trim();
        // Accept legacy "1"/"0" files written before v0.1.6-alpha.
        if (s == "1") return true;
        if (s == "0") return false;
        return bool.TryParse(s, out bool fb) ? fb : def;
    }

    public static void SetBool(string key, bool value)
    {
        try { Windows.Storage.ApplicationData.Current.LocalSettings.Values[key] = value; }
        catch { /* file fallback below still persists it */ }
        // Must be "true"/"false": bool.TryParse (used on read) rejects "1"/"0".
        WriteFile(key, value ? "true" : "false");
    }

    private static string FilePath(string key)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) key = key.Replace(c, '_');
        return Path.Combine(FallbackDir, "settings", key + ".txt");
    }

    private static string? ReadFile(string key)
    {
        try
        {
            var p = FilePath(key);
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }
        catch { return null; }
    }

    private static void WriteFile(string key, string value)
    {
        try
        {
            var p = FilePath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, value);
        }
        catch { }
    }
}
