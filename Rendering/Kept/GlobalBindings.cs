using System.Collections.Generic;
using Friflo.Engine.ECS;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Gpu;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Xanadu.Rendering.Kept;

/// <summary>A declared shader global's buffer (<see cref="ShaderGlobals"/>), on an entity named by <see cref="GlobalName"/>.</summary>
internal struct GlobalBuffer(GpuArray<byte> gpu) : IComponent
{
    public GpuArray<byte> Gpu = gpu;

    /// <summary>The global's version last sent; -1 before the first.</summary>
    public int Version = -1;
}

/// <summary>The name of the shader global an entity holds.</summary>
internal struct GlobalName(string name) : IIndexedComponent<string>
{
    public string Name = name;

    public readonly string GetIndexedValue() => Name;
}

/// <summary>
/// What every effect stage binds as storage buffers: the declared shader globals in
/// <see cref="SlangShaders.Globals"/>' order, each sent when its version moves, then the
/// stack's graph buffers the stage reads, zeros standing in for any not there.
/// </summary>
internal sealed class GlobalBindings : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly EntityStore _store;
    private readonly ArchetypeQuery<GlobalBuffer> _globals;

    /// <summary>This frame's globals in binding order, then room for what a stage binds after them.</summary>
    private Buffer[] _bound = [];

    /// <summary>Zeros, bound for a global with no data (or nothing declaring it) and a buffer not made yet.</summary>
    private Buffer? _blank;

    /// <summary>The blank's bytes: as many as any stage reads of a global standing in for one.</summary>
    private const uint BlankBytes = 16 * 1024;

    private readonly TransferBuffer _zeros;

    public GlobalBindings(GraphicsDevice device, EntityStore store)
    {
        _device = device;
        _store = store;
        _globals = store.Query<GlobalBuffer>();
        _zeros = TransferBuffer.Create<byte>(device, "blank zeros", TransferBufferUsage.Upload, BlankBytes);
        _zeros.Map<byte>(cycle: false).Clear();
        _zeros.Unmap();
    }

    /// <summary>Runs the globals' per-frame writers, sends each that changed, and lines them up in binding order.</summary>
    public void Upload(CommandBuffer commands)
    {
        ShaderGlobals.RunEveryFrame();
        if (_blank is null)
        {
            _blank = Buffer.Create<byte>(_device, "blank global",
                BufferUsageFlags.GraphicsStorageRead | BufferUsageFlags.ComputeStorageRead, BlankBytes);
            CopyPass zero = commands.BeginCopyPass();
            zero.UploadToBuffer(_zeros, _blank, cycle: false);
            commands.EndCopyPass(zero);
        }

        IReadOnlyList<string> names = SlangShaders.Globals;
        // The globals, then room for a stack's graph buffers.
        if (_bound.Length != names.Count + SlangShaders.MaxStageBuffers)
            _bound = new Buffer[names.Count + SlangShaders.MaxStageBuffers];
        for (int i = 0; i < names.Count; i++)
            _bound[i] = ShaderGlobals.Find(names[i]) is { Count: > 0 } global ? Sent(commands, global) : _blank;
        for (int i = names.Count; i < _bound.Length; i++)
            _bound[i] = _blank;
    }

    private Buffer Sent(CommandBuffer commands, ShaderGlobal global)
    {
        Entities found = _store.ComponentIndex<GlobalName, string>()[global.Name];
        Entity entity = found.Count > 0
            ? found[0]
            : _store.CreateEntity(new GlobalName(global.Name), new GlobalBuffer(new GpuArray<byte>(_device, global.Name,
                BufferUsageFlags.GraphicsStorageRead | BufferUsageFlags.ComputeStorageRead, floor: 16)));
        ref GlobalBuffer held = ref entity.GetComponent<GlobalBuffer>();
        if (held.Version != global.Version)
        {
            held.Version = global.Version;
            CopyPass copy = commands.BeginCopyPass();
            held.Gpu.Upload(copy, global.Bytes);
            commands.EndCopyPass(copy);
        }

        return held.Gpu.Buffer!;
    }

    /// <summary>The frame's globals: <paramref name="count"/> of them.</summary>
    public Span<Buffer> Bound(int count) => _bound.AsSpan(0, Math.Min(count, _bound.Length));

    /// <summary>A stage's storage buffers: the globals, then the stack's graph buffers it reads (none when it reaches them by address).</summary>
    public Span<Buffer> Bound(CompiledShader stage, Buffer[]? built)
    {
        int stack = stage.Reached ? 0 : stage.StackBuffers.Length;
        int own = stage.StorageBuffers - stack;
        for (int k = 0; k < stack; k++)
            _bound[own + k] = built?[stage.StackBuffers[k]] ?? _blank!;
        return _bound.AsSpan(0, stage.StorageBuffers);
    }

    public void Dispose()
    {
        _globals.Each(new DisposeGlobals());
        _zeros.Dispose();
        _blank?.Dispose();
    }

    private struct DisposeGlobals : IEach<GlobalBuffer>
    {
        public void Execute(ref GlobalBuffer global) => global.Gpu.Dispose();
    }
}
