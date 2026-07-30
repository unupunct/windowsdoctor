using System.Diagnostics;

namespace WindowsDoctor.Repair;

internal static class CommandRunner
{
    internal static async Task<(bool success, string output, string error)> RunAsync(
        string exe, string args, int timeoutMs = 60_000, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
            var errorTask  = proc.StandardError.ReadToEndAsync(ct);
            var completed  = await Task.Run(() => proc.WaitForExit(timeoutMs), ct);
            var output = await outputTask;
            var error  = await errorTask;
            if (!completed) { try { proc.Kill(); } catch { } }
            return (proc.ExitCode == 0, output, error);
        }
        catch (Exception ex) { return (false, "", ex.Message); }
    }

    internal static async Task<(bool success, string output, string error)> RunPowerShellAsync(
        string script, int timeoutMs = 120_000, CancellationToken ct = default)
        => await RunAsync("powershell.exe",
            $"-NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
            timeoutMs, ct);

    internal static async Task<(bool success, string output, string error)> RunCmdAsync(
        string command, int timeoutMs = 60_000, CancellationToken ct = default)
        => await RunAsync("cmd.exe", $"/c {command}", timeoutMs, ct);
}
