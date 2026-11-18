using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiTester.Services;

public sealed class SavedHeader
{
    public bool Enabled { get; set; } = true;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class SavedSession
{
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = string.Empty;
    public List<SavedHeader> Headers { get; set; } = new();
    public string Body { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "None";
    public string BearerToken { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string ApiKeyLocation { get; set; } = "Header";

    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public string? WindowState { get; set; }
    public double? RequestSectionHeight { get; set; }
    public double? ResponseSectionHeight { get; set; }
    public string Theme { get; set; } = "light";
}

public static class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ApiTester");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "session.json");
        }
    }

    public static SavedSession? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<SavedSession>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(SavedSession session)
    {
        try
        {
            var json = JsonSerializer.Serialize(session, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Persistence is best-effort; don't crash the app.
        }
    }
}
