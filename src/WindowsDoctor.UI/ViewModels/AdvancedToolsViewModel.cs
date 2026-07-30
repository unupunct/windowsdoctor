using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.UI.ViewModels;

public partial class AdvancedToolsViewModel : ObservableObject
{
    private readonly IRepairService _repair;
    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<ToolItem> Tools { get; }

    public AdvancedToolsViewModel(IRepairService repair)
    {
        _repair = repair;
        Tools =
        [
            new("Registry Editor",     "Edit registry keys directly.",                    "📝", new RelayCommand(() => Launch("regedit.exe"))),
            new("Services Manager",    "Start, stop, and configure Windows services.",    "⚙️", new RelayCommand(() => Launch("services.msc"))),
            new("Task Manager",        "View running processes and performance.",          "📊", new RelayCommand(() => Launch("taskmgr.exe"))),
            new("Device Manager",      "Manage hardware devices and drivers.",            "🖥️", new RelayCommand(() => Launch("devmgmt.msc"))),
            new("Event Viewer",        "Browse Windows event logs.",                      "📋", new RelayCommand(() => Launch("eventvwr.msc"))),
            new("System Configuration","Manage startup and boot options.",                "🔧", new RelayCommand(() => Launch("msconfig.exe"))),
            new("System Information",  "View detailed hardware and software info.",       "ℹ️", new RelayCommand(() => Launch("msinfo32.exe"))),
            new("Disk Management",     "Manage disk partitions and volumes.",             "💾", new RelayCommand(() => Launch("diskmgmt.msc"))),
            new("Windows Firewall",    "Configure firewall rules and profiles.",          "🛡️", new RelayCommand(() => Launch("wf.msc"))),
            new("Group Policy Editor", "Edit local group policies (Pro/Enterprise).",    "📜", new RelayCommand(() => Launch("gpedit.msc"))),
            new("Certificate Manager", "Manage user certificates.",                       "🔐", new RelayCommand(() => Launch("certmgr.msc"))),
            new("Reliability Monitor", "View system reliability history.",                "📈", new RelayCommand(() => Launch("perfmon.exe", "/rel"))),
            new("Performance Monitor", "Real-time performance counters.",                 "⚡", new RelayCommand(() => Launch("perfmon.exe"))),
            new("Windows Update",      "Check for Windows updates.",                     "🔄", new RelayCommand(() => Launch("ms-settings:windowsupdate"))),
            new("Storage Sense",       "Manage Storage Sense settings.",                  "🗄️", new RelayCommand(() => Launch("ms-settings:storagesense"))),
            new("Optimize Drives",     "Run disk defragmentation and trim.",              "🔨", new RelayCommand(() => Launch("dfrgui.exe"))),
            new("Create Restore Point","Take a system snapshot before making changes.",  "💡", new AsyncRelayCommand(CreateRestorePointAsync)),
            new("Environment Variables","Edit system and user environment variables.",   "🌍", new RelayCommand(() => LaunchRoundabout())),
            new("God Mode",            "All Control Panel items in one folder.",         "👑", new RelayCommand(OpenGodMode)),
        ];
    }

    private void Launch(string exe, string args = "")
    {
        try { Process.Start(exe, args); }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
    }

    private void LaunchRoundabout()
    {
        // Open System Properties → Advanced → Environment Variables
        try { Process.Start("rundll32.exe", "sysdm.cpl,EditEnvironmentVariables"); }
        catch { Launch("SystemPropertiesAdvanced.exe"); }
    }

    private void OpenGodMode()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "GodMode.{ED7BA470-8E54-465E-825C-99712043E01C}");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
            StatusMessage = "✅ God Mode folder opened on Desktop.";
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
    }

    private async Task CreateRestorePointAsync(CancellationToken ct = default)
    {
        StatusMessage = "Creating restore point…";
        var result = await _repair.ExecuteAsync("create-restore-point",
            new Progress<Common.Models.RepairProgress>(), ct);
        StatusMessage = result.Success ? "✅ " + result.Message : "❌ " + result.Message;
    }
}

public record ToolItem(string Name, string Description, string Icon, System.Windows.Input.ICommand Command);
