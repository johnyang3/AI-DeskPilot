using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using DeskPilot.Models;

namespace DeskPilot.Services;

public sealed record UpdateInfo(string Version, string PackageUrl, string HashUrl);

public sealed class UpdateService
{
    private static readonly HttpClient Http = new();
    static UpdateService() => Http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DeskPilot", CurrentVersion));

    public static string CurrentVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);
            return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }
    }

    public async Task<UpdateInfo?> CheckAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{Uri.EscapeDataString(settings.GitHubOwner)}/{Uri.EscapeDataString(settings.GitHubRepo)}/releases/latest";
        using var response = await Http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub API {(int)response.StatusCode}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var tag = json.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
        if (!Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out var remote)) throw new InvalidDataException("Invalid release tag.");
        var current = Version.Parse(CurrentVersion);
        if (Normalize(remote) <= Normalize(current)) return null;
        var assets = json.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        string? Find(string name)
        {
            var asset = assets.FirstOrDefault(a => string.Equals(a.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase));
            return asset.ValueKind == JsonValueKind.Undefined ? null : asset.GetProperty("browser_download_url").GetString();
        }
        var package = Find("DeskPilot-win-x64.zip");
        var hash = Find("DeskPilot-win-x64.zip.sha256");
        if (package is null || hash is null) throw new InvalidDataException("Release package or SHA-256 asset is missing.");
        return new UpdateInfo(tag, package, hash);
    }

    public async Task<string> DownloadAndVerifyAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        if (!update.PackageUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
            !update.HashUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Update assets must be hosted on GitHub.");
        var packagePath = Path.Combine(Path.GetTempPath(), $"DeskPilot-{Guid.NewGuid():N}.zip");
        await DownloadAsync(update.PackageUrl, packagePath, cancellationToken);
        var hashText = await Http.GetStringAsync(update.HashUrl, cancellationToken);
        var expected = hashText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        await using var stream = File.OpenRead(packagePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        if (!actual.Equals(expected.ToLowerInvariant(), StringComparison.Ordinal))
        {
            File.Delete(packagePath);
            throw new InvalidDataException("SHA-256 verification failed.");
        }
        return packagePath;
    }

    public void StartUpdater(string packagePath)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"DeskPilot-updater-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, UpdaterScript);
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var info = new ProcessStartInfo(powershell) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");
        info.ArgumentList.Add(scriptPath);
        info.ArgumentList.Add("-ProcessId");
        info.ArgumentList.Add(Environment.ProcessId.ToString());
        info.ArgumentList.Add("-ArchivePath");
        info.ArgumentList.Add(packagePath);
        info.ArgumentList.Add("-TargetDirectory");
        info.ArgumentList.Add(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        info.ArgumentList.Add("-ExecutablePath");
        info.ArgumentList.Add(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "DeskPilot.exe"));
        Process.Start(info);
    }

    private static async Task DownloadAsync(string url, string path, CancellationToken cancellationToken)
    {
        await using var source = await Http.GetStreamAsync(url, cancellationToken);
        await using var destination = File.Create(path);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static Version Normalize(Version value) => new(value.Major, value.Minor, Math.Max(0, value.Build));

    private const string UpdaterScript = @"
param([int]$ProcessId, [string]$ArchivePath, [string]$TargetDirectory, [string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
try { Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction Stop } catch { Start-Sleep -Seconds 2 }
$token = [Guid]::NewGuid().ToString('N')
$stage = Join-Path ([IO.Path]::GetTempPath()) ('DeskPilot-stage-' + $token)
$backup = Join-Path ([IO.Path]::GetTempPath()) ('DeskPilot-backup-' + $token)
New-Item -ItemType Directory -Path $stage, $backup | Out-Null
Expand-Archive -LiteralPath $ArchivePath -DestinationPath $stage -Force
Get-ChildItem -LiteralPath $TargetDirectory -Force | Copy-Item -Destination $backup -Recurse -Force
try {
    Get-ChildItem -LiteralPath $stage -Force | Copy-Item -Destination $TargetDirectory -Recurse -Force
    Start-Process -FilePath $ExecutablePath -WorkingDirectory $TargetDirectory
} catch {
    Get-ChildItem -LiteralPath $backup -Force | Copy-Item -Destination $TargetDirectory -Recurse -Force
    throw
}
Remove-Item -LiteralPath $stage, $backup, $ArchivePath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
";
}
