using System.Windows;
using System.Windows.Media;
using ApiTester.Services;

namespace ApiTester;

public partial class UpdateConfirmWindow : Window
{
    public UpdateConfirmWindow(UpdateCheckResult update, AppTheme theme)
    {
        InitializeComponent();
        ApplyChrome(theme);

        TitleText.Text = update.ReleaseName ?? $"API Tester v{update.LatestVersion}";
        SubtitleText.Text =
            $"Version {update.CurrentVersion} → {update.LatestVersion}. Updating will download the new build and restart the app.";
        NotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? "No release notes were provided for this version."
            : update.ReleaseNotes.Trim();
    }

    private void ApplyChrome(AppTheme theme)
    {
        Background = theme.WindowBackgroundBrush;
        Foreground = theme.PrimaryTextBrush;

        SetBrush("WindowBackgroundBrush", theme.WindowBackground);
        SetBrush("SurfaceBackgroundBrush", theme.SurfaceBackground);
        SetBrush("PrimaryTextBrush", theme.PrimaryText);
        SetBrush("SecondaryTextBrush", theme.SecondaryText);
        SetBrush("BorderBrushTheme", theme.Border);
        SetBrush("AccentBrush", theme.Accent);
        SetBrush("AccentTextBrush", theme.AccentText);
        SetBrush("ControlBackgroundBrush", theme.ControlBackground);
        SetBrush("ControlBorderBrush", theme.ControlBorder);

        UpdateButton.Background = theme.AccentBrush;
        UpdateButton.Foreground = theme.AccentTextBrush;
        UpdateButton.BorderThickness = new Thickness(0);

        CancelButton.Background = theme.ControlBackgroundBrush;
        CancelButton.Foreground = theme.PrimaryTextBrush;
        CancelButton.BorderBrush = theme.ControlBorderBrush;
        CancelButton.BorderThickness = new Thickness(1);
    }

    private void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Resources[key] = brush;
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
