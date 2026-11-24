using System.Collections.Generic;
using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Host.Rendering.Compute;

/// <summary>
/// One GPU buffer that many owners keep their items in, each in a range of
/// <see cref="Block"/> items: what a <see cref="ComputeOperation{TJob}"/>'s jobs
/// share as their writable buffer, and what draws read with a base offset.
/// It grows by doubling, copying what the GPU already has, so ranges keep their
/// offsets.
/// </summary>
internal sealed class ComputeArena<T>(GraphicsDevice device, string name, uint block) : IDisposable where T : unmanaged
{
    private readonly Stack<uint> _free = [];
    private Buffer? _buffer, _old;
    private uint _capacity, _used;

    /// <summary>Items per range.</summary>
    public uint Block { get; } = block;

    /// <summary>The arena's buffer as it is this frame; call <see cref="Prepare"/> before using it.</summary>
    public Buffer Buffer => _buffer ?? throw new InvalidOperationException("Prepare the arena before using it.");

    /// <summary>A range of <see cref="Block"/> items; its first index.</summary>
    public uint Allocate()
    {
        if (_free.Count > 0)
            return _free.Pop();

        uint first = _used;
        _used += Block;
        return first;
    }

    public void Free(uint first) => _free.Push(first);

    /// <summary>Makes the buffer big enough for every range handed out, copying the old one's contents over.</summary>
    public void Prepare(CommandBuffer commands)
    {
        _old?.Dispose();
        _old = null;
        uint needed = Math.Max(_used, Block);
        if (_buffer is not null && needed <= _capacity)
            return;

        uint capacity = Math.Max(needed, _capacity * 2);
        var grown = Buffer.Create<T>(device, name,
            BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead, capacity);
        if (_buffer is not null)
        {
            CopyPass copy = commands.BeginCopyPass();
            copy.CopyBufferToBuffer(new BufferLocation { Buffer = _buffer.Handle }, new BufferLocation { Buffer = grown.Handle },
                _capacity * (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), cycle: false);
            commands.EndCopyPass(copy);
            // Kept until the next frame: the copy reads it on the GPU.
            _old = _buffer;
        }

        _buffer = grown;
        _capacity = capacity;
    }

    public void Dispose()
    {
        _buffer?.Dispose();
        _old?.Dispose();
    }
}
