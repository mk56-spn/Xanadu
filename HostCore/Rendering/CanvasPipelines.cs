using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Core.Storage;
using Xanadu.Host.Rendering.Compute;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using BlendFactor = MoonWorks.Graphics.BlendFactor;
using BlendOp = MoonWorks.Graphics.BlendOp;
using Shader = MoonWorks.Graphics.Shader;

namespace Xanadu.Host.Rendering;

/// <summary>A fragment effect ready to draw: its shader object and what it binds.</summary>
internal sealed class CanvasEffect(
    Shader shader, CompiledShader compiled, ComputePipeline? update = null, Shader? vertex = null, ComputeOperation<PoolJob>? pool = null,
    ComputePipeline[]? passes = null)
    : IDisposable
{
    public Shader Shader { get; } = shader;
    public CompiledShader Compiled { get; } = compiled;

    /// <summary>Its own vertex stage, from its <c>deform</c>; null draws with the standard one.</summary>
    public Shader? Vertex { get; } = vertex;

    /// <summary>A kept-state effect's once-a-frame <c>update</c>; null when it keeps none.</summary>
    public ComputePipeline? Update { get; } = update;

    /// <summary>A pooled stack's step (its STEP brick): one job a material, run before the draws; null without a pool.</summary>
    public ComputeOperation<PoolJob>? Pool { get; } = pool;

    /// <summary>A BUILD brick's passes, in the order it declares them (a schedule's call names one by its place); empty without one.</summary>
    public ComputePipeline[] Passes { get; } = passes ?? [];

    public void Dispose()
    {
        foreach (ComputePipeline pass in Passes)
            pass.Dispose();
        Shader.Dispose();
        Update?.Dispose();
        Vertex?.Dispose();
        Pool?.Dispose();
    }
}

