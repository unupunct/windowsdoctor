namespace WindowsDoctor.Common.Enums;

/// <summary>Indicates the severity of a diagnostic finding or notification.</summary>
public enum Severity
{
    /// <summary>Informational only; no action required.</summary>
    Info,

    /// <summary>Minor concern; worth monitoring.</summary>
    Low,

    /// <summary>Moderate concern; action recommended.</summary>
    Medium,

    /// <summary>Significant concern; action strongly recommended.</summary>
    High,

    /// <summary>Critical issue; immediate attention required.</summary>
    Critical
}

/// <summary>Overall health status of a system component or module.</summary>
public enum HealthStatus
{
    /// <summary>Component is functioning normally.</summary>
    Good,

    /// <summary>Component shows signs of degradation or potential issues.</summary>
    Warning,

    /// <summary>Component has a confirmed error or failure.</summary>
    Error,

    /// <summary>Health status could not be determined.</summary>
    Unknown
}

/// <summary>Indicates the risk level associated with executing a repair action.</summary>
public enum RepairRisk
{
    /// <summary>No meaningful risk; fully reversible.</summary>
    Safe,

    /// <summary>Minimal risk; unlikely to cause side effects.</summary>
    Low,

    /// <summary>Moderate risk; some side effects possible.</summary>
    Medium,

    /// <summary>High risk; may have significant side effects or be difficult to reverse.</summary>
    High
}

/// <summary>Supported output formats for diagnostic health reports.</summary>
public enum ReportFormat
{
    /// <summary>Portable Document Format.</summary>
    Pdf,

    /// <summary>Hypertext Markup Language.</summary>
    Html,

    /// <summary>JavaScript Object Notation.</summary>
    Json,

    /// <summary>Comma-Separated Values.</summary>
    Csv
}

/// <summary>S.M.A.R.T. health status reported by a storage device.</summary>
public enum SmartStatus
{
    /// <summary>S.M.A.R.T. data unavailable or could not be read.</summary>
    Unknown,

    /// <summary>Drive health is within normal parameters.</summary>
    Good,

    /// <summary>Drive is reporting one or more pre-failure attributes.</summary>
    Warning,

    /// <summary>Drive has reported a failure condition.</summary>
    Failed
}

/// <summary>Top-level navigation destinations within the Windows Doctor UI.</summary>
public enum NavigationItem
{
    /// <summary>Summary dashboard with overall health score.</summary>
    Dashboard,

    /// <summary>Hardware inventory and component status.</summary>
    Hardware,

    /// <summary>Disk and storage analysis.</summary>
    Storage,

    /// <summary>Windows system file and registry health.</summary>
    WindowsHealth,

    /// <summary>CPU, RAM, and I/O performance metrics.</summary>
    Performance,

    /// <summary>Defender, firewall, and security configuration checks.</summary>
    Security,

    /// <summary>Network connectivity and adapter diagnostics.</summary>
    Network,

    /// <summary>Installed software inventory and update status.</summary>
    Software,

    /// <summary>Windows Event Log analysis.</summary>
    EventLogs,

    /// <summary>Available and completed repair actions.</summary>
    RepairCenter,

    /// <summary>Report generation and export.</summary>
    Reports,

    /// <summary>Previous scan history and trends.</summary>
    History,

    /// <summary>Application preferences and configuration.</summary>
    Settings,

    /// <summary>Advanced diagnostic and administrative tools.</summary>
    AdvancedTools
}
