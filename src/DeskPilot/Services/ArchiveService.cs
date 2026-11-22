using System.IO;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace DeskPilot.Services;

public sealed class ArchiveService
{
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz" };

    public async Task<(int Succeeded, int Failed, string Log)> ExtractManyAsync(IEnumerable<string> archives, string outputRoot, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var succeeded = 0;
            var failed = 0;
            var lines = new List<string>();
            Directory.CreateDirectory(outputRoot);
            foreach (var archivePath in archives.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!File.Exists(archivePath)) throw new FileNotFoundException("Archive not found", archivePath);
                    if (!SupportedExtensions.Contains(Path.GetExtension(archivePath))) throw new NotSupportedException("Unsupported archive type");
                    var name = Path.GetFileNameWithoutExtension(archivePath);
                    if (name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)) name = Path.GetFileNameWithoutExtension(name);
                    var destination = UniqueDirectory(Path.Combine(outputRoot, name));
                    Directory.CreateDirectory(destination);
                    ExtractOne(archivePath, destination, lines, cancellationToken);
                    succeeded++;
                    lines.Add($"[OK] {archivePath} -> {destination}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    lines.Add($"[FAILED] {archivePath}: {ex.Message}");
                }
            }
            return (succeeded, failed, string.Join(Environment.NewLine, lines));
        }, cancellationToken);
    }

    private static void ExtractOne(string archivePath, string destination, List<string> lines, CancellationToken cancellationToken)
    {
        using var stream = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = ArchiveFactory.Open(stream);
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Key)) continue;
            var relative = entry.Key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"[SKIPPED] Unsafe entry: {entry.Key}");
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
            {
                lines.Add($"[SKIPPED] Existing file: {target}");
                continue;
            }
            entry.WriteToFile(target, new ExtractionOptions { ExtractFullPath = false, Overwrite = false });
        }
    }

    private static string UniqueDirectory(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path)) return path;
        for (var i = 1; i < 10000; i++)
        {
            var candidate = $"{path} ({i})";
            if (!Directory.Exists(candidate) && !File.Exists(candidate)) return candidate;
        }
        throw new IOException("Could not allocate a unique output directory.");
    }
}
