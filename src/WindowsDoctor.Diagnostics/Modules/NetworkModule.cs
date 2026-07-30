using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Diagnostics.Modules;

public class NetworkModule : DiagnosticModuleBase, IDiagnosticModule
{
    public string Name        => "Network";
    public string Icon        => "🌐";
    public string Description => "Checks internet connectivity, DNS, adapters, and hosts file.";

    public NetworkModule(ILogger<NetworkModule> logger) : base(logger) { }

    public async Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var findings = new List<DiagnosticFinding>();

        progress.Report(10);

        // Internet connectivity
        var internetOk = await Task.Run(() => PingHost("8.8.8.8", 2000), ct);
        if (!internetOk)
            findings.Add(MakeFinding(Name, "Internet", HealthStatus.Error, Severity.Critical,
                "No internet connectivity", "Cannot reach 8.8.8.8. Check your network connection.", "reset-network-stack"));
        else
            findings.Add(Good(Name, "Internet", "Internet connected", "Successfully pinged 8.8.8.8."));

        progress.Report(30);

        // DNS resolution
        var dnsOk = await Task.Run(() =>
        {
            try { Dns.GetHostAddresses("www.microsoft.com"); return true; }
            catch { return false; }
        }, ct);
        if (!dnsOk)
            findings.Add(MakeFinding(Name, "DNS", HealthStatus.Warning, Severity.High,
                "DNS resolution failing", "Cannot resolve www.microsoft.com. DNS may be misconfigured.", "flush-dns"));
        else
            findings.Add(Good(Name, "DNS", "DNS working", "Name resolution is functional."));

        progress.Report(50);

        // Hosts file check
        var (hostsOk, suspiciousHosts) = await Task.Run(CheckHostsFile, ct);
        if (!hostsOk)
            findings.Add(MakeFinding(Name, "HostsFile", HealthStatus.Warning, Severity.Medium,
                "Unusual hosts file entries", $"Found suspicious entries: {suspiciousHosts}",
                null, "Malware sometimes modifies the hosts file to redirect traffic."));
        else
            findings.Add(Good(Name, "HostsFile", "Hosts file OK", "No suspicious entries found."));

        progress.Report(70);

        // Network adapters
        var adapters = await Task.Run(GetAdapterInfo, ct);
        findings.Add(Info(Name, "Adapters", $"{adapters.Count} active adapter(s)", string.Join(", ", adapters)));

        // Latency test
        if (internetOk)
        {
            var latency = await Task.Run(() => MeasureLatency("8.8.8.8"), ct);
            if (latency > 200)
                findings.Add(MakeFinding(Name, "Latency", HealthStatus.Warning, Severity.Low,
                    "High network latency", $"Ping to 8.8.8.8: {latency}ms. May cause slow internet."));
            else
                findings.Add(Good(Name, "Latency", "Network latency OK", $"Ping: {latency}ms."));
        }

        progress.Report(100);

        return new DiagnosticReport
        {
            ModuleName = Name,
            StartedAt = started,
            CompletedAt = DateTime.UtcNow,
            HealthScore = CalculateScore(findings.Where(f => f.Status != HealthStatus.Good).ToList()),
            Findings = findings
        };
    }

    private static bool PingHost(string host, int timeout)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(host, timeout);
            return reply.Status == IPStatus.Success;
        }
        catch { return false; }
    }

    private static long MeasureLatency(string host)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(host, 3000);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : 9999;
        }
        catch { return 9999; }
    }

    private static (bool ok, string suspicious) CheckHostsFile()
    {
        var suspicious = new List<string>();
        try
        {
            var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"drivers\etc\hosts");
            if (!File.Exists(hostsPath)) return (true, "");
            foreach (var line in File.ReadAllLines(hostsPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#') || string.IsNullOrWhiteSpace(trimmed)) continue;
                // Flag non-standard non-loopback entries
                if (!trimmed.StartsWith("127.") && !trimmed.StartsWith("::1") &&
                    !trimmed.StartsWith("0.0.0.0") && !trimmed.StartsWith("255."))
                    suspicious.Add(trimmed.Split(' ', '\t')[0]);
            }
        }
        catch { }
        return (suspicious.Count == 0, string.Join(", ", suspicious.Take(3)));
    }

    private static List<string> GetAdapterInfo()
    {
        var names = new List<string>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                var ip = nic.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.Address.ToString() ?? "no IP";
                names.Add($"{nic.Name} ({ip})");
            }
        }
        catch { }
        return names;
    }
}
