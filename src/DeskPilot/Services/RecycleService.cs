using System.IO;
using Microsoft.VisualBasic.FileIO;
using DeskPilot.Models;

namespace DeskPilot.Services;

public sealed class RecycleService
{
    private readonly ProtectionService _protection;
    public RecycleService(ProtectionService protection) => _protection = protection;

    public void Recycle(string path, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.");
        var full = Path.GetFullPath(path);
        if (_protection.IsProtected(full, settings)) throw new InvalidOperationException(Loc.F("Protected", full));
        if (IsCriticalRoot(full)) throw new InvalidOperationException("Critical system or user roots cannot be removed.");
        if (File.Exists(full))
        {
            FileSystem.DeleteFile(full, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            return;
        }
        if (Directory.Exists(full))
        {
            FileSystem.DeleteDirectory(full, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            return;
        }
        throw new FileNotFoundException("Path does not exist", full);
    }

    private static bool IsCriticalRoot(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var roots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };
        roots.AddRange(DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.RootDirectory.FullName));
        return roots.Where(r => !string.IsNullOrWhiteSpace(r)).Any(r => full.Equals(r.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
    }
}
