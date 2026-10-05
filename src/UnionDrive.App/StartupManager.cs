using Microsoft.Win32;

namespace UnionDrive.App;

internal static class StartupManager
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);
            return key?.GetValue("Webdav Connetor") is string || key?.GetValue("UnionDrive") is string;
        }
    }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.DeleteValue("UnionDrive", throwOnMissingValue: false);
        if (enabled) key.SetValue("Webdav Connetor", $"\"{Environment.ProcessPath}\" --startup", RegistryValueKind.String);
        else key.DeleteValue("Webdav Connetor", throwOnMissingValue: false);
    }
}
