using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.UI.ViewModels;

public abstract partial class DiagnosticBaseViewModel : ObservableObject
{
    protected abstract IDiagnosticModule Module { get; }

    [ObservableProperty] private bool   _isRunning   = false;
    [ObservableProperty] private int    _progress    = 0;
    [ObservableProperty] private double _healthScore = 0;
    [ObservableProperty] private string _healthColor = "#CDD6F4";
    [ObservableProperty] private string _statusText   = "Not scanned. Press Run to analyse.";
    [ObservableProperty] private string _moduleTitle  = "";
    [ObservableProperty] private string _moduleIcon   = "";
    [ObservableProperty] private DateTime? _lastRun;

    public ObservableCollection<FindingItemViewModel> Findings { get; } = [];

    public bool HasFindings => Findings.Count > 0;

    protected void SetModuleInfo()
    {
        ModuleTitle = Module.Name;
        ModuleIcon  = Module.Icon;
    }

    [RelayCommand]
    protected async Task RunDiagnosticsAsync(CancellationToken ct = default)
    {
        if (IsRunning) return;
        IsRunning = true;
        Progress  = 0;
        Findings.Clear();
        StatusText = "Running…";

        try
        {
            var prog   = new Progress<int>(p => Progress = p);
            var report = await Module.RunDiagnosticsAsync(prog, ct);
            HealthScore = report.HealthScore;
            HealthColor = report.HealthScore switch { >= 80 => "#A6E3A1", >= 60 => "#F9E2AF", >= 40 => "#FAB387", _ => "#F38BA8" };
            LastRun     = DateTime.Now;
            StatusText  = $"Done — {report.Findings.Count} finding(s), score {report.HealthScore:0}/100.";

            foreach (var f in report.Findings.OrderByDescending(x => x.Severity))
                Findings.Add(new FindingItemViewModel(f));
            OnPropertyChanged(nameof(HasFindings));
        }
        catch (OperationCanceledException) { StatusText = "Cancelled."; }
        catch (Exception ex)               { StatusText = $"Error: {ex.Message}"; }
        finally { IsRunning = false; Progress = 100; }
    }

    [RelayCommand]
    private void ShowDetails(FindingItemViewModel? finding)
    {
        if (finding is null) return;
        System.Windows.MessageBox.Show(finding.FullDetails, $"Finding details — {finding.Title}",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }
}

public class FindingItemViewModel
{
    private readonly DiagnosticFinding _f;
    public FindingItemViewModel(DiagnosticFinding f) => _f = f;

    public string Icon    => _f.Status == HealthStatus.Good ? "✅" : _f.Severity == Severity.Critical ? "🔴" : _f.Severity == Severity.High ? "🟠" : _f.Severity == Severity.Medium ? "🟡" : "ℹ️";
    public string Title   => _f.Title;
    public string Message => _f.Message;
    public string Details => _f.Details ?? string.Empty;
    public string Module  => _f.Module;
    public string SeverityLabel => _f.Severity.ToString();
    public string StatusLabel   => _f.Status.ToString();
    public string SeverityColor => _f.Severity switch { Severity.Critical => "#F38BA8", Severity.High => "#FAB387", Severity.Medium => "#F9E2AF", Severity.Low => "#A6ADC8", _ => "#CDD6F4" };
    public string StatusColor   => _f.Status switch { HealthStatus.Good => "#A6E3A1", HealthStatus.Warning => "#F9E2AF", HealthStatus.Error => "#F38BA8", _ => "#CDD6F4" };
    public bool   HasRepair => !string.IsNullOrEmpty(_f.RecommendedRepairId);
    public string RepairId  => _f.RecommendedRepairId ?? "";

    public string FullDetails
    {
        get
        {
            var lines = new List<string>
            {
                $"Module:     {_f.Module}",
                $"Check:      {_f.Check}",
                $"Status:     {_f.Status}",
                $"Severity:   {_f.Severity}",
                "",
                $"Title:      {_f.Title}",
                $"Message:    {_f.Message}",
            };
            if (!string.IsNullOrWhiteSpace(_f.Details))
                lines.Add($"Details:    {_f.Details}");
            if (HasRepair)
                lines.Add($"Suggested repair: {_f.RecommendedRepairId} (see Repair Center)");
            if (_f.Data is { Count: > 0 })
            {
                lines.Add("");
                lines.Add("Additional data:");
                lines.AddRange(_f.Data.Select(kv => $"  {kv.Key}: {kv.Value}"));
            }
            lines.Add("");
            lines.Add($"Recorded at: {_f.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            lines.Add($"Finding ID:  {_f.Id}");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
