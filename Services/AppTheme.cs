using System.Windows.Media;

namespace ApiTester.Services;

public sealed class AppTheme
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Color WindowBackground { get; init; }
    public required Color PanelBackground { get; init; }
    public required Color SurfaceBackground { get; init; }
    public required Color StatusBackground { get; init; }
    public required Color PrimaryText { get; init; }
    public required Color SecondaryText { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentText { get; init; }
    public required Color Border { get; init; }
    public required Color Splitter { get; init; }
    public required Color ControlBackground { get; init; }
    public required Color ControlBorder { get; init; }

    public SolidColorBrush WindowBackgroundBrush => Solid(WindowBackground);
    public SolidColorBrush PanelBackgroundBrush => Solid(PanelBackground);
    public SolidColorBrush SurfaceBackgroundBrush => Solid(SurfaceBackground);
    public SolidColorBrush StatusBackgroundBrush => Solid(StatusBackground);
    public SolidColorBrush PrimaryTextBrush => Solid(PrimaryText);
    public SolidColorBrush SecondaryTextBrush => Solid(SecondaryText);
    public SolidColorBrush AccentBrush => Solid(Accent);
    public SolidColorBrush AccentTextBrush => Solid(AccentText);
    public SolidColorBrush BorderBrush => Solid(Border);
    public SolidColorBrush SplitterBrush => Solid(Splitter);
    public SolidColorBrush ControlBackgroundBrush => Solid(ControlBackground);
    public SolidColorBrush ControlBorderBrush => Solid(ControlBorder);

    private static SolidColorBrush Solid(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

public static class AppThemes
{
    public static readonly AppTheme Light = new()
    {
        Id = "light",
        Name = "Light",
        Description = "Clean light gray with blue accents",
        WindowBackground = Color.FromRgb(0xF4, 0xF5, 0xF7),
        PanelBackground = Colors.White,
        SurfaceBackground = Colors.White,
        StatusBackground = Color.FromRgb(0xEE, 0xF1, 0xF5),
        PrimaryText = Color.FromRgb(0x1F, 0x29, 0x37),
        SecondaryText = Color.FromRgb(0x55, 0x55, 0x55),
        Accent = Color.FromRgb(0x25, 0x63, 0xEB),
        AccentText = Colors.White,
        Border = Color.FromRgb(0xD0, 0xD4, 0xDB),
        Splitter = Color.FromRgb(0xD0, 0xD4, 0xDB),
        ControlBackground = Colors.White,
        ControlBorder = Color.FromRgb(0xC5, 0xC9, 0xD0)
    };

    public static readonly AppTheme Dark = new()
    {
        Id = "dark",
        Name = "Dark",
        Description = "Dark charcoal with bright blue accents",
        WindowBackground = Color.FromRgb(0x1A, 0x1D, 0x23),
        PanelBackground = Color.FromRgb(0x24, 0x28, 0x30),
        SurfaceBackground = Color.FromRgb(0x2B, 0x30, 0x3A),
        StatusBackground = Color.FromRgb(0x32, 0x37, 0x42),
        PrimaryText = Color.FromRgb(0xE8, 0xEA, 0xED),
        SecondaryText = Color.FromRgb(0xA0, 0xA6, 0xB0),
        Accent = Color.FromRgb(0x3B, 0x82, 0xF6),
        AccentText = Colors.White,
        Border = Color.FromRgb(0x3A, 0x40, 0x4C),
        Splitter = Color.FromRgb(0x3A, 0x40, 0x4C),
        ControlBackground = Color.FromRgb(0x2B, 0x30, 0x3A),
        ControlBorder = Color.FromRgb(0x4A, 0x51, 0x5E)
    };

    public static readonly AppTheme Ocean = new()
    {
        Id = "ocean",
        Name = "Ocean",
        Description = "Cool blue-gray surfaces with teal accents",
        WindowBackground = Color.FromRgb(0xE8, 0xF1, 0xF5),
        PanelBackground = Color.FromRgb(0xF7, 0xFB, 0xFC),
        SurfaceBackground = Colors.White,
        StatusBackground = Color.FromRgb(0xD7, 0xE8, 0xEF),
        PrimaryText = Color.FromRgb(0x1A, 0x33, 0x3F),
        SecondaryText = Color.FromRgb(0x4A, 0x6A, 0x78),
        Accent = Color.FromRgb(0x0D, 0x94, 0x88),
        AccentText = Colors.White,
        Border = Color.FromRgb(0xB7, 0xCF, 0xD9),
        Splitter = Color.FromRgb(0xB7, 0xCF, 0xD9),
        ControlBackground = Colors.White,
        ControlBorder = Color.FromRgb(0xA8, 0xC2, 0xCD)
    };

    public static readonly AppTheme HighContrast = new()
    {
        Id = "contrast",
        Name = "High Contrast",
        Description = "Near-black background with strong yellow accents",
        WindowBackground = Color.FromRgb(0x0A, 0x0A, 0x0A),
        PanelBackground = Color.FromRgb(0x12, 0x12, 0x12),
        SurfaceBackground = Color.FromRgb(0x1A, 0x1A, 0x1A),
        StatusBackground = Color.FromRgb(0x22, 0x22, 0x22),
        PrimaryText = Colors.White,
        SecondaryText = Color.FromRgb(0xDD, 0xDD, 0xDD),
        Accent = Color.FromRgb(0xFF, 0xCC, 0x00),
        AccentText = Colors.Black,
        Border = Color.FromRgb(0x66, 0x66, 0x66),
        Splitter = Color.FromRgb(0x66, 0x66, 0x66),
        ControlBackground = Color.FromRgb(0x1A, 0x1A, 0x1A),
        ControlBorder = Color.FromRgb(0x88, 0x88, 0x88)
    };

    public static IReadOnlyList<AppTheme> All { get; } = [Light, Dark, Ocean, HighContrast];

    public static AppTheme GetById(string? id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Light;
}
