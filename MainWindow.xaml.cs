using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApiTester.Models;
using ApiTester.Services;

namespace ApiTester;

public partial class MainWindow : Window
{
    private readonly HttpRequestService _httpService = new();
    private readonly ObservableCollection<HeaderItem> _headers = new();
    private bool _isSending;
    private bool _sessionReady;

    public MainWindow()
    {
        InitializeComponent();
        HeadersGrid.ItemsSource = _headers;
        LoadSessionOrDefaults();
        UpdateAuthPanels();
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
            return;
        }

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
            Headers = _headers.Select(h => new SavedHeader
            {
                Enabled = h.Enabled,
                Key = h.Key ?? string.Empty,
                Value = h.Value ?? string.Empty
            }).ToList()
        };
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
