using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Color = Xanadu.Maths.Color;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using MwColor = MoonWorks.Graphics.Color;
using Rect = MoonWorks.Graphics.Rect;
using Texture = MoonWorks.Graphics.Texture;
using Xanadu.Slang;

namespace Xanadu.Host.Rendering;

/// <summary>
/// Draws a <see cref="CanvasTree"/> (Docs/host.md §5.3):
/// walks it once a frame in Godot's order, puts every visible item's triangles
/// into one vertex buffer in pixel space, and draws them in as few calls as the
/// textures, clip rects and materials allow.
/// </summary>
/// <remarks>
/// A frame is a list of <see cref="Job"/>s, each drawing one root into one
/// target: every registered offscreen root (a self-contained layer's surface),
/// every buffered group (an item marked as a canvas group, drawn into an image
/// of its own and composited back as one quad), then the tree itself into the
/// frame. A job always comes after the jobs whose images it draws.
/// <para>
/// Screen reads: before a draw whose effect samples <c>screenTexture</c>, or
/// one an item asked to refresh the copy for, the pass is ended, the target is
/// copied into a mipmapped screen texture, and the pass resumes. A copy is only
/// made if something was drawn since the last one, so a stale image never shows.
/// </para>
/// </remarks>
internal sealed class CanvasRenderer : IDisposable
{
    private const float GroupMargin = 2f;

    [StructLayout(LayoutKind.Sequential)]
    private struct ViewUniforms { public float Width, Height, Pad0, Pad1; }

    /// <summary>xanadu.stage's <c>Frame</c>: fragment uniform buffer 0.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct FrameUniforms { public float Width, Height, Time, ItemPremultiplied; }

    /// <summary>A kept-state effect's update's uniform buffer 0 (SlangShaders.WrapUpdate).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct UpdateUniforms { public float Dt, Time, Pad0, Pad1; }

    /// <summary>One item's draws in a job, or (with <see cref="Group"/>) a buffered group's composite.</summary>
    private readonly record struct Entry(int Z, int Sequence, CanvasItem Item, Transform2D Transform, Color Color, Rect Clip, Job? Group = null);

    /// <summary>
    /// One draw call: a run of vertices sharing texture, clip and material. An
    /// item with its own instance values is its own batch (<see cref="Owner"/>).
    /// </summary>
    private readonly record struct Batch(
        uint Start, uint Count, ulong Texture, Rect Clip, CanvasMaterial? Material, CanvasItem? Owner,
        bool Premultiplied, bool CopyBefore, ParticleField? Particles = null, Transform2D Transform = default, Color Color = default);

    /// <summary>One root drawn into one target.</summary>
    private sealed class Job(CanvasItem root, Texture target, MwColor clear, bool group)
    {
        public readonly CanvasItem Root = root;
        public readonly Texture Target = target;
        public readonly MwColor Clear = clear;
        public readonly bool IsGroup = group;
        public readonly List<Entry> Entries = new();
        public int FirstBatch, BatchCount;

        /// <summary>Where the group's content landed on its target, for the composite quad.</summary>
        public float MinX = float.MaxValue, MinY = float.MaxValue, MaxX = float.MinValue, MaxY = float.MinValue;
    }

    private readonly GraphicsDevice _device;
    private readonly CanvasTree _tree;
    private readonly CanvasPipelines _pipelines;
    private readonly Texture _white;
    private readonly Texture _screen;
    private readonly ScreenMips _mips;
    private readonly Sampler _sampler;
    private readonly uint _width, _height;

    private readonly List<Job> _jobs = new();
    private readonly List<Batch> _batches = new();
    private readonly List<(CanvasItem Root, Texture Target)> _offscreen = new();
    private readonly List<Texture> _groupTargets = new();
    private int _groupTargetsUsed;
    private CanvasVertex[] _vertices = new CanvasVertex[1 << 16];
    private int _vertexCount;
    private Buffer? _buffer;
    private TransferBuffer? _transfer;
    private uint _capacity;

    /// <summary>Each kept-state material's state, made zeroed the first frame it is drawn.</summary>
    private readonly Dictionary<CanvasMaterial, Buffer> _states = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CanvasMaterial> _updated = new(ReferenceEqualityComparer.Instance);
    private readonly TransferBuffer _zeros;

    /// <summary>Textures the game can draw by id, minted from the tree so they never clash with items.</summary>
    public Dictionary<ulong, Texture> Textures { get; } = new();

    public CanvasPipelines Pipelines => _pipelines;

