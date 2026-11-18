using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ApiTester.Models;
using ApiTester.Services;

namespace ApiTester;

public partial class MainWindow : Window
{
    private readonly HttpRequestService _httpService = new();
    private readonly ObservableCollection<HeaderItem> _headers = new();
    private bool _isSending;
    private bool _sessionReady;
    private string _themeId = AppThemes.Light.Id;

    public MainWindow()
    {
        InitializeComponent();
        HeadersGrid.ItemsSource = _headers;
        LoadSessionOrDefaults();
        UpdateAuthPanels();
        ApplyTheme(AppThemes.GetById(_themeId));
        _sessionReady = true;
        Deactivated += (_, _) => PersistSession();
    }

    private void SeedDefaultHeaders()
    {
        _headers.Add(new HeaderItem { Enabled = true, Key = "Accept", Value = "application/json" });
        _headers.Add(new HeaderItem { Enabled = true, Key = "Content-Type", Value = "application/json" });
    }

    private void LoadSessionOrDefaults()
    {
        var session = SessionStore.Load();
        if (session is null)
        {
            SeedDefaultHeaders();
            ApplyTheme(AppThemes.Light);
            return;
        }

        _themeId = string.IsNullOrWhiteSpace(session.Theme) ? AppThemes.Light.Id : session.Theme;
        ApplyWindowLayout(session);

        SelectMethod(session.Method);
        UrlBox.Text = session.Url ?? string.Empty;
        BodyBox.Text = session.Body ?? string.Empty;
        BearerTokenBox.Text = session.BearerToken ?? string.Empty;
        ApiKeyBox.Text = session.ApiKey ?? string.Empty;
        ApiSecretBox.Text = session.ApiSecret ?? string.Empty;
        ApiKeyLocationCombo.SelectedIndex =
            string.Equals(session.ApiKeyLocation, "Query", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        AuthNone.IsChecked = false;
        AuthBearer.IsChecked = false;
        AuthApiKey.IsChecked = false;
        switch (session.AuthMode)
        {
            case "Bearer":
                AuthBearer.IsChecked = true;
                break;
            case "ApiKey":
                AuthApiKey.IsChecked = true;
                break;
            default:
                AuthNone.IsChecked = true;
                break;
        }

        _headers.Clear();
        if (session.Headers is { Count: > 0 })
        {
            foreach (var header in session.Headers)
            {
                _headers.Add(new HeaderItem
                {
                    Enabled = header.Enabled,
                    Key = header.Key ?? string.Empty,
                    Value = header.Value ?? string.Empty
                });
            }
        }
        else
        {
            SeedDefaultHeaders();
        }
    }

    private void ApplyWindowLayout(SavedSession session)
    {
        if (session.WindowWidth is > 0)
            Width = Math.Max(MinWidth, session.WindowWidth.Value);
        if (session.WindowHeight is > 0)
            Height = Math.Max(MinHeight, session.WindowHeight.Value);

        if (session.WindowLeft is not null && session.WindowTop is not null)
        {
            var left = session.WindowLeft.Value;
            var top = session.WindowTop.Value;
            if (IsOnScreen(left, top, Width, Height))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }
        }

        if (string.Equals(session.WindowState, "Maximized", StringComparison.OrdinalIgnoreCase))
            WindowState = WindowState.Maximized;

        if (session.RequestSectionHeight is > 0 && session.ResponseSectionHeight is > 0)
        {
            RequestSectionRow.Height = new GridLength(session.RequestSectionHeight.Value, GridUnitType.Pixel);
            ResponseSectionRow.Height = new GridLength(session.ResponseSectionHeight.Value, GridUnitType.Pixel);
        }
    }

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var workArea = SystemParameters.VirtualScreenLeft;
        var workTop = SystemParameters.VirtualScreenTop;
        var workWidth = SystemParameters.VirtualScreenWidth;
        var workHeight = SystemParameters.VirtualScreenHeight;

        var right = left + width;
        var bottom = top + height;

