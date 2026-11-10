namespace Xanadu.Systems.Debug;

/// <summary>
/// Video memory as the driver reports it, in bytes, over the GPU's own (device-local) heaps:
/// what this process has allocated, how much the driver says it may use right now
/// (what's left after other programs, so it moves), and the heaps' full size.
/// </summary>
public readonly record struct GpuMemoryReading(long ProcessBytes, long BudgetBytes, long TotalBytes);

/// <summary>Provides video memory use to the debug overlay.</summary>
public interface IGpuMemorySource
{
    /// <summary>The current reading; null when the driver can't say.</summary>
    GpuMemoryReading? Read();
}
