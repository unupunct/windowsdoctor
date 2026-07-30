using System.Text.RegularExpressions;

namespace WindowsDoctor.Services;

/// <summary>
/// Maps well-known Windows event log signatures (provider + event ID + message content)
/// to a plain-language explanation and a concrete suggested fix.
/// </summary>
internal static class EventDiagnosisCatalog
{
    private static readonly Regex BugCheckHex = new(@"0x([0-9A-Fa-f]{1,8})", RegexOptions.Compiled);

    private static readonly Dictionary<string, (string Cause, string Fix)> BugCheckCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["116"] = ("VIDEO_TDR_FAILURE — the graphics driver stopped responding and Windows could not recover it.",
                   "This is almost always caused by an outdated, corrupted, or unstable GPU driver. Update the driver directly from NVIDIA/AMD/Intel's website (not just Windows Update) or roll back to a previous version if this started right after an update. Also check GPU temperatures for overheating."),
        ["9F"] = ("DRIVER_POWER_STATE_FAILURE — a driver did not handle a sleep/wake power transition correctly.",
                  "Update chipset, storage, and GPU drivers. If this happens around sleep/hibernate, try disabling fast startup and hybrid sleep in Power Options."),
        ["EF"] = ("CRITICAL_PROCESS_DIED — an essential Windows process terminated unexpectedly.",
                  "Run 'sfc /scannow' and 'DISM /Online /Cleanup-Image /RestoreHealth' from the Repair Center, and check for recent driver or software changes."),
        ["133"] = ("DPC_WATCHDOG_VIOLATION — a driver took too long to complete an operation, often storage or GPU related.",
                   "Update storage controller (AHCI/NVMe) and GPU drivers. On some systems this is linked to SSD firmware — check the drive manufacturer's tool for updates."),
        ["124"] = ("WHEA_UNCORRECTABLE_ERROR — a hardware error was detected (CPU, RAM, or PCIe bus).",
                   "This points to hardware, not software. Check CPU/GPU temperatures, reseat RAM and expansion cards, and run a memory test (Windows Memory Diagnostic). If overclocked, revert to stock settings."),
        ["3B"] = ("SYSTEM_SERVICE_EXCEPTION — a driver caused an exception while running in kernel mode.",
                  "Usually a driver issue. Update GPU, network, and audio drivers, and check Reliability Monitor for what was installed shortly before the crash."),
        ["7E"] = ("SYSTEM_THREAD_EXCEPTION_NOT_HANDLED — a driver thread crashed unexpectedly.",
                  "Update the driver named in the crash message. If a specific .sys file is mentioned, search for that driver's vendor and update it."),
        ["8E"] = ("KERNEL_MODE_EXCEPTION_NOT_HANDLED — a driver or hardware fault in kernel mode.",
                  "Update all drivers, particularly GPU and storage. Run memory diagnostics if this recurs."),
    };

    public static (string? Diagnosis, string? Fix) Diagnose(string source, int eventId, string message)
    {
        // Unexpected reboot / power loss
        if (source.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase) && eventId == 41)
            return ("The system restarted without a clean shutdown — Windows did not get a chance to shut down normally first.",
                    "This is a symptom, not a root cause. Check Reliability Monitor (type 'reliability' in Start) for what happened right before the restart. Common causes: an unstable/outdated GPU driver, a power supply issue, overheating, or a hardware fault. Look for a VIDEO_TDR_FAILURE, WHEA-Logger, or Display (event 4101) entry near the same time for the real cause.");

        // Graphics driver crash/recovery (TDR)
        if (source.Contains("Display", StringComparison.OrdinalIgnoreCase) && eventId == 4101)
            return ("The graphics driver stopped responding and Windows recovered it (Timeout Detection and Recovery). This is a classic sign of an outdated or unstable GPU driver.",
                    "Update your graphics driver directly from NVIDIA, AMD, or Intel's website — not just via Windows Update. If this started right after an update, roll back to the previous driver version instead. Also check GPU temperature under load.");

        if (Regex.IsMatch(message, "nvlddmkm", RegexOptions.IgnoreCase))
            return ("The NVIDIA graphics driver (nvlddmkm.sys) crashed or stopped responding.",
                    "Update your NVIDIA driver from nvidia.com (or via GeForce Experience/NVIDIA App). If a recent driver update caused this, roll back to the previous stable version using Device Manager.");
        if (Regex.IsMatch(message, "atikmdag|amdkmdag", RegexOptions.IgnoreCase))
            return ("The AMD graphics driver crashed or stopped responding.",
                    "Update your AMD driver from amd.com (or AMD Software: Adrenalin Edition). Consider a clean reinstall using AMD Cleanup Utility if problems persist.");
        if (Regex.IsMatch(message, "igfxkmd|igfx", RegexOptions.IgnoreCase))
            return ("The Intel graphics driver crashed or stopped responding.",
                    "Update your Intel graphics driver from intel.com or via Intel Driver & Support Assistant.");

        // Blue screen report
        if (source.Contains("WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase) || eventId == 1001)
        {
            var match = BugCheckHex.Match(message);
            if (match.Success && BugCheckCodes.TryGetValue(match.Groups[1].Value.TrimStart('0').PadLeft(1, '0'), out var info))
                return ($"Windows recorded a crash (blue screen) — bug check 0x{match.Groups[1].Value}: {info.Cause}", info.Fix);
            if (match.Success)
                return ($"Windows recorded a crash (blue screen) — bug check code 0x{match.Groups[1].Value}.",
                        "Search Microsoft's documentation for this specific bug check code, or check Reliability Monitor for what was installed right before the crash.");
            return ("Windows recorded a crash (blue screen) report.",
                    "Check Reliability Monitor for details and what changed right before the crash.");
        }

        // Hardware errors
        if (source.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase))
            return ("A hardware error was detected by the CPU or chipset (Windows Hardware Error Architecture).",
                    "This points to hardware, not software. Check CPU/GPU temperatures, reseat RAM and expansion cards, run Windows Memory Diagnostic, and revert any overclocking to stock settings.");

        // Disk errors
        if (source.Equals("Disk", StringComparison.OrdinalIgnoreCase) || source.Equals("disk", StringComparison.OrdinalIgnoreCase))
            return ("The storage driver reported an I/O error communicating with a disk.",
                    "Check the disk's health on the Storage tab (S.M.A.R.T. status). Repeated disk errors can indicate a failing drive, a loose/faulty SATA cable, or a controller issue — back up important data as a precaution.");

        // Service failures
        if (source.Equals("Service Control Manager", StringComparison.OrdinalIgnoreCase))
            return ("A Windows service failed to start or stopped unexpectedly.",
                    "Usually harmless if it's a non-critical service. If it recurs for the same service, check that service's status in services.msc, or reinstall the software that owns it.");

        // Application crashes
        if (source.Equals("Application Error", StringComparison.OrdinalIgnoreCase) || source.Equals(".NET Runtime", StringComparison.OrdinalIgnoreCase))
            return ("An application crashed unexpectedly.",
                    "Usually caused by a bug in that specific application, not Windows itself. Update or reinstall the affected program if this recurs frequently.");

        return (null, null);
    }
}
