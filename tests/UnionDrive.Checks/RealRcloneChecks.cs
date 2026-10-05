using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnionDrive.App;

internal static class RealRcloneChecks
{
    public static async Task RunAsync(string executable, Action<bool, string> assert)
    {
        var obscure = await RcloneManager.ObscureAsync(executable, "test-only-한글-password");
        assert(obscure.Length > 20 && !obscure.Contains("password"), "official rclone stdin password preparation");
        var remote = "ud" + Guid.NewGuid().ToString("N");
        var info = RcloneManager.StartInfo(executable, ["listremotes", "--config", "NUL"]);
        foreach (var key in info.Environment.Keys.Where(key => key.StartsWith("RCLONE_", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        var prefix = "RCLONE_CONFIG_" + remote.ToUpperInvariant() + "_";
        info.Environment[prefix + "TYPE"] = "webdav"; info.Environment[prefix + "URL"] = "https://nas.example.test/";
        info.Environment[prefix + "USER"] = "test-only"; info.Environment[prefix + "PASS"] = obscure;
        using (var list = new Process { StartInfo = info })
        {
            list.Start(); var output = list.StandardOutput.ReadToEndAsync(); var errors = RcloneManager.DrainAsync(list.StandardError);
            await list.WaitForExitAsync(); await errors;
            assert(list.ExitCode == 0 && (await output).Trim() == remote + ":", "official rclone recognizes isolated environment WebDAV remote");
        }
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var rcInfo = RcloneManager.StartInfo(executable, ["rcd", "--rc-addr", $"127.0.0.1:{port}", "--rc-user", "UnionDrive", "--rc-no-auth=false", "--config", "NUL"]);
        rcInfo.Environment["RCLONE_RC_PASS"] = password;
        using var rc = new Process { StartInfo = rcInfo }; rc.Start();
        var drains = Task.WhenAll(RcloneManager.DrainAsync(rc.StandardOutput), RcloneManager.DrainAsync(rc.StandardError));
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
        { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(1) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("UnionDrive:" + password)));
        try
        {
            var ready = false; var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && !rc.HasExited)
            {
                try
                {
                    using var response = await http.PostAsync("core/pid", new StringContent("{}", Encoding.UTF8, "application/json"));
                    response.EnsureSuccessStatusCode(); using var identity = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    ready = identity.RootElement.GetProperty("pid").GetInt32() == rc.Id; if (ready) break;
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                await Task.Delay(100);
            }
            assert(ready, "official rclone RC password environment and process identity");
            try { using var quit = await http.PostAsync("core/quit", new StringContent("{}", Encoding.UTF8, "application/json")); }
            catch (HttpRequestException) { }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await rc.WaitForExitAsync(timeout.Token); await drains;
            assert(rc.ExitCode == 0, "official rclone core/quit exits without forced termination");
        }
        finally
        {
            // This is a dedicated empty RC test server, never a mount or NAS process.
            if (!rc.HasExited) { rc.Kill(); await rc.WaitForExitAsync(); }
            await drains;
        }
    }
}
