using WindowsDoctor.Core.Interfaces;
using WindowsDoctor.Diagnostics.Modules;

namespace WindowsDoctor.UI.ViewModels;

public class HardwareViewModel : DiagnosticBaseViewModel
{
    private readonly HardwareModule _module;
    protected override IDiagnosticModule Module => _module;
    public HardwareViewModel(HardwareModule module) { _module = module; SetModuleInfo(); }
}

public class WindowsHealthViewModel : DiagnosticBaseViewModel
{
    private readonly WindowsHealthModule _module;
    protected override IDiagnosticModule Module => _module;
    public WindowsHealthViewModel(WindowsHealthModule module) { _module = module; SetModuleInfo(); }
}

public class PerformanceViewModel : DiagnosticBaseViewModel
{
    private readonly PerformanceModule _module;
    protected override IDiagnosticModule Module => _module;
    public PerformanceViewModel(PerformanceModule module) { _module = module; SetModuleInfo(); }
}

public class SecurityViewModel : DiagnosticBaseViewModel
{
    private readonly SecurityModule _module;
    protected override IDiagnosticModule Module => _module;
    public SecurityViewModel(SecurityModule module) { _module = module; SetModuleInfo(); }
}

public class NetworkViewModel : DiagnosticBaseViewModel
{
    private readonly NetworkModule _module;
    protected override IDiagnosticModule Module => _module;
    public NetworkViewModel(NetworkModule module) { _module = module; SetModuleInfo(); }
}

public class SoftwareViewModel : DiagnosticBaseViewModel
{
    private readonly SoftwareModule _module;
    protected override IDiagnosticModule Module => _module;
    public SoftwareViewModel(SoftwareModule module) { _module = module; SetModuleInfo(); }
}