/// <summary>
/// The canvas's pipelines: the standard vertex stage (or an effect's own, from its
/// <c>deform</c>) with each fragment effect,
/// one per blend mode used (Docs/host.md §3: blend is pipeline state,
/// not shader code). Effects compile the first time they are drawn and are kept;
/// one that fails to compile logs its error once and draws plain.
/// </summary>
/// <remarks>
/// Compiling on first draw can hitch; §2a wants level-load compiles and a disk
/// cache, which come with the level shader slots.
/// </remarks>
internal sealed class CanvasPipelines : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly TextureFormat _format;
    private readonly Shader _vertex;

    /// <summary>The vertex stage that draws an item's triangles once per particle (particle_mesh.slang).</summary>
    private readonly Shader _particleVertex;

    /// <summary>Particle-mesh pipelines, by effect, blend and sample count.</summary>
    private readonly Dictionary<(CanvasEffect, BlendMode, CustomBlend?, SampleCount), GraphicsPipeline> _particlePipelines = [];
    private readonly CanvasEffect _plain;
    private readonly Dictionary<string, CanvasEffect?> _effects = [];
    /// <summary>By effect, blend and whether it draws only the triangles' edges (the triangles overlay).</summary>
    private readonly Dictionary<(CanvasEffect, BlendMode, CustomBlend?, bool Lines, SampleCount Samples, bool Depth), GraphicsPipeline> _pipelines = [];

    /// <summary>The main frame's MSAA sample count, set by the renderer; warming makes pipelines for it as well as for single-sampled targets.</summary>
    public SampleCount MainSamples { get; set; } = SampleCount.One;

    /// <summary>The depth buffer's format, for pipelines drawing into the renderer's passes (which always carry one).</summary>
    public TextureFormat DepthFormat { get; init; } = TextureFormat.D16Unorm;

    public CanvasPipelines(GraphicsDevice device, TextureFormat format)
    {
        _device = device;
        _format = format;
        string canvas = Path.Combine(AppContext.BaseDirectory, "Shaders", "canvas.slang");
        CompiledShader vertex = SlangShaders.CompileReflected(canvas, "item", "vertex", samplerSpace: 0, uniformSpace: 1);
        _vertex = Shader.Create(device, vertex.Spirv, "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumUniformBuffers = vertex.UniformBuffers });
        _plain = Make(SlangShaders.CompileReflected(canvas, "shade", "fragment", samplerSpace: 2, uniformSpace: 3));

        string mesh = Path.Combine(AppContext.BaseDirectory, "Shaders", "particle_mesh.slang");
        CompiledShader particle = SlangShaders.CompileReflected(mesh, "meshParticle", "vertex", samplerSpace: 0, uniformSpace: 1);
        _particleVertex = Shader.Create(device, particle.Spirv, "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumStorageBuffers = 2, NumUniformBuffers = 2 });
    }

    /// <summary>The plain canvas fragment: vertex colour times the item's texture.</summary>
    public CanvasEffect Plain => _plain;

    /// <summary>A material's effect: its level shader code when it has some, else its shipped effect.</summary>
    public CanvasEffect Effect(CanvasMaterial? material)
    {
        if (material?.Source is not { } source)
            return Effect(material?.Shader ?? "", material?.Overlay ?? MaterialOverlay.None);

        // Keyed by the code, not the name: two slots may share a name, and an edit makes a new effect.
        string key = SourceKey(material, source);
        if (!_effects.TryGetValue(key, out CanvasEffect? effect))
        {
            try
            {
                effect = Make(SlangShaders.CompileEffectBody(material.Shader, source, material.KeepsState, ShadeWith(material.Overlay)));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Shader {material.Shader} failed to compile; drawing plain.\n{exception.Message}");
            }

            _effects[key] = effect;
        }

        return effect ?? _plain;
    }

    private static string SourceKey(CanvasMaterial material, string source) =>
        (material.KeepsState ? "state:" : "source:") + OverlayKey(material.Overlay) + source;

    private static string OverlayKey(MaterialOverlay overlay) =>
        overlay == MaterialOverlay.None ? "" : overlay + ":";

    /// <summary>The library function an overlay draws instead of shade; null for the effect itself.</summary>
    private static string? ShadeWith(MaterialOverlay overlay) => overlay switch
    {
        MaterialOverlay.Triangles => SlangShaders.TrianglesOverlay,
        MaterialOverlay.UvLines => SlangShaders.UvOverlay,
        _ => null,
    };

    /// <summary>The effect at a path in the game folder (entry point <c>shade</c>), or the plain one if it can't be built.</summary>
    public CanvasEffect Effect(string path, MaterialOverlay overlay = MaterialOverlay.None)
    {
        if (path.Length == 0)
            return _plain;

        string key = OverlayKey(overlay) + path;
        if (!_effects.TryGetValue(key, out CanvasEffect? effect))
        {
            effect = TryCompile(path, ShadeWith(overlay));
            _effects[key] = effect;
        }

        return effect ?? _plain;
    }

    private CanvasEffect? TryCompile(string path, string? shadeWith)
    {
        if (ShaderLayersKey.TryParse(path, out string[] layers))
            return TryCompileStack(path, layers, shadeWith);

        string folder = GamePaths.Full(path);
        if (!ShaderStack.IsStack(folder))
        {
            Console.Error.WriteLine($"Shader stack {path} not found; drawing plain.");
            return null;
        }

        try
        {
            // Its bricks as one body, written as level shaders are: the same wrapper calls its shade. Lab-only ones may keep state.
            string body = ShaderStack.Compose(path, ShaderStack.Read(folder));
            return Make(SlangShaders.CompileEffectBody(path, body, SlangShaders.IsLabOnly(path), shadeWith));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Shader {path} failed to compile; drawing plain.\n{exception.Message}");
            return null;
        }
    }

    /// <summary>Layered shaders (<see cref="ShaderLayersKey"/>) stitched into one and compiled, or null with its error logged.</summary>
    private CanvasEffect? TryCompileStack(string key, string[] layers, string? shadeWith)
    {
        try
        {
            var sources = new List<(string, string)>(layers.Length);
            foreach (string layer in layers)
            {
                string folder = GamePaths.Full(layer);
                if (!ShaderStack.IsStack(folder))
                    throw new DirectoryNotFoundException($"layer {layer} not found");
                sources.Add((layer, ShaderStack.Compose(layer, ShaderStack.Read(folder))));
            }

            return Make(SlangShaders.CompileEffectBody(key, ShaderLayers.Stitch(sources), keepsState: false, shadeWith));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Shader stack {key} failed to compile; drawing plain.\n{exception.Message}");
            return null;
        }
    }

    private CanvasEffect Make(CompiledShader compiled)
    {
        Shader shader = Shader.Create(_device, compiled.Spirv, "main", new ShaderCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            Stage = ShaderStage.Fragment,
            NumSamplers = (uint)compiled.Samplers.Length,
            NumStorageBuffers = (uint)compiled.StorageBuffers,
            NumUniformBuffers = compiled.UniformBuffers,
        });
        ComputePipeline? update = compiled.Update is { } step
            ? ComputePipelines.Create(_device, step.Spirv, "update brick", new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = (uint)step.StorageBuffers,
                NumReadWriteStorageBuffers = 1,
                NumUniformBuffers = step.UniformBuffers,
                ThreadCountX = 1, ThreadCountY = 1, ThreadCountZ = 1,
            })
            : null;
        Shader? vertex = compiled.Vertex is { } stage
            ? Shader.Create(_device, stage.Spirv, "main", new ShaderCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                Stage = ShaderStage.Vertex,
                NumSamplers = (uint)stage.Samplers.Length,
                NumStorageBuffers = (uint)stage.StorageBuffers,
                NumUniformBuffers = stage.UniformBuffers,
            })
            : null;
        // A pool's step: the values are uniform 1, the globals (and kept state) its read-only inputs after the jobs.
        ComputeOperation<PoolJob>? pool = compiled.Pool is { } step2
            ? new ComputeOperation<PoolJob>(_device, step2.Spirv, inputs: (uint)(step2.StorageBuffers - 1),
                groupsPerJob: (uint)((step2.ItemCount + SlangShaders.PoolThreads - 1) / SlangShaders.PoolThreads), uniforms: step2.UniformBuffers)
            : null;
        // A BUILD brick's passes: the globals (and kept state) read-only, the buffers each touches read-write.
        ComputePipeline[] passes = compiled.Build is { } build
            ? [.. build.Passes.Select(pass => ComputePipelines.Create(_device, pass.Shader.Spirv, $"build pass {pass.Name}", new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = (uint)pass.Shader.StorageBuffers,
                NumReadWriteStorageBuffers = (uint)pass.Buffers.Length,
                NumUniformBuffers = Math.Max(1, pass.Shader.UniformBuffers),
                ThreadCountX = (uint)pass.Threads, ThreadCountY = 1, ThreadCountZ = 1,
            }))]
            : [];
        return new CanvasEffect(shader, compiled, update, vertex, pool, passes);
    }

    /// <summary>
    /// Drops a level-code material's effect and its pipelines, for code that won't
    /// be drawn again (a live-edited shader's old version). SDL releases them once
    /// the GPU is done with them.
    /// </summary>
    public void Forget(CanvasMaterial material)
    {
        Forgotten?.Invoke(material);
        if (material.Source is not { } source || !_effects.Remove(SourceKey(material, source), out CanvasEffect? effect))
            return;

        Drop(effect);
    }

    /// <summary>
    /// Drops a shipped effect so its next draw compiles it again from its file,
    /// for one just saved over (the shader tool's thumbnails).
    /// </summary>
    public void Reload(string path)
    {
        foreach (MaterialOverlay overlay in Enum.GetValues<MaterialOverlay>())
        {
            if (_effects.Remove(OverlayKey(overlay) + path, out CanvasEffect? effect))
                Drop(effect);
        }
    }

    /// <summary>Frees an effect and every pipeline made with it.</summary>
    private void Drop(CanvasEffect? effect)
    {
        if (effect is null)
            return;

        foreach (((CanvasEffect Effect, BlendMode Blend, CustomBlend? Custom, bool Lines, SampleCount Samples, bool Depth) key, GraphicsPipeline pipeline) in _pipelines.ToArray())
        {
            if (key.Effect != effect)
                continue;
            pipeline.Dispose();
            _pipelines.Remove(key);
        }

        foreach (((CanvasEffect Effect, BlendMode, CustomBlend?, SampleCount) key, GraphicsPipeline pipeline) in _particlePipelines.ToArray())
        {
            if (key.Effect != effect)
                continue;
            pipeline.Dispose();
            _particlePipelines.Remove(key);
        }

        effect.Dispose();
    }

    /// <summary>Raised when a material is forgotten, so what the renderer keeps for it (its state) can go too.</summary>
    public event Action<CanvasMaterial>? Forgotten;

    /// <summary>Compiles a material's effect and its pipeline now, so its first draw doesn't stall.</summary>
    public void Warm(CanvasMaterial material)
    {
        Pipeline(Effect(material), material);
        Pipeline(Effect(material), material, MainSamples);
    }

    /// <summary>
    /// Compiles every shipped effect (each shader stack, a folder of bricks, under <c>Assets/Shaders</c>
    /// outside the library and the lab-only folder) with mix; called once at start, so screens and the
    /// playfield never compile one mid-song.
    /// </summary>
    public void WarmShipped(Action<int, int>? progress = null)
    {
        string root = GamePaths.Full("Assets/Shaders");
        string library = Path.Combine(root, "Library");
        string[] stacks = [.. ShaderStack.Under(root, skip: library).Where(folder => !SlangShaders.IsLabOnly(folder + "/"))];
        for (int i = 0; i < stacks.Length; i++)
        {
            string path = "Assets/Shaders/" + Path.GetRelativePath(root, stacks[i]).Replace('\\', '/');
            Pipeline(Effect(path), BlendMode.Mix, depth: true);
            Pipeline(Effect(path), BlendMode.Mix, samples: MainSamples, depth: true);
            progress?.Invoke(i + 1, stacks.Length);
        }
    }

    /// <summary>The pipeline for an effect drawn with a material's blend: its custom one when it has it, else its mode.</summary>
    /// <remarks>The triangles overlay draws line-filled: each triangle's edges, wherever its corners landed.</remarks>
    public GraphicsPipeline Pipeline(CanvasEffect effect, CanvasMaterial? material, SampleCount samples = SampleCount.One, bool depth = false)
    {
        bool lines = material?.Overlay == MaterialOverlay.Triangles;
        return material?.Custom is { } custom
            ? Pipeline(effect, BlendMode.Mix, custom.PipelineKey, lines, samples, depth)
            : Pipeline(effect, material?.Blend ?? BlendMode.Mix, lines: lines, samples: samples, depth: depth);
    }

    /// <summary>The pipeline for an effect and blend, drawing into a target with <paramref name="samples"/> samples a pixel.</summary>
    /// <param name="depth">Whether the target pass carries the renderer's depth buffer; without it the effect never tests it.</param>
    public GraphicsPipeline Pipeline(CanvasEffect effect, BlendMode blend, CustomBlend? custom = null, bool lines = false, SampleCount samples = SampleCount.One, bool depth = false)
    {
        if (_pipelines.TryGetValue((effect, blend, custom, lines, samples, depth), out GraphicsPipeline? pipeline))
            return pipeline;

        pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = effect.Vertex ?? _vertex,
            FragmentShader = effect.Shader,
            VertexInputState = VertexInputState.CreateSingleBinding<CanvasVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = lines ? RasterizerState.CCW_Wireframe : RasterizerState.CCW_CullNone,
            MultisampleState = new MultisampleState { SampleCount = samples },
            // In the renderer's passes an effect tests depth, so an opaque rect in front hides it. A deforming
            // effect also writes it, within its own slice, so its nearest triangle wins per pixel; ties (depth
            // left at 0) still draw in order.
            DepthStencilState = depth
                ? new DepthStencilState
                {
                    EnableDepthTest = true,
                    EnableDepthWrite = effect.Vertex is not null,
                    CompareOp = effect.Vertex is not null ? CompareOp.LessOrEqual : CompareOp.Less,
                }
                : DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new ColorTargetDescription { Format = _format, BlendState = custom is { } state ? Blend(state) : Blend(blend) }],
                HasDepthStencilTarget = depth,
                DepthStencilFormat = depth ? DepthFormat : default,
            },
        });
        _pipelines[(effect, blend, custom, lines, samples, depth)] = pipeline;
        return pipeline;
    }

    /// <summary>
    /// The pipeline that draws an item's triangles once per particle (instanced,
    /// <see cref="CanvasItem.ParticleMesh"/>) through <paramref name="effect"/>'s
    /// fragment. Its own deform is left out: the particle places the corners.
    /// Depth is tested, never written, as the quad particles do.
    /// </summary>
    public GraphicsPipeline ParticlePipeline(CanvasEffect effect, CanvasMaterial? material, SampleCount samples)
    {
        BlendMode blend = material?.Blend ?? BlendMode.Mix;
        CustomBlend? custom = material?.Custom?.PipelineKey;
        if (_particlePipelines.TryGetValue((effect, blend, custom, samples), out GraphicsPipeline? pipeline))
            return pipeline;

        pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = _particleVertex,
            FragmentShader = effect.Shader,
            VertexInputState = VertexInputState.CreateSingleBinding<CanvasVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = new MultisampleState { SampleCount = samples },
            DepthStencilState = new DepthStencilState { EnableDepthTest = true, EnableDepthWrite = false, CompareOp = CompareOp.Less },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new ColorTargetDescription { Format = _format, BlendState = custom is { } state ? Blend(state) : Blend(blend) }],
                HasDepthStencilTarget = true,
                DepthStencilFormat = DepthFormat,
            },
        });
        _particlePipelines[(effect, blend, custom, samples)] = pipeline;
        return pipeline;
    }

    /// <summary>The canvas blend modes, as fixed-function state.</summary>
    internal static ColorTargetBlendState Blend(BlendMode blend) => blend switch
    {
        BlendMode.Add => State(BlendOp.Add, BlendFactor.SrcAlpha, BlendFactor.One, BlendFactor.SrcAlpha, BlendFactor.One),
        BlendMode.Sub => State(BlendOp.ReverseSubtract, BlendFactor.SrcAlpha, BlendFactor.One, BlendFactor.SrcAlpha, BlendFactor.One),
        BlendMode.Mul => State(BlendOp.Add, BlendFactor.DstColor, BlendFactor.Zero, BlendFactor.DstAlpha, BlendFactor.Zero),
        BlendMode.Premultiplied => State(BlendOp.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
        // Mix: colour by source alpha, alpha accumulated as coverage.
        _ => State(BlendOp.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
    };

    /// <summary>A custom blend as it is: Core's enums carry SDL_GPU's numbers.</summary>
    internal static ColorTargetBlendState Blend(CustomBlend blend) => new()
    {
        EnableBlend = blend.Enabled,
        ColorBlendOp = (BlendOp)blend.ColorOp,
        AlphaBlendOp = (BlendOp)blend.AlphaOp,
        SrcColorBlendFactor = (BlendFactor)blend.SrcColor,
        DstColorBlendFactor = (BlendFactor)blend.DstColor,
        SrcAlphaBlendFactor = (BlendFactor)blend.SrcAlpha,
        DstAlphaBlendFactor = (BlendFactor)blend.DstAlpha,
        EnableColorWriteMask = true,
        ColorWriteMask = (ColorComponentFlags)blend.Write,
    };

    private static ColorTargetBlendState State(BlendOp op, BlendFactor src, BlendFactor dst, BlendFactor srcAlpha, BlendFactor dstAlpha) => new()
    {
        EnableBlend = true,
        ColorBlendOp = op,
        AlphaBlendOp = op,
        SrcColorBlendFactor = src,
        DstColorBlendFactor = dst,
        SrcAlphaBlendFactor = srcAlpha,
        DstAlphaBlendFactor = dstAlpha,
    };

    public void Dispose()
    {
        foreach (GraphicsPipeline pipeline in _pipelines.Values)
            pipeline.Dispose();
        foreach (GraphicsPipeline pipeline in _particlePipelines.Values)
            pipeline.Dispose();
        _particleVertex.Dispose();
        foreach (CanvasEffect? effect in _effects.Values)
            effect?.Dispose();
        _plain.Dispose();
        _vertex.Dispose();
    }
}
