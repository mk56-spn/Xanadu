using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Rendering.Gpu;

/// <summary>
/// A GPU buffer of <typeparamref name="T"/> that grows to fit, and its own upload staging.
/// Growing makes a new buffer, so what was in it is gone: callers that keep contents send them again.
/// </summary>
/// <param name="floor">The fewest items it is made with, so small frames don't remake it as they grow.</param>
internal sealed class GpuArray<T>(GraphicsDevice device, string name, BufferUsageFlags usage, uint floor = 1 << 12)
    : IDisposable where T : unmanaged
{
    private readonly GpuStaging<T> _staging = new(device, name + " upload");
    private Buffer? _buffer;

    /// <summary>The buffer, or null before the first <see cref="Ensure"/>.</summary>
    public Buffer? Buffer => _buffer;

    /// <summary>The items the buffer holds room for; 0 before it is made.</summary>
    public uint Capacity { get; private set; }

    /// <summary>The buffer with room for at least <paramref name="needed"/> items (and at least one).</summary>
    public Buffer Ensure(int needed)
    {
        uint wanted = (uint)Math.Max(needed, 1);
        if (_buffer is null || wanted > Capacity)
        {
            _buffer?.Dispose();
            Capacity = Math.Max(wanted, Math.Max(Capacity * 2, floor));
            _buffer = Buffer.Create<T>(device, name, usage, Capacity);
        }

        return _buffer;
    }

    /// <summary>
    /// Sends <paramref name="items"/> to the start of the buffer, growing it first; nothing when
    /// there are none. <paramref name="staging"/> stands in for its own, for many small arrays sharing one.
    /// </summary>
    public void Upload(CopyPass copy, ReadOnlySpan<T> items, GpuStaging<T>? staging = null)
    {
        if (items.IsEmpty)
            return;

        Buffer buffer = Ensure(items.Length);
        TransferBuffer transfer = (staging ?? _staging).Fill(items);
        copy.UploadToBuffer<T>(transfer, buffer, 0, 0, (uint)items.Length, cycle: true);
    }

    /// <summary>Lets the buffer go; the next <see cref="Ensure"/> makes a fresh one at the size asked.</summary>
    public void Reset()
    {
        _buffer?.Dispose();
        _buffer = null;
        Capacity = 0;
    }

    public void Dispose()
    {
        _buffer?.Dispose();
        _staging.Dispose();
    }
}
