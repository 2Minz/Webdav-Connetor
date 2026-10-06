using System.Diagnostics;

namespace UnionDrive.App;

internal static class RcloneManager
{
    public static ProcessStartInfo StartInfo(string executable, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }

    public static async Task CheckRemoteAsync(string executable, string remote)
    {
        using var process = new Process { StartInfo = StartInfo(executable, ["listremotes"]) };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = DrainAsync(process.StandardError);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync();
            throw new InvalidOperationException("rclone 설정 확인 시간이 초과되었습니다. 암호화된 config 등의 실행 환경을 확인하세요.");
        }
        await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException($"rclone 설정 확인 실패 (종료 코드 {process.ExitCode}).");
        if (!(await output).Split('\n').Any(line => line.Trim() == remote))
            throw new InvalidOperationException($"기존 rclone 설정에 {remote} remote가 없습니다. rclone config로 먼저 설정하세요.");
    }

    public static async Task<string> ObscureAsync(string executable, string password)
    {
        var info = StartInfo(executable, ["obscure", "-"]);
        foreach (var key in info.Environment.Keys.Where(key => key.StartsWith("RCLONE_", StringComparison.OrdinalIgnoreCase)).ToArray())
            info.Environment.Remove(key);
        info.RedirectStandardInput = true;
        using var process = new Process { StartInfo = info };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = DrainAsync(process.StandardError);
        await process.StandardInput.WriteAsync(password);
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync();
            throw new InvalidOperationException("비밀번호 전달 준비 시간이 초과되었습니다.");
        }
        await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException("비밀번호 전달 준비에 실패했습니다.");
        return (await output).Trim();
    }

    public static async Task CheckConnectionAsync(string executable, string remote, IReadOnlyDictionary<string, string> environment)
    {
        var info = StartInfo(executable,
            ["lsf", remote, "--max-depth", "1", "--dirs-only", "--config", "NUL", "--contimeout", "5s", "--timeout", "10s", "--retries", "1", "--low-level-retries", "1"]);
        foreach (var key in info.Environment.Keys.Where(key => key.StartsWith("RCLONE_", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        foreach (var entry in environment) info.Environment[entry.Key] = entry.Value;
        using var process = new Process { StartInfo = info }; process.Start();
        foreach (var key in environment.Keys) info.Environment.Remove(key);
        var output = DrainAsync(process.StandardOutput);
        var errors = ClassifyErrorAsync(process.StandardError);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync(); await output; await errors;
            throw new InvalidOperationException("NAS 응답 시간이 초과되었습니다. 네트워크와 서버 주소를 확인하세요.");
        }
        await output; var errorKind = await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException(errorKind switch
        {
            "auth" => "NAS 인증 또는 접근 권한 확인에 실패했습니다. 계정과 비밀번호를 확인하세요.",
            "tls_trust" => "NAS 인증서의 발급기관을 신뢰할 수 없습니다. DSM 보안 → 인증서에서 유효한 인증서를 WebDAV Server에 지정하거나, 관리자가 확인한 CA 인증서를 Windows에서 신뢰하도록 설정하세요.",
            "tls_name" => "NAS 인증서에 입력한 서버 주소가 포함되어 있지 않습니다. 인증서에 등록된 주소로 연결하거나, DSM에서 해당 주소의 인증서를 WebDAV Server에 지정하세요.",
            "tls_date" => "NAS 인증서가 만료되었거나 아직 유효하지 않습니다. PC 날짜·시간을 확인하고, DSM에서 갱신한 인증서를 WebDAV Server에 지정하세요.",
            "tls_roots" => "Windows의 신뢰할 수 있는 인증기관을 불러오지 못했습니다. Windows 인증서 저장소와 업데이트 상태를 확인하세요.",
            "tls_protocol" => "NAS와 TLS 보안 연결을 협상하지 못했습니다. 접속 포트가 HTTPS WebDAV 포트인지 확인하고, DSM의 WebDAV TLS 설정을 확인하세요.",
            "tls" => "NAS HTTPS 인증서 검증에 실패했습니다. DSM 보안 → 인증서에서 발급 대상 주소·만료일과 WebDAV Server에 지정된 인증서를 확인하세요.",
            "path" => "NAS 서버 또는 공유 폴더 경로를 찾을 수 없습니다. URL을 확인하세요.",
            _ => $"NAS 접근 확인에 실패했습니다 (종료 코드 {process.ExitCode}). 서버 주소와 네트워크를 확인하세요."
        });
    }

    private static async Task<string> ClassifyErrorAsync(StreamReader reader)
    {
        var buffer = new char[2048]; var previous = ""; var kind = ""; var tlsKind = "";
        int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
        {
            // Inspect bounded transient text, return categories only. Never expose raw output.
            var text = previous + new string(buffer, 0, count);
            bool Has(string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);
            // Specific TLS causes take precedence over generic words or HTTP codes in URLs.
            if (Has("certificate signed by unknown authority")) tlsKind = "tls_trust";
            else if (Has("certificate has expired or is not yet valid")) tlsKind = "tls_date";
            else if (Has("certificate is valid for") || Has("cannot validate certificate for") ||
                     Has("certificate is not valid for") || Has("certificate relies on legacy Common Name")) tlsKind = "tls_name";
            else if (Has("failed to load system roots")) tlsKind = "tls_roots";
            else if (tlsKind is "" or "tls" && (Has("tls: handshake failure") || Has("tls: protocol version") ||
                     Has("tls: first record does not look like a TLS handshake"))) tlsKind = "tls_protocol";
            else if (tlsKind.Length == 0 && (Has("x509:") || Has("failed to verify certificate"))) tlsKind = "tls";
            if (Has("401 Unauthorized") || Has("403 Forbidden") || Has("status code 401") || Has("status code 403")) kind = "auth";
            else if (Has("404 Not Found") || Has("status code 404")) kind = "path";
            previous = text.Length > 512 ? text[^512..] : text;
        }
        return tlsKind.Length > 0 ? tlsKind : kind;
    }

    // Drain both pipes to avoid deadlocks. Never persist or display raw rclone output:
    // remote error messages may contain URLs, usernames or other sensitive values.
    public static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[2048];
        while (await reader.ReadAsync(buffer) != 0) { }
    }
}
