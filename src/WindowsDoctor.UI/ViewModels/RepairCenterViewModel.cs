using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.UI.ViewModels;

public partial class RepairCenterViewModel : ObservableObject
{
    private readonly IRepairService _repair;

    [ObservableProperty] private bool   _isRepairing   = false;
    [ObservableProperty] private string _repairLog      = "";
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private bool   _isAdmin;

    public ObservableCollection<RepairItemViewModel> AllRepairs      { get; } = [];
    public ObservableCollection<RepairItemViewModel> FilteredRepairs { get; } = [];
    public ObservableCollection<string>              Categories      { get; } = ["All"];

    public RepairCenterViewModel(IRepairService repair)
    {
        _repair  = repair;
        IsAdmin  = repair.IsRunningAsAdmin();

        foreach (var def in repair.GetAvailableRepairs())
        {
            AllRepairs.Add(new RepairItemViewModel(def));
            if (!Categories.Contains(def.Category))
                Categories.Add(def.Category);
        }
        ApplyFilter();
    }

    [RelayCommand]
    private void Filter(string category)
    {
        SelectedCategory = category;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredRepairs.Clear();
        var items = SelectedCategory == "All"
            ? AllRepairs
            : AllRepairs.Where(r => r.Definition.Category == SelectedCategory);
        foreach (var r in items) FilteredRepairs.Add(r);
    }

    [RelayCommand]
    private async Task ExecuteRepairAsync(RepairItemViewModel item, CancellationToken ct = default)
    {
        if (item is null || item.IsExecuting) return;

        var risk = item.Definition.Risk;
        var confirmMsg = $"Execute: {item.Definition.Name}\n\nRisk: {risk}\nExpected impact: {item.Definition.ExpectedImpact}";
        if (item.Definition.RequiresRestart)
            confirmMsg += "\n\n⚠️ This repair requires a system restart.";

        var answer = MessageBox.Show(confirmMsg, "Confirm Repair",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        item.IsExecuting = true;
        IsRepairing      = true;
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Starting: {item.Definition.Name}");

        try
        {
            var progress = new Progress<RepairProgress>(p =>
            {
                item.Progress = p.PercentComplete;
                item.Status   = p.StatusMessage;
            });

            var result = await _repair.ExecuteAsync(item.Definition.Id, progress, ct);
            item.LastResult = result.Success ? "✅ " + result.Message : "❌ " + result.Message;
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {(result.Success ? "SUCCESS" : "FAILED")}: {result.Message}");
            if (!string.IsNullOrEmpty(result.Output))    AppendLog(result.Output);
            if (!string.IsNullOrEmpty(result.ErrorOutput)) AppendLog("ERR: " + result.ErrorOutput);

            if (result.Success && item.Definition.RequiresRestart)
            {
                var restartAnswer = MessageBox.Show("A restart is required to complete this repair. Restart now?",
                    "Restart Required", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (restartAnswer == MessageBoxResult.Yes)
                    await CommandRunner.RunAsync("shutdown", "/r /t 10 /c \"Windows Doctor repair restart\"");
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] Cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
        }
        finally
        {
            item.IsExecuting = false;
            IsRepairing      = false;
        }
    }

    private void AppendLog(string line)
    {
        RepairLog += line + Environment.NewLine;
    }
}

public partial class RepairItemViewModel : ObservableObject
{
    public RepairDefinition Definition { get; }

    [ObservableProperty] private bool   _isExecuting = false;
    [ObservableProperty] private int    _progress    = 0;
    [ObservableProperty] private string _status      = "";
    [ObservableProperty] private string _lastResult  = "";

    public RepairItemViewModel(RepairDefinition def) => Definition = def;

    public string RiskColor => Definition.Risk switch
    {
        RepairRisk.Safe   => "#A6E3A1",
        RepairRisk.Low    => "#89B4FA",
        RepairRisk.Medium => "#F9E2AF",
        RepairRisk.High   => "#F38BA8",
        _                 => "#CDD6F4"
    };
    public string ElevationBadge => Definition.RequiresElevation ? "🔐" : "";
    public string RestartBadge   => Definition.RequiresRestart   ? "🔄" : "";
}

// Small helper so RepairCenterViewModel can call CommandRunner
internal static class CommandRunner
{
    internal static async Task<(bool, string, string)> RunAsync(string exe, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true };
            using var p = System.Diagnostics.Process.Start(psi)!;
            p.WaitForExit(30_000);
            return (p.ExitCode == 0, "", "");
        }
        catch (Exception ex) { return (false, "", ex.Message); }
    }
}
