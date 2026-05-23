using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace P3DDebin.Core;

// App-level preferences persisted as JSON in %APPDATA%\P3DDebin\prefs.json.
// Stored: last input/output folder, window bounds, last prefix rules.
// Language is kept in Strings.SavePreference for backward compatibility.
public sealed class Prefs
{
    public string LastInput  { get; set; } = string.Empty;
    public string LastOutput { get; set; } = string.Empty;

    public int WindowX      { get; set; } = -1;
    public int WindowY      { get; set; } = -1;
    public int WindowWidth  { get; set; } = -1;
    public int WindowHeight { get; set; } = -1;
    public bool WindowMaximized { get; set; }

    public PrefRule[] RecentRules { get; set; } = Array.Empty<PrefRule>();

    public sealed class PrefRule
    {
        public string From { get; set; } = string.Empty;
        public string To   { get; set; } = string.Empty;
        public bool   Regex { get; set; }
    }

    [JsonIgnore]
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "P3DDebin", "prefs.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static Prefs Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Prefs();
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<Prefs>(json) ?? new Prefs();
        }
        catch
        {
            return new Prefs();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch
        {
            // Best-effort; preferences are not critical.
        }
    }
}
