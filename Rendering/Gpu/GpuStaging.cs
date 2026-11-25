using MoonWorks.Graphics;

namespace Xanadu.Rendering.Gpu;

/// <summary>
/// An upload buffer that grows to fit: <see cref="Fill"/> copies items into it, ready for a copy
/// pass to send on. Grows to at least double, so a size that creeps up doesn't remake it each frame.
/// </summary>
internal sealed class GpuStaging<T>(GraphicsDevice device, string name) : IDisposable where T : unmanaged
{
    private const uint Floor = 1 << 12;

    private TransferBuffer? _transfer;
    private uint _capacity;

    /// <summary>The items in the buffer from its start, mapped with cycling so a frame still sending the last fill keeps it.</summary>
    public TransferBuffer Fill(ReadOnlySpan<T> items)
    {
        if (_transfer is null || items.Length > _capacity)
        {
            _transfer?.Dispose();
            _capacity = Math.Max((uint)items.Length, Math.Max(_capacity * 2, Floor));
            _transfer = TransferBuffer.Create<T>(device, name, TransferBufferUsage.Upload, _capacity);
        }

        items.CopyTo(_transfer.Map<T>(cycle: true));
        _transfer.Unmap();
        return _transfer;
    }

    public void Dispose() => _transfer?.Dispose();
}
