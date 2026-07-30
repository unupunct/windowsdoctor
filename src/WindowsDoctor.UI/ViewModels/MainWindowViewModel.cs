using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Security.Principal;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private string _activeItem = "Dashboard";
    [ObservableProperty] private bool _isAdmin;

    public MainWindowViewModel(INavigationService nav, IServiceProvider sp)
    {
        _nav  = nav;
        _sp   = sp;
        nav.Navigated += OnNavigated;
        using var id = WindowsIdentity.GetCurrent();
        IsAdmin = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void Initialize()
        => _nav.NavigateTo(NavigationItem.Dashboard);

    [RelayCommand]
    private void Navigate(string item)
    {
        if (Enum.TryParse<NavigationItem>(item, out var nav))
            _nav.NavigateTo(nav);
    }

    private void OnNavigated(object? sender, NavigationItem item)
    {
        ActiveItem = item.ToString();
        CurrentView = item switch
        {
            NavigationItem.Dashboard    => App.GetService<DashboardViewModel>(),
            NavigationItem.Hardware     => App.GetService<HardwareViewModel>(),
            NavigationItem.Storage      => App.GetService<StorageViewModel>(),
            NavigationItem.WindowsHealth=> App.GetService<WindowsHealthViewModel>(),
            NavigationItem.Performance  => App.GetService<PerformanceViewModel>(),
            NavigationItem.Security     => App.GetService<SecurityViewModel>(),
            NavigationItem.Network      => App.GetService<NetworkViewModel>(),
            NavigationItem.Software     => App.GetService<SoftwareViewModel>(),
            NavigationItem.EventLogs    => App.GetService<EventLogsViewModel>(),
            NavigationItem.RepairCenter => App.GetService<RepairCenterViewModel>(),
            NavigationItem.Reports      => App.GetService<ReportsViewModel>(),
            NavigationItem.History      => App.GetService<HistoryViewModel>(),
            NavigationItem.Settings     => App.GetService<SettingsViewModel>(),
            NavigationItem.AdvancedTools=> App.GetService<AdvancedToolsViewModel>(),
            _                           => App.GetService<DashboardViewModel>()
        };
    }
}
