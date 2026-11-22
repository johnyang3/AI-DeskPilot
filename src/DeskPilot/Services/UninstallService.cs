using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskPilot.Services;

public static class UninstallService
{
    public static void Start(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) throw new InvalidOperationException("No registered uninstaller is available.");
        var parts = SplitCommandLine(commandLine);
        if (parts.Length == 0) throw new InvalidOperationException("Invalid uninstaller command.");
        var info = new ProcessStartInfo(parts[0]) { UseShellExecute = true };
        foreach (var argument in parts.Skip(1)) info.ArgumentList.Add(argument);
        Process.Start(info);
    }

    private static string[] SplitCommandLine(string commandLine)
    {
        var argv = CommandLineToArgvW(commandLine, out var count);
        if (argv == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            return Enumerable.Range(0, count).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? string.Empty).ToArray();
        }
        finally { LocalFree(argv); }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
