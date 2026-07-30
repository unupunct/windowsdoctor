using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Diagnostics.Modules;

public class EventLogModule : DiagnosticModuleBase, IDiagnosticModule
{
    public string Name        => "Event Logs";
    public string Icon        => "📋";
    public string Description => "Analyses System and Application event logs for errors and warnings.";

    public EventLogModule(ILogger<EventLogModule> logger) : base(logger) { }

    public async Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var findings = new List<DiagnosticFinding>();

        progress.Report(10);

        var (sysErrors, sysCritical, sysTopSources) = await Task.Run(() => AnalyseLog("System"), ct);
        progress.Report(50);
        var (appErrors, appCritical, appTopSources) = await Task.Run(() => AnalyseLog("Application"), ct);
        progress.Report(90);

        // System log
        ReportLogFindings(findings, "System", sysErrors, sysCritical, sysTopSources);
        ReportLogFindings(findings, "Application", appErrors, appCritical, appTopSources);

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

    private void ReportLogFindings(List<DiagnosticFinding> findings, string logName,
        int errors, int critical, string topSources)
    {
        if (errors == -1)
        {
            findings.Add(Info(Name, $"Log.{logName}.Access", $"{logName} log: access restricted",
                "Run Windows Doctor as Administrator for full event log access."));
            return;
        }

        if (critical > 5)
            findings.Add(MakeFinding(Name, $"Log.{logName}.Critical", HealthStatus.Warning, Severity.High,
                $"{critical} critical events in {logName} log",
                $"{critical} critical event(s) in last 24h. Top sources: {topSources}"));
        else if (errors > 20)
            findings.Add(MakeFinding(Name, $"Log.{logName}.Errors", HealthStatus.Warning, Severity.Medium,
                $"{errors} errors in {logName} log",
                $"{errors} error event(s) in last 24h. Top sources: {topSources}"));
        else if (errors > 0)
            findings.Add(MakeFinding(Name, $"Log.{logName}.Errors", HealthStatus.Warning, Severity.Low,
                $"{errors} errors in {logName} log (24h)",
                $"Sources: {topSources}"));
        else
            findings.Add(Good(Name, $"Log.{logName}", $"{logName} log clean", $"No errors in the last 24 hours."));
    }

    private static (int errors, int critical, string topSources) AnalyseLog(string logName)
    {
        try
        {
            var log = new EventLog(logName);
            var cutoff = DateTime.Now.AddHours(-24);
            var entries = log.Entries
                .Cast<EventLogEntry>()
                .Where(e => e.TimeGenerated >= cutoff)
                .ToList();

            var errors = entries.Count(e => e.EntryType == EventLogEntryType.Error);
            var critical = entries.Count(e => e.EntryType == EventLogEntryType.FailureAudit ||
                                              (e.EntryType == EventLogEntryType.Error && e.EventID < 10));

            var topSources = entries
                .Where(e => e.EntryType == EventLogEntryType.Error)
                .GroupBy(e => e.Source)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key}({g.Count()})")
                .ToList();

            return (errors, critical, string.Join(", ", topSources));
        }
        catch (System.Security.SecurityException) { return (-1, -1, ""); }
        catch { return (0, 0, ""); }
    }
}
