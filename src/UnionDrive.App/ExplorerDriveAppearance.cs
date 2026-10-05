using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;

namespace UnionDrive.App;

internal static class ExplorerDriveAppearance
{
    private const string Records = @"Software\UnionDrive\DriveAppearance";
    private const string Drives = @"Software\Classes\Applications\Explorer.exe\Drives\";
    private const string MountPoints = @"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\";

    public static void Apply(string drive, string label)
    {
        var letter = drive[..1];
        var icon = $"\"{Environment.ProcessPath}\",0";
        Set(Drives + letter + @"\DefaultIcon", "", icon, drive);
        Set(Drives + letter + @"\DefaultLabel", "", label, drive);
        var remote = new StringBuilder(1024); var length = remote.Capacity;
        if (WNetGetConnection(drive, remote, ref length) == 0)
        {
            var path = MountPoints + remote.ToString().Replace('\\', '#');
            Set(path, "_LabelFromReg", label, drive);
            Set(path + @"\DefaultIcon", "", icon, drive);
        }
        Notify(drive);
    }

    private static void Set(string path, string name, string value, string drive)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path + "|" + name)));
        var created = new List<string>();
        for (var candidate = path; Allowed(candidate) || IsDriveRoot(candidate); candidate = candidate[..candidate.LastIndexOf('\\')])
        {
            using var existing = Registry.CurrentUser.OpenSubKey(candidate);
            if (existing is null) created.Add(candidate);
        }
        using var target = Registry.CurrentUser.CreateSubKey(path);
        using var record = Registry.CurrentUser.CreateSubKey(Records + "\\" + id);
        if (record.GetValue("Path") is null)
        {
            var previous = target.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (previous is not null && previous is not string) return;
            record.SetValue("Present", previous is null ? 0 : 1);
            record.SetValue("Previous", previous as string ?? "");
            record.SetValue("Kind", previous is null ? (int)RegistryValueKind.String : (int)target.GetValueKind(name));
            record.SetValue("Name", name); record.SetValue("Drive", drive);
            record.SetValue("Created", created.ToArray(), RegistryValueKind.MultiString);
            record.SetValue("Applied", value); record.SetValue("Path", path);
        }
        var owners = record.GetValue("Owners") as string[] ?? [];
        record.SetValue("Owners", owners.Append(drive).Distinct().ToArray(), RegistryValueKind.MultiString);
        record.SetValue("Applied", value);
        target.SetValue(name, value, RegistryValueKind.String);
    }

    public static void Restore(string? drive = null)
    {
        using var root = Registry.CurrentUser.OpenSubKey(Records, true);
        if (root is null) return;
        var createdKeys = new List<string>();
        foreach (var id in root.GetSubKeyNames())
        {
            using (var record = root.OpenSubKey(id, true))
            {
                if (record is null) continue;
                var owners = record.GetValue("Owners") as string[] ?? [];
                if (drive is not null)
                {
                    if (!owners.Contains(drive)) continue;
                    var remaining = owners.Where(owner => owner != drive).ToArray();
                    if (remaining.Length > 0) { record.SetValue("Owners", remaining, RegistryValueKind.MultiString); continue; }
                }
                if (record.GetValue("Path") is not string path || !Allowed(path)) continue;
                var name = record.GetValue("Name") as string ?? "";
                if (name is not "" and not "_LabelFromReg") continue;
                using (var target = Registry.CurrentUser.OpenSubKey(path, true))
                {
                    // A later change made by another program/user is preserved.
                    if (target is not null && Equals(target.GetValue(name), record.GetValue("Applied")))
                    {
                        if (Equals(record.GetValue("Present"), 1))
                            target.SetValue(name, (string)record.GetValue("Previous")!, (RegistryValueKind)(int)record.GetValue("Kind")!);
                        else target.DeleteValue(name, false);
                    }
                }
                createdKeys.AddRange(record.GetValue("Created") as string[] ?? []);
                if (record.GetValue("Drive") is string letter) Notify(letter);
            }
            root.DeleteSubKey(id, false);
        }
        // Shared parents are removed only after every owned value has been restored.
        foreach (var created in createdKeys.Distinct().OrderByDescending(path => path.Length)) RemoveEmpty(created);
        if (root.SubKeyCount == 0) Registry.CurrentUser.DeleteSubKey(Records, false);
    }

    private static bool Allowed(string path)
    {
        if (path.StartsWith(Drives, StringComparison.Ordinal))
        {
            var suffix = path[Drives.Length..];
            return suffix.Length > 2 && suffix[0] is >= 'D' and <= 'Z' &&
                (suffix[1..] == @"\DefaultIcon" || suffix[1..] == @"\DefaultLabel");
        }
        if (!path.StartsWith(MountPoints + "##", StringComparison.Ordinal)) return false;
        var parts = path[MountPoints.Length..].Split('\\');
        return parts.Length == 1 || (parts.Length == 2 && parts[1] == "DefaultIcon");
    }
    private static bool IsDriveRoot(string path) => path.Length == Drives.Length + 1 &&
        path.StartsWith(Drives, StringComparison.Ordinal) && path[^1] is >= 'D' and <= 'Z';
    private static void RemoveEmpty(string path)
    {
        if (!Allowed(path) && !IsDriveRoot(path)) return;
        using (var key = Registry.CurrentUser.OpenSubKey(path))
            if (key is null || key.ValueCount != 0 || key.SubKeyCount != 0) return;
        Registry.CurrentUser.DeleteSubKey(path, false);
    }
    private static void Notify(string drive) => SHChangeNotify(0x00002000, 0x0005, drive + "\\", IntPtr.Zero);
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnection(string local, StringBuilder remote, ref int length);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint eventId, uint flags, string item, IntPtr other);
}
