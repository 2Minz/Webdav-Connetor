using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace UnionDrive.App;

internal sealed record MountLease(Guid Id, int ProcessId, long StartTicks, int Port)
{
    public static MountLease Register(string root, Process process, int port, string secret)
    {
        var lease = new MountLease(Guid.NewGuid(), process.Id, process.StartTime.ToUniversalTime().Ticks, port);
        CredentialManager.Save(lease.Id, "UnionDrive RC", secret);
        var directory = Path.Combine(root, "Sessions"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, lease.Id.ToString("N") + ".json"), JsonSerializer.Serialize(lease));
        return lease;
    }
    public void Remove(string root)
    {
        CredentialManager.Delete(Id);
        File.Delete(Path.Combine(root, "Sessions", Id.ToString("N") + ".json"));
    }

    public static async Task RecoverAsync(string root, string executable)
    {
        var directory = Path.Combine(root, "Sessions");
        foreach (var file in CacheGuard.Files(directory).ToArray())
        {
            var lease = JsonSerializer.Deserialize<MountLease>(File.ReadAllText(file))
                ?? throw new InvalidOperationException("이전 마운트 정보를 확인할 수 없습니다.");
            if (lease.Id.ToString("N") + ".json" != Path.GetFileName(file) || lease.Port is < 1 or > 65535)
                throw new InvalidOperationException("이전 마운트 정보가 올바르지 않습니다.");
            Process? process;
            try { process = Process.GetProcessById(lease.ProcessId); }
            catch (ArgumentException) { lease.Remove(root); continue; }
            using (process)
            {
                if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != lease.StartTicks)
                { lease.Remove(root); continue; }
                if (!string.Equals(process.MainModule?.FileName, Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("이전 마운트 프로세스의 소유권을 확인할 수 없습니다.");
                var password = CredentialManager.Read(lease.Id) ?? throw new InvalidOperationException("이전 마운트의 종료 인증정보가 없습니다.");
                using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{lease.Port}/"), Timeout = TimeSpan.FromSeconds(2) };
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("UnionDrive:" + password)));
                async Task<JsonDocument> Call(string endpoint)
                {
                    using var response = await http.PostAsync(endpoint, new StringContent("{}", Encoding.UTF8, "application/json"));
                    response.EnsureSuccessStatusCode(); return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                }
                using var identity = await Call("core/pid");
                if (identity.RootElement.GetProperty("pid").GetInt32() != lease.ProcessId)
                    throw new InvalidOperationException("이전 마운트 프로세스가 일치하지 않습니다.");
                using var stats = await Call("vfs/stats"); var cache = stats.RootElement.GetProperty("diskCache");
                if (cache.GetProperty("uploadsInProgress").GetInt32() != 0 || cache.GetProperty("uploadsQueued").GetInt32() != 0 || cache.GetProperty("erroredFiles").GetInt32() != 0)
                    throw new InvalidOperationException("이전 마운트에서 파일을 전송 중입니다. 전송 완료 후 다시 실행하세요.");
                try { using var result = await Call("core/quit"); }
                catch (HttpRequestException) { }
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(timeout.Token); lease.Remove(root);
            }
        }
    }
}