    /// <summary>The GPU particles items draw (step 7); the host hands it to the game as its particle server.</summary>
    public CanvasParticles Particles { get; }

    private float _lastTime = float.NaN;

    /// <summary>Draw calls in the last frame, for the debug overlay and the step's sanity check.</summary>
    public int LastBatchCount => _batches.Count;

    /// <summary>Screen copies made in the last frame.</summary>
    public int LastCopyCount { get; private set; }

    /// <summary>Offscreen passes (layer surfaces and buffered groups) in the last frame.</summary>
    public int LastOffscreenCount => Math.Max(0, _jobs.Count - 1);

    public CanvasRenderer(GraphicsDevice device, CanvasTree tree, TextureFormat format, uint width, uint height)
    {
        _device = device;
        _tree = tree;
        _width = width;
        _height = height;
        _pipelines = new CanvasPipelines(device, format);
        Particles = new CanvasParticles(device, tree, format);

        var uploader = new ResourceUploader(device);
        _white = uploader.CreateTexture2D<uint>("white", [0xFFFFFFFFu], TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler, 1, 1);
        uploader.UploadAndWait();
        uploader.Dispose();

        uint levels = (uint)Math.Floor(Math.Log2(Math.Max(width, height))) + 1;
        _screen = Texture.Create2D(device, "screen copy", width, height, format,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget, levels);
        _mips = new ScreenMips(device, format, width, height, levels);

        // Linear with linear mips (MaxLod unclamped), so mipmapped textures such as glyph strips filter smoothly.
        _sampler = Sampler.Create(device, SamplerCreateInfo.LinearClamp);

        _zeros = TransferBuffer.Create<byte>(device, "state zeros", TransferBufferUsage.Upload, SlangShaders.MaxStateBytes);
        _zeros.Map<byte>(cycle: false).Clear();
        _zeros.Unmap();
        _pipelines.Forgotten += material =>
        {
            if (_states.Remove(material, out Buffer? state))
                state.Dispose();
        };
    }

    /// <summary>Makes a target the size of the frame, for an offscreen root or a group.</summary>
    public Texture CreateTarget(string name) =>
        Texture.Create2D(_device, name, _width, _height, TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);

    /// <summary>Draws <paramref name="root"/> (an item with no parent) into <paramref name="target"/> each frame, before the tree.</summary>
    public void AddOffscreen(CanvasItem root, Texture target) => _offscreen.Add((root, target));

    public void RemoveOffscreen(CanvasItem root) => _offscreen.RemoveAll(entry => entry.Root == root);

    /// <summary>Draws the whole frame into <paramref name="target"/>, cleared to <paramref name="clear"/>; <paramref name="time"/> is an effect's TIME.</summary>
    public void Render(CommandBuffer commands, Texture target, MwColor clear, float time)
    {
        _jobs.Clear();
        _groupTargetsUsed = 0;
        foreach ((CanvasItem root, Texture offscreen) in _offscreen)
            CollectJob(root, offscreen, new MwColor(0, 0, 0, 0), group: false, Transform2D.Identity, FullClip);
        CollectJob(_tree.Root, target, clear, group: false, Transform2D.Identity, FullClip);

        Build();
        using (commands.Scope("upload vertices"))
            Upload(commands);

        // The particles and kept state move by the frame's time, before any pass draws them.
        float dt = float.IsNaN(_lastTime) ? 0f : Math.Clamp(time - _lastTime, 0f, 0.1f);
        _lastTime = time;
        using (commands.Scope("shader updates"))
            UpdateStates(commands, dt, time);
        using (commands.Scope("particles step"))
            Particles.Step(commands, dt);

        LastCopyCount = 0;
        foreach (Job job in _jobs)
        {
            using (commands.Scope(job.IsGroup ? "group target" : job.Target == target ? "main target" : "offscreen target"))
                Draw(commands, job, time);
        }
    }

    private Rect FullClip => new() { X = 0, Y = 0, W = (int)_width, H = (int)_height };

    // ---- Collecting -------------------------------------------------------

