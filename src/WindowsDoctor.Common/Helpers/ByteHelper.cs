namespace WindowsDoctor.Common.Helpers;

/// <summary>
/// Utility methods for formatting raw byte counts into human-readable strings
/// and performing byte-unit arithmetic.
/// </summary>
public static class ByteHelper
{
    private const long KB = 1_024L;
    private const long MB = 1_024L * KB;
    private const long GB = 1_024L * MB;
    private const long TB = 1_024L * GB;

    /// <summary>
    /// Formats a raw byte count as the most appropriate human-readable string using
    /// binary prefixes (KiB, MiB, GiB, TiB).
    /// </summary>
    /// <param name="bytes">The byte count to format. Negative values are treated as zero.</param>
    /// <param name="decimals">Number of decimal places in the formatted value. Defaults to 1.</param>
    /// <returns>
    /// A string such as "4.5 GiB" or "512 MiB".
    /// Returns "0 B" when <paramref name="bytes"/> is zero or negative.
    /// </returns>
    public static string FormatBytes(long bytes, int decimals = 1)
    {
        if (bytes <= 0) return "0 B";

        string f = "F" + decimals;
        if (bytes >= TB) return ((double)bytes / TB).ToString(f) + " TiB";
        if (bytes >= GB) return ((double)bytes / GB).ToString(f) + " GiB";
        if (bytes >= MB) return ((double)bytes / MB).ToString(f) + " MiB";
        if (bytes >= KB) return ((double)bytes / KB).ToString(f) + " KiB";
        return bytes + " B";
    }

    /// <summary>
    /// Calculates the used bytes from a total and free byte count.
    /// </summary>
    /// <param name="totalBytes">Total capacity in bytes.</param>
    /// <param name="freeBytes">Available free bytes.</param>
    /// <returns>Used bytes, clamped to zero when <paramref name="freeBytes"/> exceeds <paramref name="totalBytes"/>.</returns>
    public static long GetUsedBytes(long totalBytes, long freeBytes)
        => Math.Max(0L, totalBytes - freeBytes);

    /// <summary>
    /// Computes the used percentage of a volume given total and free byte counts.
    /// </summary>
    /// <param name="totalBytes">Total capacity in bytes.</param>
    /// <param name="freeBytes">Available free bytes.</param>
    /// <returns>A value in the range [0.0, 100.0], or 0 when <paramref name="totalBytes"/> is zero.</returns>
    public static double GetUsedPercent(long totalBytes, long freeBytes)
    {
        if (totalBytes <= 0) return 0.0;
        long used = GetUsedBytes(totalBytes, freeBytes);
        return (double)used / totalBytes * 100.0;
    }

    /// <summary>
    /// Computes the free percentage of a volume given total and free byte counts.
    /// </summary>
    /// <param name="totalBytes">Total capacity in bytes.</param>
    /// <param name="freeBytes">Available free bytes.</param>
    /// <returns>A value in the range [0.0, 100.0], or 0 when <paramref name="totalBytes"/> is zero.</returns>
    public static double GetFreePercent(long totalBytes, long freeBytes)
        => 100.0 - GetUsedPercent(totalBytes, freeBytes);

    /// <summary>
    /// Converts a value expressed in gibibytes to bytes.
    /// </summary>
    /// <param name="gib">Gibibytes to convert.</param>
    /// <returns>Equivalent byte count.</returns>
    public static long GibToBytes(double gib) => (long)(gib * GB);

    /// <summary>
    /// Converts a byte count to gibibytes.
    /// </summary>
    /// <param name="bytes">Bytes to convert.</param>
    /// <returns>Equivalent value in gibibytes.</returns>
    public static double BytesToGib(long bytes) => (double)bytes / GB;
}
