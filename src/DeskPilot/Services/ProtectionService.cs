using System.IO;
using DeskPilot.Models;

namespace DeskPilot.Services;

public sealed class ProtectionService
{
    public bool IsProtected(string path, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        foreach (var entry in settings.ProtectedPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            string protectedPath;
            try { protectedPath = Path.GetFullPath(entry).TrimEnd(Path.DirectorySeparatorChar); }
            catch { continue; }
            if (full.Equals(protectedPath, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public bool IsInside(string candidate, string root)
    {
        var full = Path.GetFullPath(candidate);
        var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
    }
}
