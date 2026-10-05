using System.Security.Principal;

namespace UnionDrive.App;

internal static class AppLifetime
{
    private static readonly string Scope = GetScope();
    private static string GetScope()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? Environment.UserName;
    }
    public static string MutexName => @"Local\UnionDrive-" + Scope;
    public static string ExitEventName => @"Local\UnionDrive-Exit-" + Scope;

    public static int Cleanup(bool removeData)
    {
        using var instance = new Mutex(false, MutexName);
        var owned = false;
        try
        {
            try { owned = instance.WaitOne(0); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned)
            {
                try { using var signal = EventWaitHandle.OpenExisting(ExitEventName); signal.Set(); }
                catch (WaitHandleCannotBeOpenedException) { return 10; }
                try { owned = instance.WaitOne(TimeSpan.FromSeconds(60)); }
                catch (AbandonedMutexException) { owned = true; }
                if (!owned) return 10;
            }
            var configuration = new ConfigurationManager();
            var executable = EnvironmentDetector.FindRclone();
            if (executable is not null) MountLease.RecoverAsync(configuration.DataRoot, executable).GetAwaiter().GetResult();
            else if (Directory.Exists(Path.Combine(configuration.DataRoot, "Sessions"))) return 11;
            CacheGuard.AssertClean(Path.Combine(configuration.DataRoot, "Cache"));
            ExplorerDriveAppearance.Restore();
            if (!removeData) return 0;
            // Inspect the entire tree before deleting it, never follow junctions/symlinks.
            foreach (var _ in CacheGuard.Files(configuration.DataRoot)) { }
            StartupManager.Set(false); CredentialManager.DeleteAll();
            if (Directory.Exists(configuration.DataRoot)) Directory.Delete(configuration.DataRoot, true);
            return 0;
        }
        catch { return 11; }
        finally { if (owned) instance.ReleaseMutex(); }
    }
}
