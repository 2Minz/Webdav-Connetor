using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnionDrive.App;

internal enum MountState { Disconnected, Connecting, Connected, Error }

internal sealed class MountManager : IDisposable
{
    private readonly MountOptions options;
    private readonly Func<bool> hasWinFsp;
    private readonly Func<string, bool> driveExists;
    private Process? process;
    private HttpClient? control;
    private Task? drain;
    private bool stopping;
    private MountLease? lease;
    private Task? exitTask;
    public MountState State { get; private set; }
    public bool HasProcess => process is { HasExited: false };
    public event Action<string>? Changed;

    public MountManager(MountOptions options, Func<bool>? hasWinFsp = null, Func<string, bool>? driveExists = null)
    {
        this.options = options;
        this.hasWinFsp = hasWinFsp ?? EnvironmentDetector.HasWinFsp;
        this.driveExists = driveExists ?? EnvironmentDetector.DriveExists;
    }

    private void Report(MountState state, string message)
    {
        State = state;
        Changed?.Invoke(message);
    }

    public async Task ConnectAsync(string executable, IReadOnlyDictionary<string, string>? environment = null)
    {
        if (HasProcess) throw new InvalidOperationException("관리 중인 rclone을 먼저 연결 해제하세요.");
        Report(MountState.Connecting, "실행 환경을 확인하고 있습니다.");
        try
        {
            if (!File.Exists(executable)) throw new InvalidOperationException("rclone.exe를 선택하세요.");
            if (!hasWinFsp()) throw new InvalidOperationException("WinFsp 설치를 확인할 수 없습니다. WinFsp를 설치한 후 다시 시도하세요.");
            if (driveExists(options.Drive)) throw new InvalidOperationException($"{options.Drive}가 이미 사용 중입니다. 기존 드라이브를 먼저 해제하세요.");
            if (options.ManagedProfile && environment is null) throw new InvalidOperationException("드라이브 인증 설정이 없습니다.");
            if (options.ManagedProfile) await RcloneManager.CheckConnectionAsync(executable, options.Remote, environment!);
            else await RcloneManager.CheckRemoteAsync(executable, options.Remote);
            // Recheck after the asynchronous preflight; another application may have claimed it.
            if (driveExists(options.Drive)) throw new InvalidOperationException($"{options.Drive}가 이미 사용 중입니다.");
            process?.Dispose();
            control?.Dispose();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var info = RcloneManager.StartInfo(executable, options.Arguments().Concat(
                new[] { "--rc", "--rc-addr", $"127.0.0.1:{port}", "--rc-user", "UnionDrive", "--rc-no-auth=false" }));
            if (options.ManagedProfile)
            {
                foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("RCLONE_", StringComparison.OrdinalIgnoreCase)).ToArray())
                    info.Environment.Remove(key);
            }
            if (environment is not null)
                foreach (var entry in environment) info.Environment[entry.Key] = entry.Value;
            // Keep the transient RC secret out of command-line arguments and files.
            info.Environment["RCLONE_RC_PASS"] = password;
            control = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(2)
            };
            control.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"UnionDrive:{password}")));
            var mounted = new Process { StartInfo = info };
            process = mounted;
            try { mounted.Start(); }
            catch { process = null; mounted.Dispose(); throw; }
            foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("RCLONE_", StringComparison.OrdinalIgnoreCase)).ToArray())
                info.Environment.Remove(key);
            drain = Task.WhenAll(RcloneManager.DrainAsync(mounted.StandardOutput), RcloneManager.DrainAsync(mounted.StandardError));
            exitTask = ObserveExitAsync(mounted);
            if (options.ManagedProfile && options.DataRoot is not null)
                lease = MountLease.Register(options.DataRoot, mounted, port, password);

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (mounted.HasExited) throw new InvalidOperationException($"rclone 마운트 실패 (종료 코드 {mounted.ExitCode}). remote 설정과 NAS 접근 상태를 확인하세요.");
                try
                {
                    await CheckIdentityAsync();
                    using var stats = await CallAsync("vfs/stats");
                    if (!mounted.HasExited && driveExists(options.Drive))
                    {
                        if (options.ManagedProfile)
                        {
                            try { ExplorerDriveAppearance.Apply(options.Drive, options.Volume); }
                            catch { Changed?.Invoke("탐색기 아이콘/이름을 적용하지 못했습니다."); }
                        }
                        Report(MountState.Connected, $"{options.Volume} ({options.Drive}) 마운트됨 · NAS 접근은 드라이브 열기로 확인하세요.");
                        return;
                    }
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                await Task.Delay(300);
            }
            throw new InvalidOperationException("마운트 확인 시간이 초과되었습니다. 관리 중인 rclone은 연결 해제 버튼으로 종료하세요.");
        }
        catch (Exception ex)
        {
            Report(MountState.Error, SafeMessage(ex));
            throw;
        }
    }

    private async Task ObserveExitAsync(Process mounted)
    {
        await mounted.WaitForExitAsync();
        if (ReferenceEquals(process, mounted) && options.ManagedProfile)
        { try { ExplorerDriveAppearance.Restore(options.Drive); } catch { } }
        if (ReferenceEquals(process, mounted) && lease is not null && options.DataRoot is not null)
        {
            try { lease.Remove(options.DataRoot); lease = null; } catch (Exception) { }
        }
        if (ReferenceEquals(process, mounted) && !stopping)
            Report(MountState.Error, $"rclone이 종료되었습니다 (종료 코드 {mounted.ExitCode}). 다시 연결할 수 있습니다.");
    }

    private async Task<JsonDocument> CallAsync(string endpoint)
    {
        using var response = await control!.PostAsync(endpoint, new StringContent("{}", Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task CheckIdentityAsync()
    {
        using var identity = await CallAsync("core/pid");
        if (identity.RootElement.GetProperty("pid").GetInt32() != process!.Id)
            throw new InvalidOperationException("rclone 제어 프로세스가 일치하지 않습니다.");
    }

    public async Task DisconnectAsync()
    {
        if (!HasProcess)
        {
            Report(MountState.Disconnected, "연결 끊김");
            return;
        }
        stopping = true;
        Changed?.Invoke("연결 해제 준비 중 · 열어 둔 파일을 닫아 주세요.");
        try
        {
            await CheckIdentityAsync();
            // Do not silently discard pending write-back data or kill an owned mount.
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (true)
            {
                using var stats = await CallAsync("vfs/stats");
                var cache = stats.RootElement.GetProperty("diskCache");
                if (cache.GetProperty("uploadsInProgress").GetInt32() == 0 &&
                    cache.GetProperty("uploadsQueued").GetInt32() == 0 &&
                    cache.GetProperty("erroredFiles").GetInt32() == 0) break;
                if (DateTime.UtcNow >= deadline)
                    throw new InvalidOperationException("NAS로 전송 중이거나 전송에 실패한 캐시 파일이 있습니다. 파일을 닫고 전송 완료 후 다시 연결 해제하세요.");
                await Task.Delay(500);
            }
            if (options.CacheDirectory is not null) CacheGuard.AssertClean(options.CacheDirectory);
            try { using var response = await CallAsync("core/quit"); }
            catch (HttpRequestException) { /* The process may close HTTP before replying. */ }
            catch (TaskCanceledException) { }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process!.WaitForExitAsync(timeout.Token);
            if (drain is not null) await drain;
            if (exitTask is not null) await exitTask;
            process.Dispose(); process = null; control?.Dispose(); control = null; drain = null;
            Report(MountState.Disconnected, "연결 끊김 · rclone 종료 완료");
        }
        catch (Exception ex)
        {
            Report(MountState.Error, HasProcess ? SafeMessage(ex) + " 연결 해제를 다시 시도하세요." : "rclone이 종료되었습니다.");
            if (HasProcess) throw;
        }
        finally { stopping = false; }
    }

    public static string SafeMessage(Exception exception) => exception switch
    {
        InvalidOperationException => exception.Message,
        HttpRequestException => "rclone 로컬 제어 연결에 실패했습니다.",
        OperationCanceledException => "rclone 응답 시간이 초과되었습니다.",
        _ => "작업에 실패했습니다. rclone 실행 경로와 설치 환경을 확인하세요."
    };

    public void Dispose()
    {
        control?.Dispose();
        process?.Dispose();
    }

    public void RequestSystemShutdown(TimeSpan? budget = null)
    {
        if (!HasProcess || control is null) return;
        // Synchronous, tightly bounded best effort before the message loop is destroyed.
        // Refuse to request quit when pending writes are visible. Windows may still end the session.
        using var timeout = new CancellationTokenSource(budget ?? TimeSpan.FromSeconds(3));
        try
        {
            using var identity = Send("core/pid");
            if (identity.RootElement.GetProperty("pid").GetInt32() != process!.Id) return;
            using var stats = Send("vfs/stats");
            var cache = stats.RootElement.GetProperty("diskCache");
            if (cache.GetProperty("uploadsInProgress").GetInt32() != 0 ||
                cache.GetProperty("uploadsQueued").GetInt32() != 0 ||
                cache.GetProperty("erroredFiles").GetInt32() != 0) return;
            using var result = Send("core/quit");
        }
        catch (Exception) { /* Windows shutdown must continue, even if the mount is unavailable. */ }

        JsonDocument Send(string endpoint)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            using var response = control.Send(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            using var stream = response.Content.ReadAsStream(timeout.Token);
            return JsonDocument.Parse(stream);
        }
    }
}