    private Job CollectJob(CanvasItem root, Texture target, MwColor clear, bool group, Transform2D parent, Rect clip)
    {
        var job = new Job(root, target, clear, group);
        int sequence = 0;
        if (group)
        {
            // The group's own draws go inside its image, plain and unfaded: its
            // material and modulate belong to the composite.
            Transform2D transform = parent * root.Transform;
            if (root.Vertices.Count > 0)
                job.Entries.Add(new Entry(0, sequence++, root, transform, Colors.White, clip));
            root.SortChildren();
            foreach (CanvasItem child in root.Children)
                Visit(job, child, transform, Colors.White, 0, clip, ref sequence);
        }
        else
            Visit(job, root, parent, Colors.White, 0, clip, ref sequence);

        job.Entries.Sort(static (a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.Sequence.CompareTo(b.Sequence));

        // Added after any group jobs its visit made, so their images exist when it draws.
        _jobs.Add(job);
        return job;
    }

    private void Visit(Job job, CanvasItem item, Transform2D parent, Color parentModulate, int parentZ, Rect clip, ref int sequence)
    {
        if (!item.Visible)
            return;

        Transform2D transform = parent * item.Transform;
        Color modulate = parentModulate * item.Modulate;
        int z = Math.Clamp(parentZ + item.Z, -4096, 4096);
        if (item.Clip && item.UseCustomRect)
            clip = Intersect(clip, Bounds(transform, item.CustomRect));

        if (modulate.A <= 0f || clip.W <= 0 || clip.H <= 0)
            return;

        if (item.Group)
        {
            Job inner = CollectJob(item, NextGroupTarget(), new MwColor(0, 0, 0, 0), group: true, parent, FullClip);
            job.Entries.Add(new Entry(z, sequence++, item, transform, modulate * item.SelfModulate, clip, inner));
            return;
        }

        if (item.Vertices.Count > 0 || item.Particles is { Live: true })
            job.Entries.Add(new Entry(z, sequence++, item, transform, modulate * item.SelfModulate, clip));

        item.SortChildren();
        foreach (CanvasItem child in item.Children)
            Visit(job, child, transform, modulate, z, clip, ref sequence);
    }

    private Texture NextGroupTarget()
    {
        if (_groupTargetsUsed == _groupTargets.Count)
            _groupTargets.Add(CreateTarget("group"));
        return _groupTargets[_groupTargetsUsed++];
    }

    // ---- Building vertices ------------------------------------------------

    private void Build()
    {
        _batches.Clear();
        _vertexCount = 0;
        foreach (Job job in _jobs)
        {
            job.FirstBatch = _batches.Count;
            _jobFirstBatch = job.FirstBatch;
            foreach (Entry entry in job.Entries)
            {
                if (entry.Group is { } group)
                {
                    AddComposite(entry, group);
                    continue;
                }

                CanvasItem item = entry.Item;
                bool inGroupRoot = job.IsGroup && item == job.Root;
                List<(int Start, ulong Texture)> runs = item.Runs;
                for (int r = 0; r < runs.Count; r++)
                {
                    int start = runs[r].Start;
                    int end = r + 1 < runs.Count ? runs[r + 1].Start : item.Vertices.Count;
                    AddRun(job, item, start, end, entry, runs[r].Texture, plain: inGroupRoot);
                }

                // Particles draw after the item's own draws, as Godot drew an item's particles command.
                if (item.Particles is { Live: true } particles)
                    _batches.Add(new Batch((uint)_vertexCount, 0, particles.Strip, entry.Clip, null, null, false, false,
                        particles, entry.Transform, entry.Color));
            }

            job.BatchCount = _batches.Count - job.FirstBatch;
        }
    }

    private void AddRun(Job job, CanvasItem item, int start, int end, in Entry entry, ulong texture, bool plain)
    {
        int count = end - start;
        if (count <= 0)
            return;

        Reserve(count);
        Transform2D t = entry.Transform;
        Color tint = entry.Color;
        Span<CanvasVertex> source = CollectionsMarshal.AsSpan(item.Vertices).Slice(start, count);
        Span<CanvasVertex> target = _vertices.AsSpan(_vertexCount, count);
        for (int i = 0; i < count; i++)
        {
            ref readonly CanvasVertex v = ref source[i];
            float x = t.X.X * v.X + t.Y.X * v.Y + t.Origin.X;
            float y = t.X.Y * v.X + t.Y.Y * v.Y + t.Origin.Y;
            target[i] = new CanvasVertex(
                new Vector2(x, y),
                new Vector2(v.U, v.V),
                new Color(v.R * tint.R, v.G * tint.G, v.B * tint.B, v.A * tint.A),
                new Vector2(v.LX, v.LY));
            if (job.IsGroup)
            {
                job.MinX = MathF.Min(job.MinX, x);
                job.MinY = MathF.Min(job.MinY, y);
                job.MaxX = MathF.Max(job.MaxX, x);
                job.MaxY = MathF.Max(job.MaxY, y);
            }
        }

        CanvasMaterial? material = plain ? null : item.Material;
        CanvasItem? owner = !plain && item.InstanceValues is { Count: > 0 } ? item : null;
        // Only the item's first run carries its copy request.
        bool copy = item.CopyScreen && start == 0;
        AddBatch(new Batch((uint)_vertexCount, (uint)count, texture, entry.Clip, material, owner, false, copy));
        _vertexCount += count;
    }

    /// <summary>A buffered group's image laid back where the group sits, over the area its content covered.</summary>
    private void AddComposite(in Entry entry, Job group)
    {
        if (group.MinX > group.MaxX)
            return;

        float x0 = Math.Clamp(MathF.Floor(group.MinX - GroupMargin), 0, _width);
        float y0 = Math.Clamp(MathF.Floor(group.MinY - GroupMargin), 0, _height);
        float x1 = Math.Clamp(MathF.Ceiling(group.MaxX + GroupMargin), 0, _width);
        float y1 = Math.Clamp(MathF.Ceiling(group.MaxY + GroupMargin), 0, _height);
        if (x1 <= x0 || y1 <= y0)
            return;

        ulong textureId = GroupTextureId(group.Target);
        Reserve(6);
        Color color = entry.Color;
        Span<Vector2> corners = [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y0), new(x1, y1), new(x0, y1)];
        for (int i = 0; i < 6; i++)
        {
            Vector2 p = corners[i];
            _vertices[_vertexCount + i] = new CanvasVertex(p, new Vector2(p.X / _width, p.Y / _height), color, p);
        }

        CanvasItem item = entry.Item;
        CanvasItem? owner = item.InstanceValues is { Count: > 0 } ? item : null;
        AddBatch(new Batch((uint)_vertexCount, 6, textureId, entry.Clip, item.Material, owner, true, item.CopyScreen));
        _vertexCount += 6;
    }

