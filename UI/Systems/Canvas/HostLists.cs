using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Xanadu.Ui.Systems.Canvas;

/// <summary>One ranged write into a host list: <see cref="Length"/> bytes from <see cref="From"/> in <see cref="HostLists.Written"/>, landing <see cref="At"/> bytes in.</summary>
public readonly record struct HostWrite(string Name, long At, int From, int Length);

/// <summary>
/// What a material's graph gets from the host for its lists (Docs/render-graph.md): the whole data of a <c>[Host]</c>
/// list, set at once from its start; ranged writes into one, kept on the GPU between frames, so a range sent once stays;
/// and the length of a list declared with no <c>[Size]</c>, which the host sets at run time (it grows, keeping what it held).
/// Laid out as the shader's <c>T</c> (std430). The renderer takes the writes once a frame.
/// </summary>
public sealed class HostLists
{
    private readonly Dictionary<string, (byte[] Data, int Length, int Version)> _whole = [];
    private readonly Dictionary<string, int> _lengths = [];
    private readonly List<HostWrite> _writes = [];
    private byte[] _written = new byte[1 << 12];
    private int _writtenCount;

    /// <summary>Goes up whenever anything here moves: data set, written or sized.</summary>
    public int Version { get; private set; }

    /// <summary>The writes since the renderer last took them, in order; each one's bytes are in <see cref="Written"/>.</summary>
    public IReadOnlyList<HostWrite> Writes => _writes;

    public ReadOnlySpan<byte> Written => _written.AsSpan(0, _writtenCount);

    /// <summary>Sets a list's data from its start; the same data as last time changes nothing.</summary>
    public void Set<T>(string name, ReadOnlySpan<T> data) where T : unmanaged
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(data);
        if (_whole.TryGetValue(name, out (byte[] Data, int Length, int Version) had))
        {
            if (had.Length == bytes.Length && bytes.SequenceEqual(had.Data.AsSpan(0, had.Length)))
                return;
            if (had.Data.Length < bytes.Length)
                had.Data = new byte[Math.Max(bytes.Length, had.Data.Length * 2)];
        }
        else
        {
            had.Data = new byte[bytes.Length];
        }

        bytes.CopyTo(had.Data);
        _whole[name] = (had.Data, bytes.Length, had.Version + 1);
        Version++;
    }

    /// <summary>The data last set for <paramref name="name"/> and how many times it was set; false when it never was.</summary>
    public bool TryGetWhole(string name, out ReadOnlySpan<byte> data, out int version)
    {
        if (_whole.TryGetValue(name, out (byte[] Data, int Length, int Version) had))
        {
            data = had.Data.AsSpan(0, had.Length);
            version = had.Version;
            return true;
        }

        data = default;
        version = 0;
        return false;
    }

    /// <summary>Writes <paramref name="data"/> into the list from element <paramref name="at"/> (counted in <typeparamref name="T"/>s); the rest keeps what it held.</summary>
    public void Write<T>(string name, int at, ReadOnlySpan<T> data) where T : unmanaged
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(data);
        if (bytes.IsEmpty)
            return;
        if (_writtenCount + bytes.Length > _written.Length)
            Array.Resize(ref _written, Math.Max(_written.Length * 2, _writtenCount + bytes.Length));
        bytes.CopyTo(_written.AsSpan(_writtenCount));
        _writes.Add(new HostWrite(name, (long)at * Unsafe.SizeOf<T>(), _writtenCount, bytes.Length));
        _writtenCount += bytes.Length;
        Version++;
    }

    /// <summary>The renderer has sent every write.</summary>
    public void ClearWrites()
    {
        _writes.Clear();
        _writtenCount = 0;
    }

    /// <summary>Sets how many elements a list declared with no <c>[Size]</c> holds; it grows to fit, keeping what it held.</summary>
    public void Size(string name, int elements)
    {
        if (_lengths.TryGetValue(name, out int had) && had == elements)
            return;
        _lengths[name] = elements;
        Version++;
    }

    /// <summary>The length the host set for <paramref name="name"/>; -1 when it set none.</summary>
    public int LengthOf(string name) => _lengths.TryGetValue(name, out int length) ? length : -1;
}
