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
/// <see cref="SlangShaders.Globals"/>' order, each sent when its version moves, then what
/// the stage binds after them (kept state, a pool's items, a stack's BUILD buffers), zeros
/// standing in for any not there.
/// </summary>
internal sealed class GlobalBindings : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly EntityStore _store;
    private readonly ArchetypeQuery<GlobalBuffer> _globals;

    /// <summary>This frame's globals in binding order, then room for what a stage binds after them.</summary>
    private Buffer[] _bound = [];

    /// <summary>Zeros, bound for a global with no data (or nothing declaring it) and for a state not made yet.</summary>
    private Buffer? _blank;

    /// <summary>Zeros the size of the largest state, to clear a new buffer from.</summary>
    public TransferBuffer Zeros { get; }

    public GlobalBindings(GraphicsDevice device, EntityStore store)
    {
        _device = device;
        _store = store;
        _globals = store.Query<GlobalBuffer>();
        Zeros = TransferBuffer.Create<byte>(device, "state zeros", TransferBufferUsage.Upload, SlangShaders.MaxStateBytes);
        Zeros.Map<byte>(cycle: false).Clear();
        Zeros.Unmap();
    }

    /// <summary>Runs the globals' per-frame writers, sends each that changed, and lines them up in binding order.</summary>
    public void Upload(CommandBuffer commands)
    {
        ShaderGlobals.RunEveryFrame();
        if (_blank is null)
        {
            _blank = Buffer.Create<byte>(_device, "blank global",
                BufferUsageFlags.GraphicsStorageRead | BufferUsageFlags.ComputeStorageRead, SlangShaders.MaxStateBytes);
            CopyPass zero = commands.BeginCopyPass();
            zero.UploadToBuffer(Zeros, _blank, cycle: false);
            commands.EndCopyPass(zero);
        }

        IReadOnlyList<string> names = SlangShaders.Globals;
        // The globals, two places for what a stage binds after them, then room for a stack's BUILD buffers.
        if (_bound.Length != names.Count + 2 + SlangShaders.MaxStageBuffers)
            _bound = new Buffer[names.Count + 2 + SlangShaders.MaxStageBuffers];
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

    /// <summary>The frame's globals, then <paramref name="first"/> and <paramref name="second"/> (zeros for one not there): <paramref name="count"/> in all.</summary>
    public Span<Buffer> Bound(int count, Buffer? first = null, Buffer? second = null)
    {
        int globals = SlangShaders.Globals.Count;
        _bound[globals] = first ?? _blank!;
        _bound[globals + 1] = second ?? _blank!;
        return _bound.AsSpan(0, Math.Min(count, _bound.Length));
    }

    /// <summary>A stage's storage buffers: its own (<see cref="Bound(int, Buffer?, Buffer?)"/>), then the stack's BUILD buffers it reads.</summary>
    public Span<Buffer> Bound(CompiledShader stage, Buffer? first, Buffer? second, Buffer[]? built)
    {
        int own = stage.StorageBuffers - stage.StackBuffers.Length;
        Bound(own, first, second);
        for (int k = 0; k < stage.StackBuffers.Length; k++)
            _bound[own + k] = built?[stage.StackBuffers[k]] ?? _blank!;
        return _bound.AsSpan(0, stage.StorageBuffers);
    }

    public void Dispose()
    {
        _globals.Each(new DisposeGlobals());
        Zeros.Dispose();
        _blank?.Dispose();
    }

    private struct DisposeGlobals : IEach<GlobalBuffer>
    {
        public void Execute(ref GlobalBuffer global) => global.Gpu.Dispose();
    }
}
