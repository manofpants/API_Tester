using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace ApiTester.Services;

public static class SyntaxHighlighting
{
    private static IHighlightingDefinition? _jsonLight;
    private static IHighlightingDefinition? _jsonDark;

    public static IHighlightingDefinition? ForBody(string body, string? contentType, bool dark = false)
    {
        if (IsJson(body, contentType))
            return dark ? JsonDark : JsonLight;

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                return HighlightingManager.Instance.GetDefinition("XML");
            }
        }

        var trimmed = body.TrimStart();
        if (trimmed.StartsWith('<'))
            return HighlightingManager.Instance.GetDefinition("XML");

        return null;
    }

    public static bool IsJson(string body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static IHighlightingDefinition JsonLight =>
        _jsonLight ??= LoadDefinition("Json", "pack://application:,,,/Resources/Json.xshd");

    private static IHighlightingDefinition JsonDark =>
        _jsonDark ??= LoadDefinition("JsonDark", "pack://application:,,,/Resources/JsonDark.xshd");

    private static IHighlightingDefinition LoadDefinition(string name, string packUri)
    {
        var uri = new Uri(packUri);
        using var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
        if (stream is null)
            return HighlightingManager.Instance.GetDefinition("JavaScript")
                   ?? throw new InvalidOperationException($"Missing highlighting definition: {name}");

        using var reader = XmlReader.Create(stream);
        var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, [".json"], definition);
        return definition;
    }
}
