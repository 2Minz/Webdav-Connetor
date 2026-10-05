using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using UnionDrive.App;

// No NAS, credentials, driver installation or drive mappings are changed by these checks.
// Child mode is a small rclone stand-in that exercises real redirected pipes and HTTP control.
if (args.Length > 0)
{
    if (args[0] == "listremotes")
    {
        Console.WriteLine(Environment.GetEnvironmentVariable("UD_CHECK_SCENARIO") == "missing" ? "Other:" : "Union:");
        return 0;
    }
    if (args[0] == "lsf")
    {
        if (Environment.GetEnvironmentVariable("UD_CHECK_SCENARIO") == "auth")
        { Console.Error.WriteLine("401 Unauthorized test-only-secret"); return 5; }
        return 0;
    }
    if (args[0] != "mount") return 9;
    if (Environment.GetEnvironmentVariable("UD_CHECK_SCENARIO") == "failure") return 23;
    foreach (var expected in new[] { "--network-mode", "full", "30s", "10G", "24h", "--rc-no-auth=false" })
        if (!args.Contains(expected)) return 24;
    if (args.Contains("--rc-pass")) return 25;
    await Console.Out.WriteAsync(new string('a', 100_000));
    await Console.Error.WriteAsync(new string('b', 100_000));
    var address = args[Array.IndexOf(args, "--rc-addr") + 1];
    using var server = new TcpListener(IPAddress.Loopback, int.Parse(address.Split(':')[1]));
    server.Start();
    var expectedAuth = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(
        "UnionDrive:" + Environment.GetEnvironmentVariable("RCLONE_RC_PASS")));
    var marker = Environment.GetEnvironmentVariable("UD_CHECK_PENDING")!;
    File.WriteAllText(marker + ".pid", Environment.ProcessId.ToString());
    while (true)
    {
        using var client = await server.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync();
        string? authorization = null;
        var contentLength = 0;
        while (await reader.ReadLineAsync() is { Length: > 0 } header)
        {
            if (header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) authorization = header[14..].Trim();
            if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) contentLength = int.Parse(header[15..].Trim());
        }
        if (contentLength > 0) await reader.ReadBlockAsync(new char[contentLength], 0, contentLength);
        if (authorization != expectedAuth)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
            continue;
        }
        var endpoint = requestLine!.Split(' ')[1];
        var response = endpoint switch
        {
            "/core/pid" => JsonSerializer.Serialize(new { pid = Environment.ProcessId }),
            "/vfs/stats" => JsonSerializer.Serialize(new { diskCache = new { uploadsInProgress = 0, uploadsQueued = File.Exists(marker) ? 1 : 0, erroredFiles = 0 } }),
            "/core/quit" => "{}",
            _ => "{}"
        };
        var body = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"));
        await stream.WriteAsync(body);
        if (endpoint == "/core/quit") return 0;
    }
}

