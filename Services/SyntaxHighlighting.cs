using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace ApiTester.Services;

public static class SyntaxHighlighting
{
    private static IHighlightingDefinition? _json;

    public static IHighlightingDefinition? Json => _json ??= LoadJson();

    public static IHighlightingDefinition? ForBody(string body, string? contentType)
    {
        if (IsJson(body, contentType))
            return Json;

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

    private static IHighlightingDefinition? LoadJson()
    {
        var uri = new Uri("pack://application:,,,/Resources/Json.xshd");
        using var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
        if (stream is null)
            return HighlightingManager.Instance.GetDefinition("JavaScript");

        using var reader = XmlReader.Create(stream);
        var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting("Json", [".json"], definition);
        return definition;
    }
}