    /// <summary>Group targets get canvas texture ids the first time they are composited.</summary>
    private readonly Dictionary<Texture, ulong> _groupIds = new();

    private ulong GroupTextureId(Texture target)
    {
        if (!_groupIds.TryGetValue(target, out ulong id))
        {
            id = _tree.MintRid().Id;
            _groupIds[target] = id;
            Textures[id] = target;
        }

        return id;
    }

    private void AddBatch(Batch batch)
    {
        // Never into the previous job's last batch: jobs draw into different targets.
        if (!batch.CopyBefore && _batches.Count > _jobFirstBatch && _batches[^1] is { } last &&
            last.Texture == batch.Texture && SameRect(last.Clip, batch.Clip) &&
            last.Material == batch.Material && last.Owner == batch.Owner &&
            last.Premultiplied == batch.Premultiplied &&
            last.Particles is null && batch.Particles is null &&
            last.Start + last.Count == batch.Start)
            _batches[^1] = last with { Count = last.Count + batch.Count };
        else
            _batches.Add(batch);
    }

    private int _jobFirstBatch;

    private void Reserve(int count)
    {
        if (_vertexCount + count > _vertices.Length)
            Array.Resize(ref _vertices, Math.Max(_vertices.Length * 2, _vertexCount + count));
    }

    // ---- GPU --------------------------------------------------------------

    private void Upload(CommandBuffer commands)
    {
        if (_vertexCount == 0)
            return;

        EnsureCapacity((uint)_vertexCount);
        Span<CanvasVertex> mapped = _transfer!.Map<CanvasVertex>(cycle: true);
        _vertices.AsSpan(0, _vertexCount).CopyTo(mapped);
        _transfer.Unmap();

        CopyPass copy = commands.BeginCopyPass();
        copy.UploadToBuffer<CanvasVertex>(_transfer, _buffer!, 0, 0, (uint)_vertexCount, cycle: true);
        commands.EndCopyPass(copy);
    }

