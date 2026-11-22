using System.IO;
using System.Text.Json;
using DeskPilot.Models;

namespace DeskPilot.Services;

public sealed class DesktopService
{
    private readonly ProtectionService _protection;
    private readonly string _historyDirectory;

    private static readonly Dictionary<string, string> Categories = new()
    {
        ["文档"] = ".txt .md .pdf .doc .docx .rtf .odt .xls .xlsx .csv .ppt .pptx",
        ["图片"] = ".jpg .jpeg .png .gif .bmp .webp .svg .ico",
        ["视频"] = ".mp4 .mkv .avi .mov .wmv .flv .webm",
        ["音频"] = ".mp3 .wav .flac .m4a .ogg .aac",
        ["压缩包"] = ".zip .rar .7z .tar .gz .tgz"
    };
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
        { ".lnk", ".url", ".exe", ".msi", ".msix", ".appx", ".bat", ".cmd", ".ps1", ".dll", ".sys", ".ini", ".db" };

    public DesktopService(SettingsService settings, ProtectionService protection)
    {
        _protection = protection;
        _historyDirectory = Path.Combine(settings.DataDirectory, "history");
    }

    public string DesktopPath => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public string OrganizedRoot => Path.Combine(DesktopPath, "DeskPilot整理");

    public List<DesktopMove> CreatePlan(AppSettings settings)
    {
        var result = new List<DesktopMove>();
        if (!Directory.Exists(DesktopPath)) return result;
        foreach (var file in Directory.EnumerateFiles(DesktopPath, "*", SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var attributes = File.GetAttributes(file);
                if (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System) || attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                var extension = Path.GetExtension(file);
                if (Skipped.Contains(extension) || _protection.IsProtected(file, settings)) continue;
                var category = Categories.FirstOrDefault(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(extension, StringComparer.OrdinalIgnoreCase)).Key ?? "其他";
                var destination = UniqueDestination(Path.Combine(OrganizedRoot, category, Path.GetFileName(file)), result.Select(m => m.Destination));
                result.Add(new DesktopMove { Source = file, Destination = destination });
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return result;
    }

    public int ApplyPlan(IEnumerable<DesktopMove> plan)
    {
        var journal = new MoveJournal();
        foreach (var move in plan)
        {
            if (!File.Exists(move.Source) || File.Exists(move.Destination)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(move.Destination)!);
            File.Move(move.Source, move.Destination);
            journal.Moves.Add(move);
        }
        if (journal.Moves.Count == 0) return 0;
        Directory.CreateDirectory(_historyDirectory);
        File.WriteAllText(Path.Combine(_historyDirectory, journal.Id + ".json"), JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true }));
        return journal.Moves.Count;
    }

    public int UndoLast()
    {
        if (!Directory.Exists(_historyDirectory)) return 0;
        var latest = Directory.EnumerateFiles(_historyDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (latest is null) return 0;
        var journal = JsonSerializer.Deserialize<MoveJournal>(File.ReadAllText(latest)) ?? new MoveJournal();
        var restored = 0;
        var remaining = new List<DesktopMove>();
        foreach (var move in journal.Moves.AsEnumerable().Reverse())
        {
            try
            {
                if (!File.Exists(move.Destination) || File.Exists(move.Source))
                {
                    if (File.Exists(move.Destination)) remaining.Add(move);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(move.Source)!);
                File.Move(move.Destination, move.Source);
                restored++;
            }
            catch
            {
                remaining.Add(move);
            }
        }
        if (remaining.Count == 0) File.Delete(latest);
        else
        {
            journal.Moves = remaining;
            File.WriteAllText(latest, JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true }));
        }
        return restored;
    }

    private static string UniqueDestination(string path, IEnumerable<string> reserved)
    {
        var reservedSet = reserved.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path) && !reservedSet.Contains(path)) return path;
        var directory = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 1; i < 10000; i++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!File.Exists(candidate) && !reservedSet.Contains(candidate)) return candidate;
        }
        throw new IOException("Could not allocate a unique destination.");
    }
}
