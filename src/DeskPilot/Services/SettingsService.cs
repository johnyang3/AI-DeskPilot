using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeskPilot.Models;

namespace DeskPilot.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    public string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskPilot");
    private string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public AppSettings Load()
    {
        Directory.CreateDirectory(DataDirectory);
        if (!File.Exists(SettingsPath)) return Defaults();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? Defaults();
        }
        catch
        {
            return Defaults();
        }
    }

    public AppSettings Defaults() => new()
    {
        ProtectedPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()
    };

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataDirectory);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, _json), new UTF8Encoding(false));
        File.Move(temp, SettingsPath, true);
    }

    public void SetApiKey(AppSettings settings, string apiKey)
    {
        settings.EncryptedApiKey = string.IsNullOrEmpty(apiKey)
            ? string.Empty
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), null, DataProtectionScope.CurrentUser));
    }

    public string GetApiKey(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.EncryptedApiKey)) return string.Empty;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(settings.EncryptedApiKey), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }
}
