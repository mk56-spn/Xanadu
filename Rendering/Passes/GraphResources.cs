using MoonWorks.Graphics;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Entity = Friflo.Engine.ECS.Entity;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// The buffers a material's graph keeps (<see cref="BuildBuffers"/> on its entity): made, zeroed, the first time the
/// graph runs after its effect compiled. Images are each item's, from <see cref="ItemImages"/>.
/// </summary>
internal sealed class GraphResources(GraphicsDevice device, MaterialStore materials) : IDisposable
{
    /// <summary>Bytes of zeros a new buffer is cleared from, a piece at a time.</summary>
    private const uint ZeroPiece = 1 << 20;

    private TransferBuffer? _zeroPiece;

    /// <summary>What the material's graph keeps, made for this compile of its effect if it wasn't.</summary>
    public BuildBuffers For(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect)
    {
        Entity entity = materials.For(material);
        if (entity.TryGetComponent(out BuildBuffers kept) && kept.Effect == effect)
            return kept;
        if (kept.Buffers is not null)
            MaterialStore.Release(kept);
        kept = new BuildBuffers(effect, effect.Compiled.Build is { } build ? MakeBuffers(commands, build) : []);
        entity.AddComponent(kept);
        return kept;
    }

    /// <summary>The graph's buffers, each cleared to zeros.</summary>
    private Buffer[] MakeBuffers(CommandBuffer commands, CompiledBuild build)
    {
        if (_zeroPiece is null)
        {
            _zeroPiece = TransferBuffer.Create<byte>(device, "build zeros", TransferBufferUsage.Upload, ZeroPiece);
            _zeroPiece.Map<byte>(cycle: false).Clear();
            _zeroPiece.Unmap();
        }

        var buffers = new Buffer[build.Bytes.Length];
        CopyPass copy = commands.BeginCopyPass();
        for (int k = 0; k < buffers.Length; k++)
        {
            uint bytes = (uint)((build.Bytes[k] + 3) / 4 * 4);
            buffers[k] = Buffer.Create<byte>(device, $"stack {build.Shape.Buffers[k].Name}",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead, bytes);
            for (uint at = 0; at < bytes; at += ZeroPiece)
                copy.UploadToBuffer<byte>(_zeroPiece, buffers[k], 0, at, Math.Min(ZeroPiece, bytes - at), cycle: false);
        }

        commands.EndCopyPass(copy);
        return buffers;
    }

    public void Dispose() => _zeroPiece?.Dispose();
}
