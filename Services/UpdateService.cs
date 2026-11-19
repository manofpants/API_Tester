using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ApiTester.Services;

public sealed class UpdateCheckResult
{
    public bool Succeeded { get; init; }
    public bool UpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string? LatestVersion { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ReleaseUrl { get; init; }
    public string? ReleaseName { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class UpdateService : IDisposable
{
    private const string Owner = "manofpants";
    private const string Repo = "API_Tester";
    private const string AssetPrefix = "API_Tester-win-x64";
    private const string LegacyAssetName = "API_Tester-win-x64.zip";

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ApiTester", GetCurrentVersion()));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null)
            return "0.0.0";

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var current = GetCurrentVersion();

        try
        {
            using var response = await _http.GetAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult
                {
                    Succeeded = false,
                    CurrentVersion = current,
                    ErrorMessage = $"Could not check for updates ({(int)response.StatusCode})."
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var latest = NormalizeVersion(tag);
            var releaseUrl = root.TryGetProperty("html_url", out var htmlUrl)
                ? htmlUrl.GetString()
                : $"https://github.com/{Owner}/{Repo}/releases/latest";
            var releaseName = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var releaseNotes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;

            var downloadUrl = FindReleaseAssetUrl(root, latest);

            var updateAvailable = IsNewer(latest, current) && !string.IsNullOrWhiteSpace(downloadUrl);

            return new UpdateCheckResult
            {
                Succeeded = true,
                UpdateAvailable = updateAvailable,
                CurrentVersion = current,
                LatestVersion = latest,
                DownloadUrl = downloadUrl,
                ReleaseUrl = releaseUrl,
                ReleaseName = string.IsNullOrWhiteSpace(releaseName) ? $"API Tester v{latest}" : releaseName,
                ReleaseNotes = string.IsNullOrWhiteSpace(releaseNotes)
                    ? "No release notes were provided for this version."
                    : releaseNotes,
                ErrorMessage = updateAvailable || !string.IsNullOrWhiteSpace(downloadUrl)
                    ? null
                    : string.IsNullOrWhiteSpace(downloadUrl)
                        ? $"Latest release ({latest}) has no {AssetPrefix}-v{{version}}.zip asset."
                        : null
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                Succeeded = false,
                CurrentVersion = current,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task ApplyUpdateAsync(string downloadUrl, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not locate the running application path.");
        var appDir = Path.GetDirectoryName(currentExe)
            ?? throw new InvalidOperationException("Could not locate the application folder.");

        var tempRoot = Path.Combine(Path.GetTempPath(), "ApiTesterUpdate", Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(tempRoot, "update.zip");
        var extractDir = Path.Combine(tempRoot, "extract");
        Directory.CreateDirectory(extractDir);

        try
        {
            progress?.Report("Downloading update…");
            await DownloadFileAsync(downloadUrl, zipPath, cancellationToken);

            progress?.Report("Unpacking…");
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            var newExe = Directory.EnumerateFiles(extractDir, "ApiTester.exe", SearchOption.AllDirectories)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Update package did not contain ApiTester.exe.");

            var stagingExe = Path.Combine(appDir, "ApiTester.new.exe");
            if (File.Exists(stagingExe))
                File.Delete(stagingExe);

            File.Copy(newExe, stagingExe, overwrite: true);

            // PowerShell helper avoids cmd "timeout" failing when no console is attached.
            var scriptPath = Path.Combine(appDir, "apply-update.ps1");
            var logPath = Path.Combine(appDir, "update.log");
            var pid = Environment.ProcessId;

            var script = $$"""
                $ErrorActionPreference = 'Continue'
                $target = '{{EscapeForPowerShellSingleQuoted(currentExe)}}'
                $new = '{{EscapeForPowerShellSingleQuoted(stagingExe)}}'
                $log = '{{EscapeForPowerShellSingleQuoted(logPath)}}'
                $pidToWait = {{pid}}
                function Write-Log([string]$message) {
                  Add-Content -Path $log -Value ("[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $message)
                }
                Write-Log "Updater started. Waiting for PID $pidToWait to exit."
                while (Get-Process -Id $pidToWait -ErrorAction SilentlyContinue) {
                  Start-Sleep -Milliseconds 400
                }
                Write-Log "Process exited. Replacing executable."
                $replaced = $false
                for ($i = 0; $i -lt 30; $i++) {
                  try {
                    Copy-Item -LiteralPath $new -Destination $target -Force
                    Remove-Item -LiteralPath $new -Force -ErrorAction SilentlyContinue
                    $replaced = $true
                    break
                  } catch {
                    Write-Log ("Replace attempt failed: " + $_.Exception.Message)
                    Start-Sleep -Milliseconds 500
                  }
                }
                if (-not $replaced) {
                  Write-Log "Failed to replace executable after retries."
                  exit 1
                }
                Write-Log "Starting updated app."
                Start-Process -FilePath $target
                Write-Log "Updater finished."
                Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
                """;

            await File.WriteAllTextAsync(scriptPath, script, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

            progress?.Report("Restarting…");
            var started = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                WorkingDirectory = appDir,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (started is null)
                throw new InvalidOperationException("Could not start the update helper process.");

            // Give the helper a moment to attach before this process exits.
            await Task.Delay(500, cancellationToken);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; updater no longer needs the temp extract once staged.
            }
        }
    }

    private static string EscapeForPowerShellSingleQuoted(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private static string? FindReleaseAssetUrl(JsonElement root, string latestVersion)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        var preferredNames = new[]
        {
            $"{AssetPrefix}-v{latestVersion}.zip",
            $"{AssetPrefix}-{latestVersion}.zip",
            LegacyAssetName
        };

        foreach (var preferred in preferredNames)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                if (string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase) &&
                    asset.TryGetProperty("browser_download_url", out var urlProp))
                {
                    return urlProp.GetString();
                }
            }
        }

        // Fallback: any API_Tester-win-x64*.zip on the release.
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (!string.IsNullOrWhiteSpace(name) &&
                name.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                asset.TryGetProperty("browser_download_url", out var urlProp))
            {
                return urlProp.GetString();
            }
        }

        return null;
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var local = File.Create(destinationPath);
        await remote.CopyToAsync(local, cancellationToken);
    }

    private static string NormalizeVersion(string tag)
    {
        var value = tag.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value[1..];
        return value;
    }

    private static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(PadVersion(latest), out var latestVersion))
            return false;
        if (!Version.TryParse(PadVersion(current), out var currentVersion))
            return true;

        return latestVersion > currentVersion;
    }

    private static string PadVersion(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        while (parts.Length < 3)
            parts = parts.Append("0").ToArray();
        return string.Join('.', parts.Take(4));
    }

    public void Dispose() => _http.Dispose();
}