var executable = Environment.ProcessPath!;
var temp = Path.Combine(Path.GetTempPath(), "UnionDriveChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var markerPath = Path.Combine(temp, "pending");
Environment.SetEnvironmentVariable("UD_CHECK_PENDING", markerPath);
var count = 0;
void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
    count++;
}
async Task ExpectFailure(Func<Task> action, string message)
{
    try { await action(); }
    catch (Exception) { Assert(true, message); return; }
    throw new Exception("Expected failure: " + message);
}
try
{
    Exception? uiError = null;
    var uiThread = new Thread(() =>
    {
        try
        {
            using var form = new MainForm();
            form.CreateControl();
            form.PerformLayout();
            IEnumerable<Control> Walk(Control root) => root.Controls.Cast<Control>().SelectMany(control => new[] { control }.Concat(Walk(control)));
            var controls = Walk(form).OfType<Button>().ToDictionary(button => button.Text);
            Assert(!controls["연결"].Enabled && !controls["연결 해제"].Enabled && !controls["드라이브 열기"].Enabled,
                "WinForms construction and initial button states");
            using var profileForm = new ProfileForm(new(), false);
            profileForm.CreateControl(); profileForm.PerformLayout();
            Assert(profileForm.Controls.Count > 0, "profile editor construction");
            var resourcesBefore = NativeResources.Count();
            for (var iteration = 0; iteration < 30; iteration++)
            { using var repeated = new MainForm(); repeated.CreateControl(); repeated.PerformLayout(); }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var resourcesAfter = NativeResources.Count();
            Assert(resourcesAfter.Gdi <= resourcesBefore.Gdi + 4 && resourcesAfter.User <= resourcesBefore.User + 4,
                $"30 UI disposal cycles reclaim GDI/User handles ({resourcesBefore} -> {resourcesAfter})");
        }
        catch (Exception ex) { uiError = ex; }
    });
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start();
    uiThread.Join();
    if (uiError is not null) throw uiError;
    var configuration = new ConfigurationManager(Path.Combine(temp, "data"));
    var profile = new ConnectionProfile { ServerUrl = "https://nas.example.test:5006/share", Username = "test-user" };
    configuration.Save([profile, profile with { Id = Guid.NewGuid(), DriveLetter = "Y:" }]);
    Assert(configuration.Load().Count == 2, "multiple profile settings round trip");
    Assert(!File.ReadAllText(configuration.ConfigPath).Contains("Password", StringComparison.OrdinalIgnoreCase), "config contains no password field");
    await ExpectFailure(() => { configuration.Save([profile, profile with { Id = Guid.NewGuid() }]); return Task.CompletedTask; }, "duplicate drive letters rejected");
    await ExpectFailure(() => { (profile with { ServerUrl = "http://nas.test" }).Validate(); return Task.CompletedTask; }, "unencrypted server URL rejected");
    var credentialId = Guid.NewGuid();
    try
    {
        CredentialManager.Save(credentialId, "test-only", "temporary-secret-한글");
        Assert(CredentialManager.Read(credentialId) == "temporary-secret-한글", "Windows Credential Manager Unicode round trip");
    }
    finally { CredentialManager.Delete(credentialId); }
    Assert(CredentialManager.Read(credentialId) is null, "owned test credential removed");
    var metadata = Path.Combine(temp, "cache", "vfsMeta", "remote"); Directory.CreateDirectory(metadata);
    var metadataFile = Path.Combine(metadata, "file.txt"); File.WriteAllText(metadataFile, "{\"Dirty\":true}");
    await ExpectFailure(() => { CacheGuard.AssertClean(Path.Combine(temp, "cache")); return Task.CompletedTask; }, "dirty write-back metadata blocks deletion");
    File.WriteAllText(metadataFile, "{\"Dirty\":false}"); CacheGuard.AssertClean(Path.Combine(temp, "cache"));
    Assert(true, "clean cache metadata accepted");
    using (var missingDriver = new MountManager(new(), () => false, _ => false))
    {
        await ExpectFailure(() => missingDriver.ConnectAsync(executable), "missing WinFsp blocks mount");
        Assert(!missingDriver.HasProcess, "missing driver creates no child");
    }
    using (var occupied = new MountManager(new(), () => true, _ => true))
    {
        await ExpectFailure(() => occupied.ConnectAsync(executable), "occupied drive blocks mount");
        Assert(!occupied.HasProcess, "occupied drive creates no child");
    }
    Environment.SetEnvironmentVariable("UD_CHECK_SCENARIO", "missing");
    using (var missingRemote = new MountManager(new(), () => true, _ => false))
    {
        await ExpectFailure(() => missingRemote.ConnectAsync(executable), "missing Union remote blocks mount");
        Assert(!missingRemote.HasProcess, "missing remote creates no mount child");
    }
    Environment.SetEnvironmentVariable("UD_CHECK_SCENARIO", "failure");
    using (var failure = new MountManager(new(), () => true, _ => false))
    {
        await ExpectFailure(() => failure.ConnectAsync(executable), "mount startup failure is reported");
        Assert(failure.State == MountState.Error && !failure.HasProcess, "failed child does not stay connected");
    }
    Environment.SetEnvironmentVariable("UD_CHECK_SCENARIO", null);
    Environment.SetEnvironmentVariable("UD_CHECK_SCENARIO", "auth");
    using (var denied = new MountManager(new() { ManagedProfile = true }, () => true, _ => false))
    {
        await ExpectFailure(() => denied.ConnectAsync(executable, new Dictionary<string, string>()), "NAS authentication preflight rejects bad credentials");
        Assert(!denied.HasProcess, "failed NAS preflight creates no mount child");
    }
    Environment.SetEnvironmentVariable("UD_CHECK_SCENARIO", null);
    var queries = 0;
    using var mount = new MountManager(new(), () => true, _ => ++queries > 2);
    await mount.ConnectAsync(executable);
    Assert(mount.State == MountState.Connected && mount.HasProcess, "mount readiness, authenticated RC and large redirected output");
    await ExpectFailure(() => mount.ConnectAsync(executable), "duplicate connect rejected");
    File.WriteAllText(markerPath, "pending write-back");
    await ExpectFailure(() => mount.DisconnectAsync(), "pending uploads block disconnect");
    Assert(mount.HasProcess, "pending upload child is preserved");
    mount.RequestSystemShutdown();
    Assert(mount.HasProcess, "session shutdown refuses quit with pending uploads");
    File.Delete(markerPath);
    await mount.DisconnectAsync();
    Assert(mount.State == MountState.Disconnected && !mount.HasProcess, "graceful core/quit exits child");
    queries = 0;
    await mount.ConnectAsync(executable);
    Assert(mount.State == MountState.Connected, "reconnect after disconnect");
    using (var child = Process.GetProcessById(int.Parse(File.ReadAllText(markerPath + ".pid"))))
    {
        Assert(child.MainModule!.FileName == executable, "crash test targets only the stand-in executable");
        child.Kill();
        await child.WaitForExitAsync();
    }
    var crashDeadline = DateTime.UtcNow.AddSeconds(5);
    while (mount.State != MountState.Error && DateTime.UtcNow < crashDeadline) await Task.Delay(50);
    Assert(mount.State == MountState.Error && !mount.HasProcess, "unexpected child exit updates error state");
    queries = 0;
    await mount.ConnectAsync(executable);
    Assert(mount.State == MountState.Connected, "reconnect after unexpected exit");
    mount.RequestSystemShutdown();
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (mount.HasProcess && DateTime.UtcNow < deadline) await Task.Delay(50);
    Assert(!mount.HasProcess, "bounded session shutdown requests child exit");
    Assert(MountManager.SafeMessage(new HttpRequestException("password=secret")) is var safe && !safe.Contains("secret"), "raw transport diagnostics are not exposed");
    var firstQueries = 0; var secondQueries = 0;
    using var first = new MountManager(new() { ManagedProfile = true, Remote = "udfirst:", Drive = "X:", DataRoot = configuration.DataRoot }, () => true, _ => ++firstQueries > 2);
    using var second = new MountManager(new() { ManagedProfile = true, Remote = "udsecond:", Drive = "Y:", DataRoot = configuration.DataRoot }, () => true, _ => ++secondQueries > 2);
    await Task.WhenAll(first.ConnectAsync(executable, new Dictionary<string, string>()), second.ConnectAsync(executable, new Dictionary<string, string>()));
    Assert(first.HasProcess && second.HasProcess, "two independent managed mounts connected concurrently");
    var leases = Directory.GetFiles(Path.Combine(configuration.DataRoot, "Sessions"));
    Assert(leases.Length == 2 && leases.All(file => !File.ReadAllText(file).Contains("password", StringComparison.OrdinalIgnoreCase)), "owned process leases contain no RC password");
    await first.DisconnectAsync();
    Assert(!first.HasProcess && second.HasProcess, "disconnecting one mount preserves the other");
    await second.DisconnectAsync();
    Assert(!second.HasProcess, "second managed mount shuts down independently");
    for (var cycle = 0; cycle < 10; cycle++)
    {
        firstQueries = 0;
        await first.ConnectAsync(executable, new Dictionary<string, string>());
        await first.DisconnectAsync();
    }
    Assert(Directory.GetFiles(Path.Combine(configuration.DataRoot, "Sessions")).Length == 0, "10 managed reconnect cycles reclaim ownership leases");
    var recoveryInfo = RcloneManager.StartInfo(executable, new MountOptions().Arguments().Concat(new[] { "--rc-no-auth=false", "--rc-addr", "127.0.0.1:0" }));
    using var reserve = new TcpListener(IPAddress.Loopback, 0); reserve.Start();
    var recoveryPort = ((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
    recoveryInfo.ArgumentList[^1] = $"127.0.0.1:{recoveryPort}";
    recoveryInfo.Environment["RCLONE_RC_PASS"] = "test-recovery-secret";
    using var recoveryChild = new Process { StartInfo = recoveryInfo }; recoveryChild.Start();
    var recoveryDrains = Task.WhenAll(RcloneManager.DrainAsync(recoveryChild.StandardOutput), RcloneManager.DrainAsync(recoveryChild.StandardError));
    var recoveryLease = MountLease.Register(configuration.DataRoot, recoveryChild, recoveryPort, "test-recovery-secret");
    await Task.Delay(500);
    await MountLease.RecoverAsync(configuration.DataRoot, executable); await recoveryDrains;
    Assert(recoveryChild.HasExited && CredentialManager.Read(recoveryLease.Id) is null, "orphaned owned child recovered and RC credential removed");
    var officialRclone = Path.GetFullPath("packaging/vendor/rclone/rclone-v1.75.1-windows-amd64/rclone.exe");
    if (File.Exists(officialRclone)) await RealRcloneChecks.RunAsync(officialRclone, Assert);
    Console.WriteLine($"{count} checks passed.");
    return 0;
}
finally
{
    Environment.SetEnvironmentVariable("UD_CHECK_SCENARIO", null);
    Environment.SetEnvironmentVariable("UD_CHECK_PENDING", null);
    File.Delete(markerPath);
    File.Delete(markerPath + ".pid");
    Directory.Delete(temp, true);
}
