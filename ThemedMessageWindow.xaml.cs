using System.Windows;
using System.Windows.Media;
using ApiTester.Services;

namespace ApiTester;

public partial class ThemedMessageWindow : Window
{
    public ThemedMessageWindow(string title, string heading, string body, AppTheme theme)
    {
        InitializeComponent();
        Title = title;
        HeadingText.Text = heading;
        BodyText.Text = body;
        ApplyChrome(theme);
    }

    public static void Show(Window owner, string title, string heading, string body, AppTheme theme)
    {
        var dialog = new ThemedMessageWindow(title, heading, body, theme)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    private void ApplyChrome(AppTheme theme)
    {
        SetBrush("MsgWindowBackground", theme.WindowBackground);
        SetBrush("MsgPrimaryText", theme.PrimaryText);
        SetBrush("MsgSecondaryText", theme.SecondaryText);
        SetBrush("MsgAccent", theme.Accent);
        SetBrush("MsgAccentText", theme.AccentText);

        Background = theme.WindowBackgroundBrush;
        Foreground = theme.PrimaryTextBrush;
        CloseButton.Background = theme.AccentBrush;
        CloseButton.Foreground = theme.AccentTextBrush;
        CloseButton.BorderThickness = new Thickness(0);
    }

    private void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Resources[key] = brush;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
