namespace UnionDrive.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--uninstall-cleanup")) return AppLifetime.Cleanup(removeData: true);
        if (args.Contains("--prepare-update")) return AppLifetime.Cleanup(removeData: false);
        using var instance = new Mutex(true, AppLifetime.MutexName, out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Webdav Connetor가 이미 실행 중입니다.", "Webdav Connetor");
            return 0;
        }
        ApplicationConfiguration.Initialize();
        using var form = new MainForm(args.Contains("--startup"));
        using var exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, AppLifetime.ExitEventName);
        var registration = ThreadPool.RegisterWaitForSingleObject(exitSignal, (_, _) =>
        {
            if (form.IsHandleCreated && !form.IsDisposed)
            {
                try { form.BeginInvoke(async () => await form.RequestExitAsync()); }
                catch (InvalidOperationException) { }
            }
        }, null, Timeout.Infinite, executeOnlyOnce: false);
        try { Application.Run(form); }
        finally { registration.Unregister(null); }
        return 0;
    }
}
