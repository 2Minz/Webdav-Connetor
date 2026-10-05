namespace UnionDrive.App;

internal sealed class DriveSession : IDisposable
{
    private readonly string dataRoot;
    private readonly Logger logger;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Action<string> stateHandler;
    private bool desired;
    private bool retrying;
    public ConnectionProfile Profile { get; }
    public MountManager Mount { get; }
    public bool Busy { get; private set; }
    public bool Failed { get; private set; }
    public string Message { get; private set; } = "연결 끊김";
    public event Action? Changed;

    public DriveSession(ConnectionProfile profile, string root, Logger logger)
    {
        Profile = profile; dataRoot = root; this.logger = logger;
        Mount = new MountManager(profile.MountOptions(root));
        stateHandler = message =>
        {
            Message = message; Changed?.Invoke();
            if (desired && !Busy && !Mount.HasProcess && Mount.State == MountState.Error) StartRetry();
        };
        Mount.Changed += stateHandler;
    }

    public async Task ConnectAsync()
    {
        if (Busy || Mount.HasProcess || lifetime.IsCancellationRequested) return;
        desired = true; Busy = true; Failed = false; Changed?.Invoke();
        try
        {
            var executable = EnvironmentDetector.FindRclone() ?? throw new InvalidOperationException("포함된 rclone.exe를 찾을 수 없습니다. 프로그램을 다시 설치하세요.");
            var password = CredentialManager.Read(Profile.Id) ?? throw new InvalidOperationException("저장된 비밀번호가 없습니다. 드라이브 설정에서 입력하세요.");
            var obscure = await RcloneManager.ObscureAsync(executable, password);
            var prefix = $"RCLONE_CONFIG_UD{Profile.Id:N}_".ToUpperInvariant();
            var environment = new Dictionary<string, string>
            {
                [prefix + "TYPE"] = "webdav", [prefix + "URL"] = Profile.ServerUrl,
                [prefix + "VENDOR"] = "other", [prefix + "USER"] = Profile.Username, [prefix + "PASS"] = obscure
            };
            try
            {
                Directory.CreateDirectory(Path.Combine(dataRoot, "Cache", Profile.Id.ToString("N")));
                logger.Write("MountStarting", Profile.Id);
                await Mount.ConnectAsync(executable, environment); logger.Write("MountReady", Profile.Id);
            }
            finally { environment.Clear(); }
        }
        catch (Exception ex) { Failed = true; Message = MountManager.SafeMessage(ex); logger.Write("MountFailed", Profile.Id); }
        finally { Busy = false; Changed?.Invoke(); }
    }

    public async Task DisconnectAsync()
    {
        desired = false;
        if (Busy) throw new InvalidOperationException("드라이브 작업이 끝난 후 다시 시도하세요.");
        Busy = true; Changed?.Invoke();
        try { await Mount.DisconnectAsync(); Failed = false; logger.Write("MountStopped", Profile.Id); }
        catch (Exception ex) { Message = MountManager.SafeMessage(ex); throw; }
        finally { Busy = false; Changed?.Invoke(); }
    }

    public void NetworkRestored()
    { if (desired && !Mount.HasProcess && !Busy) StartRetry(); }
    private async void StartRetry()
    {
        if (retrying || lifetime.IsCancellationRequested) return;
        retrying = true;
        try
        {
            foreach (var delay in new[] { 5, 10, 30, 60 })
            {
                await Task.Delay(TimeSpan.FromSeconds(delay), lifetime.Token);
                if (!desired || Mount.HasProcess) return;
                await ConnectAsync(); if (Mount.HasProcess) return;
            }
        }
        catch (OperationCanceledException) { }
        finally { retrying = false; }
    }
    public void StopRetrying() => desired = false;
    public void Dispose()
    {
        desired = false; lifetime.Cancel(); lifetime.Dispose();
        Mount.Changed -= stateHandler; Mount.Dispose(); Changed = null;
    }
}
