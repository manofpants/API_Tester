using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApiTester.Services;

namespace ApiTester;

public partial class SettingsWindow : Window
{
    private readonly ObservableCollection<ThemeOption> _options = new();
    private readonly Action<string> _onThemeSelected;
    private bool _ready;

    public SettingsWindow(string currentThemeId, Action<string> onThemeSelected, AppTheme hostTheme)
    {
        _onThemeSelected = onThemeSelected;
        InitializeComponent();
        ApplyChrome(hostTheme);

        foreach (var theme in AppThemes.All)
        {
            _options.Add(new ThemeOption
            {
                Id = theme.Id,
                Name = theme.Name,
                Description = theme.Description,
                Swatch1 = theme.WindowBackgroundBrush,
                Swatch2 = theme.PanelBackgroundBrush,
                Swatch3 = theme.AccentBrush,
                Swatch4 = theme.PrimaryTextBrush,
                IsSelected = string.Equals(theme.Id, currentThemeId, StringComparison.OrdinalIgnoreCase)
            });
        }

        ThemeList.ItemsSource = _options;
        _ready = true;
    }

    private void ApplyChrome(AppTheme theme)
    {
        SetBrush("SettingsWindowBackground", theme.WindowBackground);
        SetBrush("SettingsPrimaryText", theme.PrimaryText);
        SetBrush("SettingsSecondaryText", theme.SecondaryText);
        SetBrush("SettingsAccent", theme.Accent);
        SetBrush("SettingsAccentText", theme.AccentText);

        Background = theme.WindowBackgroundBrush;
        Foreground = theme.PrimaryTextBrush;
    }

    private void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Resources[key] = brush;
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton { Tag: string id })
            return;

        foreach (var option in _options)
            option.IsSelected = string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase);

        _onThemeSelected(id);
        ApplyChrome(AppThemes.GetById(id));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class ThemeOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Brush Swatch1 { get; init; }
    public required Brush Swatch2 { get; init; }
    public required Brush Swatch3 { get; init; }
    public required Brush Swatch4 { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
