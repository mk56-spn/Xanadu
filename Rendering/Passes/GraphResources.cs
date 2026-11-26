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

    /// <summary>This frame's uploads out of the host piece: into which buffer, from where, to where, how many bytes.</summary>
    private readonly List<(Buffer Buffer, uint From, uint Into, uint Bytes)> _sends = [];

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

        if (kept.HostVersion != material.Lists.Version && effect.Compiled.Build is { } built)
        {
            Upload(commands, material.Lists, built, ref kept);
            kept.HostVersion = material.Lists.Version;
            entity.GetComponent<BuildBuffers>() = kept;
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
        {
            args[p * 4] = build.Passes[p].Drawn?.Vertices ?? 0;
            // A mesh draw's args are read as mesh tasks (x, y, z): its copies are y, so z is one.
            args[p * 4 + 2] = build.Passes[p].Drawn is { Mesh: true } ? 1u : 0u;
        }
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

    /// <summary>
    /// The host's side of the material's lists (<see cref="HostLists"/>): the lists it sizes grown to fit (keeping what
    /// they held), then whole data that moved, then the ranged writes, each cut to its buffer's size.
    /// </summary>
    private void Upload(CommandBuffer commands, HostLists lists, CompiledBuild build, ref BuildBuffers kept)
    {
        Buffer[] buffers = kept.Buffers;
        CopyPass? grow = null;
        for (int k = 0; k < buffers.Length; k++)
        {
            BuildBuffer declared = build.Shape.Buffers[k];
            int length = declared.HostSized ? lists.LengthOf(declared.Name) : -1;
            uint wanted = length < 0 ? 0 : (uint)Math.Min(length * build.Bytes[k], SlangShaders.MaxBufferBytes) / 4 * 4;
            if (wanted <= buffers[k].Size)
                continue;
            uint bytes = Math.Min(Math.Max(wanted, buffers[k].Size * 2), (uint)SlangShaders.MaxBufferBytes);
            Buffer bigger = Buffer.Create<byte>(device, $"stack {declared.Name}",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead, bytes);
            grow ??= commands.BeginCopyPass();
            grow.CopyBufferToBuffer(new BufferLocation { Buffer = buffers[k].Handle }, new BufferLocation { Buffer = bigger.Handle }, buffers[k].Size,
                cycle: false);
            // SDL_GPU lets it go once the frames using it are done.
            buffers[k].Dispose();
            buffers[k] = bigger;
        }

        if (grow is not null)
            commands.EndCopyPass(grow);

        // Everything to send, back to back in one piece: whole data that moved, then the writes.
        uint total = (uint)lists.Written.Length;
        for (int k = 0; k < buffers.Length; k++)
            if (build.Shape.Buffers[k].Host && lists.TryGetWhole(build.Shape.Buffers[k].Name, out ReadOnlySpan<byte> data, out int version)
                && version != kept.WholeVersions[k])
                total += (uint)data.Length;
        if (total == 0)
            return;
        if (_hostPiece is null || _hostPiece.Size < total)
        {
            _hostPiece?.Dispose();
            _hostPiece = TransferBuffer.Create<byte>(device, "host data", TransferBufferUsage.Upload, Math.Max(total, 1u << 16));
        }

        Span<byte> piece = _hostPiece.Map<byte>(cycle: true);
        _sends.Clear();
        uint at = 0;
        void Send(Buffer buffer, ReadOnlySpan<byte> bytes, long into, Span<byte> piece)
        {
            uint fits = into >= buffer.Size ? 0 : (uint)Math.Min(bytes.Length, buffer.Size - into) / 4 * 4;
            if (fits == 0)
                return;
            bytes[..(int)fits].CopyTo(piece[(int)at..]);
            _sends.Add((buffer, at, (uint)into, fits));
            at += fits;
        }

        for (int k = 0; k < buffers.Length; k++)
        {
            if (!build.Shape.Buffers[k].Host || !lists.TryGetWhole(build.Shape.Buffers[k].Name, out ReadOnlySpan<byte> data, out int version)
                || version == kept.WholeVersions[k])
                continue;
            kept.WholeVersions[k] = version;
            Send(buffers[k], data, 0, piece);
        }

        ReadOnlySpan<byte> written = lists.Written;
        foreach (HostWrite write in lists.Writes)
        {
            if (HostBuffer(build, write.Name) is var k and >= 0)
                Send(buffers[k], written.Slice(write.From, write.Length), write.At, piece);
        }

        lists.ClearWrites();
        _hostPiece.Unmap();
        if (_sends.Count == 0)
            return;
        CopyPass copy = commands.BeginCopyPass();
        foreach ((Buffer buffer, uint from, uint into, uint bytes) in _sends)
            copy.UploadToBuffer<byte>(_hostPiece, buffer, from, into, bytes, cycle: false);
        commands.EndCopyPass(copy);
    }

    /// <summary>The <c>[Host]</c> buffer named <paramref name="name"/>, by its place; -1 when the graph has none.</summary>
    private static int HostBuffer(CompiledBuild build, string name)
    {
        for (int k = 0; k < build.Shape.Buffers.Count; k++)
            if (build.Shape.Buffers[k].Host && build.Shape.Buffers[k].Name == name)
                return k;
        return -1;
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
