using System.Text.Json;

namespace ApiTester.Services;

public static class JsonFormatter
{
    public static string TryPrettyPrint(string body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body))
            return body;

        var looksLikeJson =
            (!string.IsNullOrWhiteSpace(contentType) &&
             contentType.Contains("json", StringComparison.OrdinalIgnoreCase)) ||
            LooksLikeJson(body);

        if (!looksLikeJson)
            return body;

        try
        {
            using var document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static bool LooksLikeJson(string body)
    {
        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}
