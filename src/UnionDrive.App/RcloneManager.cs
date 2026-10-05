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
            "tls" => "NAS HTTPS 인증서를 확인할 수 없습니다. 서버 인증서를 확인하세요.",
            "path" => "NAS 서버 또는 공유 폴더 경로를 찾을 수 없습니다. URL을 확인하세요.",
            _ => $"NAS 접근 확인에 실패했습니다 (종료 코드 {process.ExitCode}). 서버 주소와 네트워크를 확인하세요."
        });
    }

    private static async Task<string> ClassifyErrorAsync(StreamReader reader)
    {
        var buffer = new char[2048]; var previous = ""; var kind = "";
        int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
        {
            // Inspect bounded transient text, return categories only. Never expose raw output.
            var text = previous + new string(buffer, 0, count);
            if (text.Contains("401") || text.Contains("403") || text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)) kind = "auth";
            else if (text.Contains("x509", StringComparison.OrdinalIgnoreCase) || text.Contains("certificate", StringComparison.OrdinalIgnoreCase)) kind = "tls";
            else if (text.Contains("404")) kind = "path";
            previous = text.Length > 64 ? text[^64..] : text;
        }
        return kind;
    }

    // Drain both pipes to avoid deadlocks. Never persist or display raw rclone output:
    // remote error messages may contain URLs, usernames or other sensitive values.
    public static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[2048];
        while (await reader.ReadAsync(buffer) != 0) { }
    }
}