    private void Draw(CommandBuffer commands, Job job, float time)
    {
        RenderPass pass = commands.BeginRenderPass(new ColorTargetInfo(job.Target, job.Clear));
        if (job.BatchCount == 0)
        {
            commands.EndRenderPass(pass);
            return;
        }

        var frame = new FrameUniforms { Width = _width, Height = _height, Time = time };
        commands.PushVertexUniformData(new ViewUniforms { Width = _width, Height = _height });
        commands.PushFragmentUniformData(frame, 0);
        Begin(pass);
        GraphicsPipeline? bound = null;
        bool drawnSinceCopy = true, copied = false;
        Span<TextureSamplerBinding> samplers = stackalloc TextureSamplerBinding[8];
        for (int b = job.FirstBatch; b < job.FirstBatch + job.BatchCount; b++)
        {
            Batch batch = _batches[b];
            if (batch.Particles is { } field)
            {
                pass.SetScissor(batch.Clip);
                Particles.Draw(commands, pass, field, batch.Transform, batch.Color, _width, _height,
                    Textures.GetValueOrDefault(batch.Texture) ?? _white, _sampler);
                commands.PushVertexUniformData(new ViewUniforms { Width = _width, Height = _height });
                Begin(pass);
                bound = null;
                drawnSinceCopy = true;
                continue;
            }

            CanvasEffect effect = _pipelines.Effect(batch.Material);
            string[] names = effect.Compiled.Samplers;
            bool readsScreen = effect.Compiled.ReadsScreen;
            // As Godot: the first screen read copies the target and every later
            // read shares that copy; only an item that asks (SetCopyScreen, a
            // BackBufferCopy) copies again, and only if something was drawn since.
            if ((batch.CopyBefore && drawnSinceCopy) || (readsScreen && !copied))
            {
                copied = true;
                commands.EndRenderPass(pass);
                CopyScreen(commands, job.Target);
                commands.PushFragmentUniformData(frame, 0);
                pass = commands.BeginRenderPass(new ColorTargetInfo(job.Target, LoadOp.Load));
                Begin(pass);
                bound = null;
                drawnSinceCopy = false;
            }

            GraphicsPipeline pipeline = _pipelines.Pipeline(effect, batch.Material);
            if (batch.Material?.Custom is { UsesConstant: true } custom)
                pass.SetBlendConstants(new MoonWorks.Graphics.Color(custom.Constant.R, custom.Constant.G, custom.Constant.B, custom.Constant.A));
            if (pipeline != bound)
            {
                pass.BindGraphicsPipeline(pipeline);
                bound = pipeline;
            }

            float premultiplied = batch.Premultiplied ? 1f : 0f;
            if (frame.ItemPremultiplied != premultiplied)
            {
                frame.ItemPremultiplied = premultiplied;
                commands.PushFragmentUniformData(frame, 0);
            }

            pass.SetScissor(batch.Clip);
            int count = Math.Clamp(names.Length, 1, samplers.Length);
            samplers[0] = new TextureSamplerBinding(Textures.GetValueOrDefault(batch.Texture) ?? _white, _sampler);
            for (int i = 1; i < count; i++)
            {
                Texture? texture = i == 1 && names[i] == "screenTexture"
                    ? _screen
                    : names[i] is { } name && batch.Material?.Textures.TryGetValue(name, out Handle id) == true
                        ? Textures.GetValueOrDefault(id.Id)
                        : null;
                samplers[i] = new TextureSamplerBinding(texture ?? _white, _sampler);
            }

            pass.BindFragmentSamplers(samplers[..count]);
            if (effect.Compiled.StorageBuffers > 0 && batch.Material is { } kept && _states.TryGetValue(kept, out Buffer? state))
                pass.BindFragmentStorageBuffers(state);
            if (effect.Compiled.ParamsSize > 0)
                PushParams(commands, effect.Compiled, batch.Material, batch.Owner?.InstanceValues);
            pass.DrawPrimitives(batch.Count, 1, batch.Start, 0);
            drawnSinceCopy = true;
        }

        commands.EndRenderPass(pass);
    }

    /// <summary>
    /// Runs each kept-state effect's <c>update</c> once, on one GPU thread, for
    /// every material drawn this frame, so all its pixels read the same state.
    /// </summary>
    private void UpdateStates(CommandBuffer commands, float dt, float time)
    {
        _updated.Clear();
        foreach (Batch batch in _batches)
        {
            if (batch.Material is not { KeepsState: true } material || !_updated.Add(material))
                continue;

            CanvasEffect effect = _pipelines.Effect(material);
            if (effect.Update is not { } update || effect.Compiled.Update is not { } compiled)
                continue;

            if (!_states.TryGetValue(material, out Buffer? state))
            {
                state = Buffer.Create<byte>(_device, "shader state",
                    BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead,
                    SlangShaders.MaxStateBytes);
                CopyPass copy = commands.BeginCopyPass();
                copy.UploadToBuffer(_zeros, state, cycle: false);
                commands.EndCopyPass(copy);
                _states[material] = state;
            }

            commands.PushComputeUniformData(new UpdateUniforms { Dt = dt, Time = time });
            if (compiled.ParamsSize > 0)
                PushParams(commands, compiled, material, null, compute: true);
            ComputePass pass = commands.BeginComputePass(new StorageBufferReadWriteBinding(state, cycle: false));
            pass.BindComputePipeline(update);
            pass.Dispatch(1, 1, 1);
            commands.EndComputePass(pass);
        }
    }

