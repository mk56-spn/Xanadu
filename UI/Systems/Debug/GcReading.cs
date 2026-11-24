namespace Xanadu.Ui.Systems.Debug;

/// <summary>
/// The managed heap and the garbage collector at one moment, from the runtime's
/// own counters: cheap enough to read every frame. Counts and totals run from the
/// launch, so two readings subtract into what happened between them.
/// </summary>
public readonly record struct GcReading(long AllocatedBytes, long HeapBytes, int Gen0, int Gen1, int Gen2, TimeSpan Paused)
{
    /// <summary>Every thread's allocations so far (approximate by up to a few KB per thread), the heap now, collections and pauses.</summary>
    public static GcReading Now() => new(
        GC.GetTotalAllocatedBytes(),
        GC.GetTotalMemory(false),
        GC.CollectionCount(0),
        GC.CollectionCount(1),
        GC.CollectionCount(2),
        GC.GetTotalPauseDuration());

    /// <summary>Collections of exactly generation 0, 1 and 2: the runtime counts a gen2 collection as gen1 and gen0 too.</summary>
    public (int Gen0, int Gen1, int Gen2) Collections => (Gen0 - Gen1, Gen1 - Gen2, Gen2);

    /// <summary><see cref="Collections"/> between <paramref name="earlier"/> and this reading.</summary>
    public (int Gen0, int Gen1, int Gen2) CollectionsSince(GcReading earlier)
    {
        int gen0 = Gen0 - earlier.Gen0, gen1 = Gen1 - earlier.Gen1, gen2 = Gen2 - earlier.Gen2;
        return (gen0 - gen1, gen1 - gen2, gen2);
    }

    /// <summary>
    /// The process's resident memory (managed, native and driver memory mapped in).
    /// Slower than the rest: the runtime asks the OS, so read it a few times a second at most.
    /// </summary>
    public static long ProcessBytes() => Environment.WorkingSet;
}
