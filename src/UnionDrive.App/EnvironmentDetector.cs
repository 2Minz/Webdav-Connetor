using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;

namespace UnionDrive.App;

internal static class EnvironmentDetector
{
    public static string? FindRclone()
    {
        var directories = new[] { Path.Combine(AppContext.BaseDirectory, "runtime"), AppContext.BaseDirectory }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (var directory in directories)
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), "rclone.exe");
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    public static bool HasWinFsp()
    {
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = machine.OpenSubKey(@"SOFTWARE\WinFsp");
            if (key?.GetValue("InstallDir") is string path && Directory.Exists(Path.Combine(path, "bin")) &&
                Directory.GetFiles(Path.Combine(path, "bin"), "winfsp-*.dll").Length > 0) return true;
        }
        return false;
    }

    // Query the DOS device table without making a blocking request to a network drive.
    public static bool DriveExists(string drive) => QueryDosDevice(drive, new StringBuilder(4096), 4096) != 0;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDevice(string device, StringBuilder target, int length);
}