        // Require at least part of the title bar area to be visible.
        return right > workArea + 40 &&
               left < workArea + workWidth - 40 &&
               bottom > workTop + 40 &&
               top < workTop + workHeight - 40;
    }

    private void SelectMethod(string? method)
    {
        var target = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
        for (var i = 0; i < MethodCombo.Items.Count; i++)
        {
            if (MethodCombo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), target, StringComparison.OrdinalIgnoreCase))
            {
                MethodCombo.SelectedIndex = i;
                return;
            }
        }

        MethodCombo.SelectedIndex = 0;
    }

    private SavedSession CaptureSession()
    {
        // Commit any in-progress DataGrid edit before reading headers.
        HeadersGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        HeadersGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var methodItem = MethodCombo.SelectedItem as ComboBoxItem;
        var authMode = "None";
        if (AuthBearer.IsChecked == true)
            authMode = "Bearer";
        else if (AuthApiKey.IsChecked == true)
            authMode = "ApiKey";

        var restore = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        return new SavedSession
        {
            Method = methodItem?.Content?.ToString() ?? "GET",
            Url = UrlBox.Text ?? string.Empty,
            Body = BodyBox.Text ?? string.Empty,
            AuthMode = authMode,
            BearerToken = BearerTokenBox.Text ?? string.Empty,
            ApiKey = ApiKeyBox.Text ?? string.Empty,
            ApiSecret = ApiSecretBox.Text ?? string.Empty,
            ApiKeyLocation = ApiKeyLocationCombo.SelectedIndex == 1 ? "Query" : "Header",
            WindowWidth = restore.Width,
            WindowHeight = restore.Height,
            WindowLeft = restore.Left,
            WindowTop = restore.Top,
            WindowState = WindowState.ToString(),
            RequestSectionHeight = RequestSectionRow.ActualHeight > 0
                ? RequestSectionRow.ActualHeight
                : RequestSectionRow.Height.IsAbsolute ? RequestSectionRow.Height.Value : null,
            ResponseSectionHeight = ResponseSectionRow.ActualHeight > 0
                ? ResponseSectionRow.ActualHeight
                : ResponseSectionRow.Height.IsAbsolute ? ResponseSectionRow.Height.Value : null,
            Theme = _themeId,
            Headers = _headers.Select(h => new SavedHeader
            {
                Enabled = h.Enabled,
                Key = h.Key ?? string.Empty,
                Value = h.Value ?? string.Empty
            }).ToList()
        };
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow(
            _themeId,
            themeId =>
            {
                _themeId = themeId;
                ApplyTheme(AppThemes.GetById(themeId));
                PersistSession();
            },
            AppThemes.GetById(_themeId))
        {
            Owner = this
        };

        settings.ShowDialog();
    }

    private void ApplyTheme(AppTheme theme)
    {
        SetBrush("WindowBackgroundBrush", theme.WindowBackground);
        SetBrush("PanelBackgroundBrush", theme.PanelBackground);
        SetBrush("SurfaceBackgroundBrush", theme.SurfaceBackground);
        SetBrush("StatusBackgroundBrush", theme.StatusBackground);
        SetBrush("PrimaryTextBrush", theme.PrimaryText);
        SetBrush("SecondaryTextBrush", theme.SecondaryText);
        SetBrush("AccentBrush", theme.Accent);
        SetBrush("AccentTextBrush", theme.AccentText);
        SetBrush("BorderBrushTheme", theme.Border);
        SetBrush("SplitterBrush", theme.Splitter);
        SetBrush("ControlBackgroundBrush", theme.ControlBackground);
        SetBrush("ControlBorderBrush", theme.ControlBorder);

        Background = theme.WindowBackgroundBrush;
        SendButton.Background = theme.AccentBrush;
        SendButton.Foreground = theme.AccentTextBrush;
        ResponseBodyEditor.Foreground = theme.PrimaryTextBrush;
        ResponseBodyEditor.Background = Brushes.Transparent;
        ResponseBodyEditor.LineNumbersForeground = theme.SecondaryTextBrush;
    }

    private void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Resources[key] = brush;
    }

    private void PersistSession()
    {
        if (!_sessionReady)
            return;

        SessionStore.Save(CaptureSession());
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            _ = SendAsync();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync();

    private void AddHeader_Click(object sender, RoutedEventArgs e)
    {
        var item = new HeaderItem { Enabled = true, Key = string.Empty, Value = string.Empty };
        _headers.Add(item);
        HeadersGrid.SelectedItem = item;
        HeadersGrid.ScrollIntoView(item);
        HeadersGrid.CurrentCell = new DataGridCellInfo(item, HeadersGrid.Columns[1]);
        HeadersGrid.BeginEdit();
        PersistSession();
    }

    private void RemoveHeader_Click(object sender, RoutedEventArgs e)
    {
        if (HeadersGrid.SelectedItem is HeaderItem selected)
        {
            _headers.Remove(selected);
            PersistSession();
        }
    }

    private void AuthMode_Changed(object sender, RoutedEventArgs e)
    {
        UpdateAuthPanels();
        PersistSession();
    }

    private void UpdateAuthPanels()
    {
        if (BearerPanel is null || ApiKeyPanel is null)
            return;

        BearerPanel.Visibility = AuthBearer.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApiKeyPanel.Visibility = AuthApiKey.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async Task SendAsync()
    {
        if (_isSending)
            return;

        _isSending = true;
        SendButton.IsEnabled = false;
        ResponseStatusText.Text = "Sending…";
        ResponseBodyEditor.Text = string.Empty;
        ResponseBodyEditor.SyntaxHighlighting = null;
        ResponseHeadersBox.Text = string.Empty;

        try
        {
            PersistSession();
            var request = BuildRequest();
            var response = await _httpService.SendAsync(request);
            RenderResponse(response);
        }
        finally
        {
            _isSending = false;
            SendButton.IsEnabled = true;
        }
    }

    private ApiRequest BuildRequest()
    {
        var methodItem = MethodCombo.SelectedItem as ComboBoxItem;
        var method = methodItem?.Content?.ToString() ?? "GET";

        var headers = _headers
            .Where(h => h.Enabled && !string.IsNullOrWhiteSpace(h.Key))
            .Select(h => (h.Key.Trim(), h.Value ?? string.Empty))
            .ToList();

        var authMode = AuthMode.None;
        if (AuthBearer.IsChecked == true)
            authMode = AuthMode.Bearer;
        else if (AuthApiKey.IsChecked == true)
            authMode = AuthMode.ApiKey;

        var apiKeyLocation = ApiKeyLocation.Header;
        if (ApiKeyLocationCombo.SelectedIndex == 1)
            apiKeyLocation = ApiKeyLocation.Query;

        return new ApiRequest
        {
            Method = method,
            Url = UrlBox.Text?.Trim() ?? string.Empty,
            Headers = headers,
            Body = BodyBox.Text,
            AuthMode = authMode,
            BearerToken = BearerTokenBox.Text,
            ApiKey = ApiKeyBox.Text,
            ApiSecret = ApiSecretBox.Text,
            ApiKeyLocation = apiKeyLocation
        };
    }

    private void RenderResponse(ApiResponse response)
    {
        ResponseStatusText.Text = response.StatusText;

        if (response.IsError)
        {
            ResponseBodyEditor.SyntaxHighlighting = null;
            ResponseBodyEditor.Text = response.Body;
            ResponseHeadersBox.Text = string.Empty;
            return;
        }

        var formatted = JsonFormatter.TryPrettyPrint(response.Body, response.ContentType);
        ResponseBodyEditor.SyntaxHighlighting = SyntaxHighlighting.ForBody(formatted, response.ContentType);
        ResponseBodyEditor.Text = formatted;
        ResponseHeadersBox.Text = string.Join(
            Environment.NewLine,
            response.Headers.Select(h => $"{h.Key}: {h.Value}"));
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        PersistSession();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _httpService.Dispose();
        base.OnClosed(e);
    }
}
