using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeskPilot.Models;
using Microsoft.Win32;

namespace DeskPilot.Services;

public sealed class CleanerService
{
    private sealed class CleanupRules
    {
        public List<string> ProgramKeywords { get; set; } = ["360", "2345", "hao123"];
        public List<string> ExtensionKeywords { get; set; } = ["360.cn", "360safe.com", "so.com", "haosou.com", "360浏览器", "360安全"];
    }

    private readonly string _rulesPath;
    public CleanerService(SettingsService settings) => _rulesPath = Path.Combine(settings.DataDirectory, "cleanup-rules.json");

    public Task<List<CleanupCandidate>> ScanAsync() => Task.Run(() =>
    {
        var rules = LoadRules();
        var items = new List<CleanupCandidate>();
        ScanPrograms(items, rules);
        ScanExtensions(items, rules);
        ScanTemp(items);
        return items.GroupBy(i => (i.Kind, i.Path, i.UninstallString)).Select(g => g.First()).OrderBy(i => i.Kind).ThenBy(i => i.Name).ToList();
    });

    private CleanupRules LoadRules()
    {
        try
        {
            if (File.Exists(_rulesPath)) return JsonSerializer.Deserialize<CleanupRules>(File.ReadAllText(_rulesPath)) ?? new CleanupRules();
            Directory.CreateDirectory(Path.GetDirectoryName(_rulesPath)!);
            var rules = new CleanupRules();
            File.WriteAllText(_rulesPath, JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true }));
            return rules;
        }
        catch { return new CleanupRules(); }
    }

    private static void ScanPrograms(List<CleanupCandidate> items, CleanupRules rules)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (root is null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var key = root.OpenSubKey(name);
                    if (key is null) continue;
                    var display = key.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(display) || (key.GetValue("SystemComponent") is int system && system == 1)) continue;
                    var publisher = key.GetValue("Publisher") as string ?? string.Empty;
                    var location = key.GetValue("InstallLocation") as string ?? string.Empty;
                    var reasons = new List<string>();
                    var keyword = rules.ProgramKeywords.FirstOrDefault(k => display.Contains(k, StringComparison.OrdinalIgnoreCase) || publisher.Contains(k, StringComparison.OrdinalIgnoreCase));
                    if (keyword is not null) reasons.Add($"名称/发布者匹配候选规则: {keyword}（不代表恶意）");
                    if (!string.IsNullOrWhiteSpace(location) && location.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) reasons.Add("安装位置位于临时目录");
                    if (reasons.Count == 0) continue;
                    items.Add(new CleanupCandidate
                    {
                        Name = display,
                        Kind = "Program",
                        Publisher = publisher,
                        Path = location,
                        UninstallString = key.GetValue("UninstallString") as string ?? string.Empty,
                        Reason = string.Join("；", reasons)
                    });
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException) { }
        }
    }

    private static void ScanExtensions(List<CleanupCandidate> items, CleanupRules rules)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[]
        {
            Path.Combine(local, "Google", "Chrome", "User Data"),
            Path.Combine(local, "Microsoft", "Edge", "User Data"),
            Path.Combine(local, "360Chrome", "Chrome", "User Data"),
            Path.Combine(local, "360se6", "User Data")
        };
        foreach (var root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> profiles;
            try { profiles = Directory.EnumerateDirectories(root).Where(d => Path.GetFileName(d).Equals("Default", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(Path.GetFileName(d), "^Profile \\d+$", RegexOptions.IgnoreCase)); }
            catch { continue; }
            foreach (var profile in profiles)
            {
                var extensions = Path.Combine(profile, "Extensions");
                if (!Directory.Exists(extensions)) continue;
                foreach (var versionDirectory in SafeDirectories(Path.Combine(extensions, "*", "*")))
                {
                    var manifest = Path.Combine(versionDirectory, "manifest.json");
                    if (!File.Exists(manifest)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                        var json = doc.RootElement.GetRawText();
                        var matched = rules.ExtensionKeywords.FirstOrDefault(k => json.Contains(k, StringComparison.OrdinalIgnoreCase));
                        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? Path.GetFileName(Path.GetDirectoryName(versionDirectory))! : Path.GetFileName(Path.GetDirectoryName(versionDirectory))!;
                        if (matched is null && !name.Contains("360", StringComparison.OrdinalIgnoreCase)) continue;
                        items.Add(new CleanupCandidate
                        {
                            Name = name,
                            Kind = "BrowserExtension",
                            Path = versionDirectory,
                            Reason = $"扩展清单匹配 360 相关规则: {matched ?? "360"}（需人工复核）",
                            SizeBytes = DirectorySize(versionDirectory)
                        });
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
                }
            }
        }
    }

    private static IEnumerable<string> SafeDirectories(string pattern)
    {
        var parent = Path.GetDirectoryName(pattern)!;
        var leaf = Path.GetFileName(pattern);
        if (!Directory.Exists(parent)) return [];
        var parentPattern = Path.GetFileName(parent);
        var grandParent = Path.GetDirectoryName(parent)!;
        try
        {
            return Directory.EnumerateDirectories(grandParent, parentPattern).SelectMany(d => Directory.EnumerateDirectories(d, leaf)).ToArray();
        }
        catch { return []; }
    }

    private static void ScanTemp(List<CleanupCandidate> items)
    {
        var temp = Path.GetTempPath();
        var threshold = DateTime.UtcNow.AddDays(-7);
        foreach (var path in Directory.EnumerateFileSystemEntries(temp).Take(2000))
        {
            try
            {
                var isDirectory = Directory.Exists(path);
                var lastWrite = isDirectory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);
                if (lastWrite > threshold) continue;
                items.Add(new CleanupCandidate
                {
                    Name = Path.GetFileName(path),
                    Kind = isDirectory ? "TempDirectory" : "TempFile",
                    Path = path,
                    Reason = "临时目录中超过 7 天未修改",
                    SizeBytes = isDirectory ? DirectorySize(path) : new FileInfo(path).Length
                });
            }
            catch { }
        }
    }

    private static long DirectorySize(string path)
    {
        try { return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Take(5000).Sum(f => { try { return f.Length; } catch { return 0L; } }); }
        catch { return 0; }
    }
}
