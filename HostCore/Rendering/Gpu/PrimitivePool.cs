using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Host.Text;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Host.Rendering.Gpu;

/// <summary>
/// Every recording's primitives in its own space, in ranges that stay while it does: an item's
/// primitives are sent again only when they changed, into the same range while they fit. When
/// the pool fills with old ranges, a bigger empty one starts and everything drawn goes up again
/// (<see cref="Overflowed"/>, then <see cref="Grow"/>).
/// </summary>
internal sealed class PrimitivePool(GraphicsDevice device) : IDisposable
{
    /// <summary>A primitive's texture slot when it reads none.</summary>
    private const uint NoTexture = 0xFFFF;

    /// <summary>A primitive's texture slot with this bit is a glyph page's layer in <see cref="Glyphs"/>' array, not a run slot.</summary>
    private const uint GlyphLayer = 0x8000;

    /// <summary>Redraws in a row that differed before an item stops hashing its primitives, and how often it checks again.</summary>
    private const int HashMissesBeforeSkipping = 8, HashRecheck = 32;

    private readonly GpuArray<GpuPrimitive> _gpu = new(device, "primitive pool", BufferUsageFlags.ComputeStorageRead);
    private readonly GpuStaging<GpuPrimitive> _staging = new(device, "pool upload");
    private uint _capacity = 1 << 16;
    private int _used;

    /// <summary>Recordings changed since they were sent: their primitives, and where they go.</summary>
    private GpuPrimitive[] _changed = new GpuPrimitive[1 << 12];
    private readonly List<(int From, int To, int Count)> _uploads = [];

    /// <summary>The text atlas, whose pages plain primitives read from its layer array instead of a run slot.</summary>
    public TextAtlas? Glyphs { get; set; }

    /// <summary>Which pool the ranges belong to; a range from an older one goes up again.</summary>
    public int Epoch { get; private set; }

    /// <summary>A range this frame didn't fit: the build starts again after <see cref="Grow"/>.</summary>
    public bool Overflowed { get; private set; }

    /// <summary>Primitives sent up this frame because they changed.</summary>
    public int ChangedCount { get; private set; }

    /// <summary>The pool buffer, made at the size the frame needs.</summary>
    public Buffer? Buffer => _gpu.Buffer;

    /// <summary>Forgets this frame's changes: the build starts again.</summary>
    public void Clear()
    {
        _uploads.Clear();
        ChangedCount = 0;
        Overflowed = false;
    }

    /// <summary>The pool is full of old ranges: a bigger, empty one, so everything drawn goes up again.</summary>
    public void Grow()
    {
        _capacity = Math.Max(_capacity * 2, (uint)_used * 2);
        _gpu.Reset();
        _used = 0;
        Epoch++;
    }

    /// <summary>Makes sure the item's primitives are in the pool as they are now.</summary>
    public void Send(CanvasItem item)
    {
        if (item.PoolEpoch == Epoch && item.PoolVersion == item.Version && item.PoolStart >= 0)
            return;

        int count = item.Primitives.Count;

        // The hash only pays when a redraw comes out the same. An item that has
        // differed every time lately (a live graph) skips it, checking again now and then.
        bool check = item.HashMisses < HashMissesBeforeSkipping || ++item.HashSkips % HashRecheck == 0;
        int content = 0;
        if (check)
        {
            var hash = new HashCode();
            hash.AddBytes(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(item.Primitives)));
            content = hash.ToHashCode();
            if (item.PoolHashKnown && item.PoolEpoch == Epoch && item.PoolStart >= 0 && item.PoolHash == content)
            {
                // Drawn again exactly as it was (most drawers clear and redraw every frame): nothing to send.
                item.PoolVersion = item.Version;
                item.HashMisses = 0;
                return;
            }

            item.HashMisses++;
        }

        item.PoolHash = content;
        item.PoolHashKnown = check;
        if (item.PoolEpoch != Epoch || item.PoolStart < 0 || count > item.PoolCapacity)
        {
            int capacity = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(count, 4));
            if (_used + capacity > _capacity)
                Overflowed = true;
            item.PoolStart = _used;
            item.PoolCapacity = capacity;
            item.PoolEpoch = Epoch;
            _used += capacity;
        }

        item.PoolVersion = item.Version;
        item.LocalTextures.Clear();
        if (ChangedCount + count > _changed.Length)
            Array.Resize(ref _changed, Math.Max(_changed.Length * 2, ChangedCount + count));
        for (int i = 0; i < count; i++)
        {
            Primitive p = item.Primitives[i];
            uint local = NoTexture;
            if (p.Texture != 0 && Glyphs?.LayerOf(p.Texture, out int layer) == true)
            {
                local = GlyphLayer | (uint)layer;
            }
            else if (p.Texture != 0)
            {
                int at = item.LocalTextures.IndexOf(p.Texture);
                if (at < 0 && item.LocalTextures.Count < PrimitivePipelines.Slots)
                {
                    at = item.LocalTextures.Count;
                    item.LocalTextures.Add(p.Texture);
                }

                if (at >= 0)
                    local = (uint)at;
            }

            p.Write(ref _changed[ChangedCount + i], local);
        }

        _uploads.Add((ChangedCount, item.PoolStart, count));
        ChangedCount += count;
    }

    /// <summary>Makes the pool at this frame's size and sends the changed recordings into their ranges.</summary>
    public void Upload(CopyPass copy)
    {
        Buffer pool = _gpu.Ensure((int)_capacity);
        if (ChangedCount == 0)
            return;

        TransferBuffer staging = _staging.Fill(_changed.AsSpan(0, ChangedCount));
        foreach ((int from, int to, int count) in _uploads)
        {
            if (count > 0)
                copy.UploadToBuffer<GpuPrimitive>(staging, pool, (uint)from, (uint)to, (uint)count, cycle: false);
        }
    }

    public void Dispose()
    {
        _gpu.Dispose();
        _staging.Dispose();
    }
}
