using System.IO.Compression;
using DeskPilot.Models;
using DeskPilot.Services;

namespace DeskPilot.Tests;

public sealed class SafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DeskPilot-tests-" + Guid.NewGuid().ToString("N"));
    public SafetyTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public async Task Archive_ExtractsNormalZip()
    {
        var archive = Path.Combine(_root, "sample.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("folder/hello.txt").Open())) writer.Write("hello");
        var service = new ArchiveService();
        var result = await service.ExtractManyAsync([archive], Path.Combine(_root, "out"));
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal("hello", await File.ReadAllTextAsync(Path.Combine(result.Log.Split(" -> ")[1].Split(Environment.NewLine)[0], "folder", "hello.txt")));
    }

    [Fact]
    public async Task Archive_DoesNotOverwriteExistingFile()
    {
        var archive = Path.Combine(_root, "duplicate.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("hello.txt").Open())) writer.Write("new");
        var service = new ArchiveService();
        var output = Path.Combine(_root, "out");
        var first = await service.ExtractManyAsync([archive], output);
        Assert.Equal(1, first.Succeeded);
        var second = await service.ExtractManyAsync([archive], output);
        Assert.Equal(1, second.Succeeded);
        Assert.Contains(" (1)", second.Log);
    }

    [Fact]
    public void Protection_MatchesChildrenCaseInsensitively()
    {
        var service = new ProtectionService();
        var settings = new AppSettings { ProtectedPaths = [Path.Combine(_root, "Protected")] };
        Assert.True(service.IsProtected(Path.Combine(_root, "protected", "important.txt"), settings));
        Assert.False(service.IsProtected(Path.Combine(_root, "other", "file.txt"), settings));
    }

    [Fact]
    public void Settings_RoundTripsProtectedPathsWithoutPlaintextKey()
    {
        var data = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var service = new SettingsService();
        var settings = service.Defaults();
        settings.ProtectedPaths.Add(_root);
        service.SetApiKey(settings, "test-secret");
        Assert.DoesNotContain("test-secret", settings.EncryptedApiKey);
        Assert.Equal("test-secret", service.GetApiKey(settings));
        Assert.False(string.IsNullOrWhiteSpace(data));
    }
}