    private void Begin(RenderPass pass)
    {
        if (_buffer is not null)
            pass.BindVertexBuffers(_buffer);
    }

    /// <summary>
    /// The target as it stands into the screen texture, with Gaussian-filtered
    /// mips for blurred reads. Leaves fragment uniform slot 0 changed.
    /// </summary>
    private void CopyScreen(CommandBuffer commands, Texture target)
    {
        using GpuScope scope = commands.Scope("screen copy + mips");
        commands.Blit(target, _screen, Filter.Linear);
        _mips.Build(commands, _screen, _width, _height);
        LastCopyCount++;
    }

    /// <summary>Packs an effect's values (its defaults, then the material's, then the item's own) into uniform buffer 1.</summary>
    private static unsafe void PushParams(CommandBuffer commands, CompiledShader compiled, CanvasMaterial? material, Dictionary<string, Vector4>? instance, bool compute = false)
    {
        Span<byte> bytes = stackalloc byte[compiled.ParamsSize];
        bytes.Clear();
        foreach (ShaderParam param in compiled.Params)
        {
            Vector4 value = param.Default;
            if (material?.Values.TryGetValue(param.Name, out Vector4 set) == true)
                value = set;
            if (instance?.TryGetValue(param.Name, out Vector4 own) == true)
                value = own;

            Span<float> floats = [value.X, value.Y, value.Z, value.W];
            for (int i = 0; i < Math.Min(4, param.Size / 4); i++)
            {
                Span<byte> slot = bytes.Slice(param.Offset + i * 4, 4);
                if (param.IsInt)
                    BitConverter.TryWriteBytes(slot, (int)MathF.Round(floats[i]));
                else
                    BitConverter.TryWriteBytes(slot, floats[i]);
            }
        }

        fixed (byte* data = bytes)
        {
            if (compute)
                commands.PushComputeUniformData(data, (uint)bytes.Length, 1);
            else
                commands.PushFragmentUniformData(data, (uint)bytes.Length, 1);
        }
    }

    private void EnsureCapacity(uint count)
    {
        if (_buffer is not null && count <= _capacity)
            return;

        _buffer?.Dispose();
        _transfer?.Dispose();
        _capacity = Math.Max(count, Math.Max(_capacity * 2, 1u << 16));
        _buffer = Buffer.Create<CanvasVertex>(_device, "canvas vertices", BufferUsageFlags.Vertex, _capacity);
        _transfer = TransferBuffer.Create<CanvasVertex>(_device, "canvas upload", TransferBufferUsage.Upload, _capacity);
    }

    private static bool SameRect(Rect a, Rect b) => a.X == b.X && a.Y == b.Y && a.W == b.W && a.H == b.H;

    private static Rect Bounds(Transform2D t, Rect2 rect)
    {
        Vector2 a = t * rect.Position;
        Vector2 b = t * new Vector2(rect.End.X, rect.Position.Y);
        Vector2 c = t * rect.End;
        Vector2 d = t * new Vector2(rect.Position.X, rect.End.Y);
        float minX = MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X));
        float minY = MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y));
        float maxX = MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X));
        float maxY = MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y));
        int x = (int)MathF.Floor(minX), y = (int)MathF.Floor(minY);
        return new Rect { X = x, Y = y, W = (int)MathF.Ceiling(maxX) - x, H = (int)MathF.Ceiling(maxY) - y };
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        int x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.W, b.X + b.W), bottom = Math.Min(a.Y + a.H, b.Y + b.H);
        return new Rect { X = x, Y = y, W = Math.Max(0, right - x), H = Math.Max(0, bottom - y) };
    }

    public void Dispose()
    {
        _pipelines.Dispose();
        Particles.Dispose();
        _white.Dispose();
        _screen.Dispose();
        _mips.Dispose();
        _sampler.Dispose();
        _zeros.Dispose();
        foreach (Buffer state in _states.Values)
            state.Dispose();
        foreach (Texture target in _groupTargets)
            target.Dispose();
        _buffer?.Dispose();
        _transfer?.Dispose();
    }
}
