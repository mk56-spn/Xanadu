using System.Collections.Generic;
using System.Linq;
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
/// graph runs after its effect compiled, and its <c>[Host]</c> buffers filled from the material's data whenever that moves.
/// A <c>[Shared]</c> list is the frame's, not the material's: one buffer for every graph declaring that name and type,
/// zeroed when made, as big as the biggest declaration (made again, zeroed, when a bigger one comes). A list's
/// <c>[History]</c> is a buffer of its own, swapped with it each frame (<see cref="Turn"/>). Images are each
/// item's, from <see cref="ItemImages"/>.
/// </summary>
internal sealed class GraphResources(GraphicsDevice device, MaterialStore materials) : IDisposable
{
    /// <summary>Bytes of one indirect draw's args: vertices, instances, first vertex, first instance.</summary>
    public const uint ArgsBytes = 16;

    /// <summary>Bytes of zeros a new buffer is cleared from, a piece at a time.</summary>
    private const uint ZeroPiece = 1 << 20;

    private TransferBuffer? _zeroPiece, _hostPiece;

    /// <summary>The <c>[Shared]</c> lists, by name and type (<see cref="SharedUse.Key(BuildBuffer)"/>).</summary>
    private readonly Dictionary<string, Buffer> _shared = [];

    /// <summary>Moves whenever a shared list is made again, so materials holding the old one take the new.</summary>
    private int _sharedGeneration;

    /// <summary>What the material's graph keeps, made for this compile of its effect if it wasn't.</summary>
    public BuildBuffers For(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect)
    {
        Entity entity = materials.For(material);
        if (!entity.TryGetComponent(out BuildBuffers kept) || kept.Effect != effect)
        {
            if (kept.Buffers is not null)
                MaterialStore.Release(kept);
            kept = new BuildBuffers(effect, effect.Compiled.Build is { } build ? MakeBuffers(commands, build) : [])
            {
                DrawArgs = effect.Compiled.Build is { } counted ? MakeDrawArgs(commands, counted) : null,
                Reports = effect.Compiled.Build is { } reporting ? GraphReports.Make(device, reporting) : null,
                SharedGeneration = _sharedGeneration,
            };
            entity.AddComponent(kept);
        }
        else if (kept.SharedGeneration != _sharedGeneration && effect.Compiled.Build is { } sharing)
        {
            // Another graph made a shared list again (bigger): point this one's slots at the new buffers.
            for (int k = 0; k < kept.Buffers.Length; k++)
                if (sharing.Shape.Buffers[k].Shared)
                    kept.Buffers[k] = _shared[SharedUse.Key(sharing.Shape.Buffers[k])];
            entity.GetComponent<BuildBuffers>().SharedGeneration = kept.SharedGeneration = _sharedGeneration;
        }

        if (kept.HostVersion != material.BufferVersion && effect.Compiled.Build is { } built)
        {
            Upload(commands, material, built, kept.Buffers);
            entity.GetComponent<BuildBuffers>().HostVersion = kept.HostVersion = material.BufferVersion;
        }

        return kept;
    }

    /// <summary>
    /// Whether the material's <c>[Cached]</c> buffers need filling this frame (just made, or <paramref name="stamp"/> moved
    /// since they were filled), marking them filled at it: asked once a frame, before its first node runs.
    /// </summary>
    public bool Stale(CanvasMaterial material, long stamp)
    {
        ref BuildBuffers kept = ref materials.For(material).GetComponent<BuildBuffers>();
        if (kept.Filled == stamp)
            return false;
        kept.Filled = stamp;
        return true;
    }

    /// <summary>Each <c>[History(list)]</c> swapped with its list: once a frame, before the graph's first node.</summary>
    public static void Turn(BuildBuffers kept, CompiledBuild build)
    {
        for (int k = 0; k < kept.Buffers.Length; k++)
            if (build.Shape.Buffers[k].Source is >= 0 and var source)
                (kept.Buffers[k], kept.Buffers[source]) = (kept.Buffers[source], kept.Buffers[k]);
    }

