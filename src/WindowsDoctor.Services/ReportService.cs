using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;
using System.Text;

namespace WindowsDoctor.Services;

public class ReportService : IReportService
{
    private readonly ILogger<ReportService> _logger;

    public ReportService(ILogger<ReportService> logger) => _logger = logger;

    public async Task GenerateAsync(FullScanRecord scan, ReportFormat format, string outputPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        switch (format)
        {
            case ReportFormat.Html: await GenerateHtmlAsync(scan, outputPath, ct); break;
            case ReportFormat.Json: await GenerateJsonAsync(scan, outputPath, ct); break;
            case ReportFormat.Csv:  await GenerateCsvAsync(scan, outputPath, ct); break;
            case ReportFormat.Pdf:  await GenerateHtmlAsync(scan, outputPath + ".html", ct); break;
        }
        _logger.LogInformation("Report written to {Path}", outputPath);
    }

    private static async Task GenerateHtmlAsync(FullScanRecord scan, string path, CancellationToken ct)
    {
        var ss = scan.SystemSnapshot;
        var score = scan.HealthReport?.OverallScore ?? 0;
        var scoreColor = score >= 80 ? "#A6E3A1" : score >= 60 ? "#F9E2AF" : "#F38BA8";
        var findings = scan.Reports.SelectMany(r => r.Findings).OrderByDescending(f => f.Severity).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>Windows Doctor Report - {scan.StartedAt:yyyy-MM-dd HH:mm}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',sans-serif;background:#1E1E2E;color:#CDD6F4;margin:0;padding:24px}");
        sb.AppendLine($"h1{{color:{scoreColor};font-size:2em}}");
        sb.AppendLine("h2{color:#89B4FA;border-bottom:1px solid #363849;padding-bottom:6px}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin-bottom:24px}");
        sb.AppendLine("th{background:#24273A;padding:8px 12px;text-align:left;color:#A6ADC8}");
        sb.AppendLine("td{padding:8px 12px;border-bottom:1px solid #363849}");
        sb.AppendLine(".good{color:#A6E3A1}.warn{color:#F9E2AF}.err{color:#F38BA8}");
        sb.AppendLine(".badge{display:inline-block;padding:2px 8px;border-radius:4px;font-size:.8em}");
        sb.AppendLine($".score-circle{{display:inline-block;width:80px;height:80px;border-radius:50%;background:{scoreColor};color:#1E1E2E;font-size:1.8em;font-weight:700;text-align:center;line-height:80px}}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>Windows Doctor Report</h1>");
        sb.AppendLine($"<p>Scanned: {scan.StartedAt:yyyy-MM-dd HH:mm:ss} UTC | Computer: {ss.ComputerName}</p>");
        sb.AppendLine($"<div class=\"score-circle\">{score:0}</div>");
        sb.AppendLine("<h2>System Information</h2><table>");
        sb.AppendLine("<tr><th>Property</th><th>Value</th></tr>");
        sb.AppendLine($"<tr><td>OS</td><td>{ss.WindowsEdition} {ss.WindowsVersion} (Build {ss.WindowsBuild})</td></tr>");
        sb.AppendLine($"<tr><td>CPU</td><td>{ss.CpuName} ({ss.CpuCores} cores, {ss.CpuUsagePercent:0}% usage)</td></tr>");
        sb.AppendLine($"<tr><td>RAM</td><td>{FormatBytes(ss.TotalRamBytes)} total, {FormatBytes(ss.AvailableRamBytes)} free</td></tr>");
        sb.AppendLine($"<tr><td>GPU</td><td>{ss.GpuName}</td></tr>");
        sb.AppendLine($"<tr><td>Motherboard</td><td>{ss.Motherboard}</td></tr>");
        sb.AppendLine($"<tr><td>BIOS</td><td>{ss.BiosVersion}</td></tr>");
        sb.AppendLine($"<tr><td>Uptime</td><td>{ss.Uptime.Days}d {ss.Uptime.Hours}h {ss.Uptime.Minutes}m</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine("<h2>Security Status</h2><table>");
        sb.AppendLine("<tr><th>Check</th><th>Status</th></tr>");
        sb.AppendLine($"<tr><td>Windows Defender</td><td class=\"{(ss.DefenderEnabled ? "good" : "err")}\">{(ss.DefenderEnabled ? "Enabled" : "Disabled")}</td></tr>");
        sb.AppendLine($"<tr><td>Firewall</td><td class=\"{(ss.FirewallEnabled ? "good" : "err")}\">{(ss.FirewallEnabled ? "Enabled" : "Disabled")}</td></tr>");
        sb.AppendLine($"<tr><td>BitLocker</td><td class=\"{(ss.BitLockerEnabled ? "good" : "warn")}\">{(ss.BitLockerEnabled ? "Enabled" : "Not enabled")}</td></tr>");
        sb.AppendLine($"<tr><td>Secure Boot</td><td class=\"{(ss.SecureBootEnabled ? "good" : "warn")}\">{(ss.SecureBootEnabled ? "Enabled" : "Disabled")}</td></tr>");
        sb.AppendLine($"<tr><td>TPM</td><td class=\"{(ss.TpmPresent ? "good" : "warn")}\">{(ss.TpmPresent ? "Present" : "Not detected")}</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine($"<h2>Findings ({findings.Count})</h2><table>");
        sb.AppendLine("<tr><th>Module</th><th>Severity</th><th>Title</th><th>Message</th></tr>");

        foreach (var f in findings)
        {
            var cls = f.Severity is Severity.Critical or Severity.High ? "err" : f.Severity == Severity.Medium ? "warn" : "good";
            sb.AppendLine($"<tr><td>{f.Module}</td><td class=\"{cls}\">{f.Severity}</td><td>{f.Title}</td><td>{f.Message}</td></tr>");
        }
        sb.AppendLine("</table></body></html>");

        await File.WriteAllTextAsync(path, sb.ToString(), ct);
    }

    private static async Task GenerateJsonAsync(FullScanRecord scan, string path, CancellationToken ct)
    {
        var json = JsonConvert.SerializeObject(scan, Formatting.Indented);
        await File.WriteAllTextAsync(path, json, ct);
    }

    private static async Task GenerateCsvAsync(FullScanRecord scan, string path, CancellationToken ct)
    {
        var lines = new List<string> { "Module,Check,Severity,Status,Title,Message" };
        foreach (var r in scan.Reports)
        foreach (var f in r.Findings)
            lines.Add($"\"{f.Module}\",\"{f.Check}\",{f.Severity},{f.Status},\"{f.Title}\",\"{f.Message?.Replace("\"", "\"\"")}\"");

        await File.WriteAllLinesAsync(path, lines, ct);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:0.0} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:0.0} MB";
        return $"{bytes / 1024.0:0.0} KB";
    }
}
