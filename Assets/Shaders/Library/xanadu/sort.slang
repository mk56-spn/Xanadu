// xanadu.sort: sorting and prefix sums on the GPU, as passes a graph calls in graph().
// The library owns no buffer: each call lends it the stack's, by name.
//
//     [Size(MAX)] List<uint> keys;      // what to sort by
//     [Size(MAX)] List<uint> ids;       // what rides along
//     [Size(MAX)] List<uint> keysB;     // the sort's other half
//     [Size(MAX)] List<uint> idsB;
//     [Size(MAX)] List<uint> counts;    // its histograms: 256 a block of 256
//
//     void build()
//     {
//         fill();                                       // a pass of the stack's own
//         sort.radix(count, keys, ids, keysB, idsB, counts);
//     }
//
// radix sorts n (key, id) pairs by their 32-bit keys, stably, in four 8-bit rounds; they end
// back in keys and ids. scan turns n counts in place into where each starts (an exclusive prefix
// sum), in one group: what binning into cells needs between counting and filling.
module sort;
import xanadu.stage;

public namespace sort
{
    /// Keys a radix block holds: the threads a histogram or scatter group runs.
    public static const uint BLOCK = 256;
    /// An 8-bit digit's values.
    public static const uint DIGITS = 256;
    /// The threads of the one group a scan runs.
    public static const uint SCAN = 1024;

    groupshared uint blockCounts[DIGITS];
    groupshared uint blockDigits[BLOCK];
    groupshared uint scanSums[SCAN];

    /// The radix blocks n keys take.
    public uint blocks(uint n) { return (n + BLOCK - 1) / BLOCK; }

    uint digitOf(uint key, uint round) { return (key >> (round * 8)) & 0xFF; }

    /// Each block's count of each digit of this round, digit-major: counts[digit * blocks + block].
    [Pass(BLOCK, groups: (n + BLOCK - 1) / BLOCK)]
    public void hist(uint i, uint n, uint round, List<uint> keys, List<uint> counts)
    {
        blockCounts[dispatch.thread] = 0;
        GroupMemoryBarrierWithGroupSync();
        if (i < n)
        {
            uint ignored;
            InterlockedAdd(blockCounts[digitOf(keys[i], round)], 1, ignored);
        }
        GroupMemoryBarrierWithGroupSync();
        counts[dispatch.thread * blocks(n) + dispatch.group] = blockCounts[dispatch.thread];
    }

    /// One group: counts[at .. at + n) become, in place, where each starts (the sum of those before it).
    [Pass(SCAN, groups: 1)]
    public void scan(uint i, uint n, uint at, List<uint> counts)
    {
        uint chunk = (n + SCAN - 1) / SCAN;
        uint start = i * chunk;
        uint end = min(start + chunk, n);
        uint sum = 0;
        for (uint k = start; k < end; k++)
            sum += counts[at + k];
        scanSums[i] = sum;
        GroupMemoryBarrierWithGroupSync();
        for (uint step = 1; step < SCAN; step <<= 1)
        {
            uint add = i >= step ? scanSums[i - step] : 0;
            GroupMemoryBarrierWithGroupSync();
            scanSums[i] += add;
            GroupMemoryBarrierWithGroupSync();
        }

        uint running = scanSums[i] - sum;
        for (uint k = start; k < end; k++)
        {
            uint count = counts[at + k];
            counts[at + k] = running;
            running += count;
        }
    }

    /// Each pair to its place for this round's digit, from keys and ids into toKeys and toIds; stable,
    /// so the order earlier rounds gave holds.
    [Pass(BLOCK, groups: (n + BLOCK - 1) / BLOCK)]
    public void scatter(uint i, uint n, uint round, List<uint> keys, List<uint> ids,
        List<uint> toKeys, List<uint> toIds, List<uint> counts)
    {
        bool inside = i < n;
        uint key = inside ? keys[i] : 0;
        uint digit = inside ? digitOf(key, round) : DIGITS;
        blockDigits[dispatch.thread] = digit;
        GroupMemoryBarrierWithGroupSync();
        if (!inside)
            return;
        uint rank = 0;
        for (uint j = 0; j < dispatch.thread; j++)
            rank += blockDigits[j] == digit ? 1 : 0;
        uint to = counts[digit * blocks(n) + dispatch.group] + rank;
        toKeys[to] = key;
        toIds[to] = ids[i];
    }

    /// n (key, id) pairs sorted by key, stably: four rounds of 8 bits, through keysB and idsB and back.
    /// counts holds DIGITS × blocks(n) uints.
    [Schedule]
    public void radix(uint n, List<uint> keys, List<uint> ids,
        List<uint> keysB, List<uint> idsB, List<uint> counts)
    {
        for (uint round = 0; round < 4; round += 2)
        {
            hist(n, round, keys, counts);
            scan(DIGITS * ((n + BLOCK - 1) / BLOCK), 0, counts);
            scatter(n, round, keys, ids, keysB, idsB, counts);
            hist(n, round + 1, keysB, counts);
            scan(DIGITS * ((n + BLOCK - 1) / BLOCK), 0, counts);
            scatter(n, round + 1, keysB, idsB, keys, ids, counts);
        }
    }
}