    /// <summary>
    /// Pass <paramref name="pass"/>'s copies, a draw counted on the GPU, copied from its count into its args: after the
    /// passes that count them, before it draws.
    /// </summary>
    public static void Count(CommandBuffer commands, BuildBuffers kept, CompiledBuild build, int pass)
    {
        CopyPass copy = commands.BeginCopyPass();
        CompiledDraw drawn = build.Passes[pass].Drawn!;
        copy.CopyBufferToBuffer(new BufferLocation { Buffer = kept.Buffers[drawn.CopiesBuffer].Handle, Offset = (uint)drawn.CopiesAt * 4 },
            new BufferLocation { Buffer = kept.DrawArgs!.Handle, Offset = (uint)pass * ArgsBytes + 4 }, 4, cycle: false);
        commands.EndCopyPass(copy);
    }

    /// <summary>The args of the graph's counted draws, each pass's vertices in place; null when it counts none.</summary>
    private Buffer? MakeDrawArgs(CommandBuffer commands, CompiledBuild build)
    {
        if (!build.Passes.Any(pass => pass.Drawn is { CopiesBuffer: >= 0 }))
            return null;
        uint[] args = new uint[build.Passes.Length * 4];
        for (int p = 0; p < build.Passes.Length; p++)
            args[p * 4] = build.Passes[p].Drawn?.Vertices ?? 0;
        uint bytes = (uint)args.Length * 4;
        Buffer made = Buffer.Create<byte>(device, "stack draw args", BufferUsageFlags.Indirect, bytes);
        using TransferBuffer upload = TransferBuffer.Create<uint>(device, "stack draw args", TransferBufferUsage.Upload, (uint)args.Length);
        args.CopyTo(upload.Map<uint>(cycle: false));
        upload.Unmap();
        CopyPass copy = commands.BeginCopyPass();
        copy.UploadToBuffer<byte>(upload, made, 0, 0, bytes, cycle: false);
        commands.EndCopyPass(copy);
        return made;
    }

    /// <summary>Each <c>[Host]</c> buffer's data from the material, cut to the buffer's size.</summary>
    private void Upload(CommandBuffer commands, CanvasMaterial material, CompiledBuild build, Buffer[] buffers)
    {
        CopyPass? copy = null;
        for (int k = 0; k < buffers.Length; k++)
        {
            if (!build.Shape.Buffers[k].Host || !material.Buffers.TryGetValue(build.Shape.Buffers[k].Name, out byte[]? data))
                continue;
            uint bytes = Math.Min((uint)data.Length, buffers[k].Size) / 4 * 4;
            if (bytes == 0)
                continue;
            if (_hostPiece is null || _hostPiece.Size < bytes)
            {
                _hostPiece?.Dispose();
                _hostPiece = TransferBuffer.Create<byte>(device, "host data", TransferBufferUsage.Upload, Math.Max(bytes, 1u << 16));
            }

            data.AsSpan(0, (int)bytes).CopyTo(_hostPiece.Map<byte>(cycle: true));
            _hostPiece.Unmap();
            copy ??= commands.BeginCopyPass();
            copy.UploadToBuffer<byte>(_hostPiece, buffers[k], 0, 0, bytes, cycle: false);
        }

        if (copy is not null)
            commands.EndCopyPass(copy);
    }

    /// <summary>The graph's buffers, each cleared to zeros; a shared one is the frame's, zeroed only when made.</summary>
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
            BuildBuffer declared = build.Shape.Buffers[k];
            if (declared.Shared && _shared.TryGetValue(SharedUse.Key(declared), out Buffer? had) && had.Size >= bytes)
            {
                buffers[k] = had;
                continue;
            }

            buffers[k] = Buffer.Create<byte>(device, $"{(declared.Shared ? "shared" : "stack")} {declared.Name}",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead, bytes);
            for (uint at = 0; at < bytes; at += ZeroPiece)
                copy.UploadToBuffer<byte>(_zeroPiece, buffers[k], 0, at, Math.Min(ZeroPiece, bytes - at), cycle: false);
            if (!declared.Shared)
                continue;
            // Made again bigger: the old one goes once every material holding it has moved on (SDL_GPU waits for its uses).
            if (_shared.Remove(SharedUse.Key(declared), out Buffer? smaller))
            {
                smaller.Dispose();
                _sharedGeneration++;
            }

            _shared[SharedUse.Key(declared)] = buffers[k];
        }

        commands.EndCopyPass(copy);
        return buffers;
    }

    public void Dispose()
    {
        foreach (Buffer buffer in _shared.Values)
            buffer.Dispose();
        _shared.Clear();
        _zeroPiece?.Dispose();
        _hostPiece?.Dispose();
    }
}
