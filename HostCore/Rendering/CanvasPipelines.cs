using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonWorks.Graphics;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Core.Storage;
using BlendFactor = MoonWorks.Graphics.BlendFactor;
using BlendOp = MoonWorks.Graphics.BlendOp;
using Shader = MoonWorks.Graphics.Shader;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Rendering;

/// <summary>A fragment effect ready to draw: its shader object and what it binds.</summary>
internal sealed class CanvasEffect(Shader shader, CompiledShader compiled, ComputePipeline? update = null, Shader? vertex = null) : IDisposable
{
    public Shader Shader { get; } = shader;
    public CompiledShader Compiled { get; } = compiled;

    /// <summary>Its own vertex stage, from its <c>deform</c>; null draws with the standard one.</summary>
    public Shader? Vertex { get; } = vertex;

    /// <summary>A kept-state effect's once-a-frame <c>update</c>; null when it keeps none.</summary>
    public ComputePipeline? Update { get; } = update;

    public void Dispose()
    {
        Shader.Dispose();
        Update?.Dispose();
        Vertex?.Dispose();
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
    private readonly CanvasEffect _plain;
    private readonly Dictionary<string, CanvasEffect?> _effects = new();
    /// <summary>By effect, blend and whether it draws only the triangles' edges (the triangles overlay).</summary>
    private readonly Dictionary<(CanvasEffect, BlendMode, CustomBlend?, bool Lines), GraphicsPipeline> _pipelines = new();

    public CanvasPipelines(GraphicsDevice device, TextureFormat format)
    {
        _device = device;
        _format = format;
        string canvas = Path.Combine(AppContext.BaseDirectory, "Shaders", "canvas.slang");
        CompiledShader vertex = SlangShaders.CompileReflected(canvas, "item", "vertex", samplerSpace: 0, uniformSpace: 1);
        _vertex = Shader.Create(device, vertex.Spirv, "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumUniformBuffers = vertex.UniformBuffers });
        _plain = Make(SlangShaders.CompileReflected(canvas, "shade", "fragment", samplerSpace: 2, uniformSpace: 3));
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
        string file = GamePaths.Full(path);
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"Shader {path} not found; drawing plain.");
            return null;
        }

        try
        {
            // Written as level shaders are: the same wrapper calls its shade. Lab-only ones may keep state.
            return Make(SlangShaders.CompileEffectBody(path, File.ReadAllText(file), SlangShaders.IsLabOnly(path), shadeWith));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Shader {path} failed to compile; drawing plain.\n{exception.Message}");
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
            ? ComputePipeline.Create(_device, step.Spirv, "main", new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = 1,
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
                NumStorageBuffers = (uint)stage.StorageBuffers,
                NumUniformBuffers = stage.UniformBuffers,
            })
            : null;
        return new CanvasEffect(shader, compiled, update, vertex);
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

        foreach (((CanvasEffect Effect, BlendMode Blend, CustomBlend? Custom, bool Lines) key, GraphicsPipeline pipeline) in _pipelines.ToArray())
        {
            if (key.Effect != effect)
                continue;
            pipeline.Dispose();
            _pipelines.Remove(key);
        }

        effect.Dispose();
    }

    /// <summary>Raised when a material is forgotten, so what the renderer keeps for it (its state) can go too.</summary>
    public event Action<CanvasMaterial>? Forgotten;

    /// <summary>Compiles a material's effect and its pipeline now, so its first draw doesn't stall.</summary>
    public void Warm(CanvasMaterial material) =>
        Pipeline(Effect(material), material);

    /// <summary>
    /// Compiles every shipped effect (each <c>.slang</c> under <c>Assets/Shaders</c>
    /// outside the library and the lab-only folder) with mix; called once at start, so screens and the
    /// playfield never compile one mid-song.
    /// </summary>
    public void WarmShipped(Action<int, int>? progress = null)
    {
        string root = GamePaths.Full("Assets/Shaders");
        string library = Path.Combine(root, "Library");
        string[] files = Directory.EnumerateFiles(root, "*.slang", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(library, StringComparison.Ordinal) && !SlangShaders.IsLabOnly(file))
            .ToArray();
        for (int i = 0; i < files.Length; i++)
        {
            string path = "Assets/Shaders/" + Path.GetRelativePath(root, files[i]).Replace('\\', '/');
            Pipeline(Effect(path), BlendMode.Mix);
            progress?.Invoke(i + 1, files.Length);
        }
    }

    /// <summary>The pipeline for an effect drawn with a material's blend: its custom one when it has it, else its mode.</summary>
    /// <remarks>The triangles overlay draws line-filled: each triangle's edges, wherever its corners landed.</remarks>
    public GraphicsPipeline Pipeline(CanvasEffect effect, CanvasMaterial? material)
    {
        bool lines = material?.Overlay == MaterialOverlay.Triangles;
        return material?.Custom is { } custom
            ? Pipeline(effect, BlendMode.Mix, custom.PipelineKey, lines)
            : Pipeline(effect, material?.Blend ?? BlendMode.Mix, lines: lines);
    }

    public GraphicsPipeline Pipeline(CanvasEffect effect, BlendMode blend, CustomBlend? custom = null, bool lines = false)
    {
        if (_pipelines.TryGetValue((effect, blend, custom, lines), out GraphicsPipeline? pipeline))
            return pipeline;

        pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = effect.Vertex ?? _vertex,
            FragmentShader = effect.Shader,
            VertexInputState = VertexInputState.CreateSingleBinding<CanvasVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = lines ? RasterizerState.CCW_Wireframe : RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new ColorTargetDescription { Format = _format, BlendState = custom is { } state ? Blend(state) : Blend(blend) }],
            },
        });
        _pipelines[(effect, blend, custom, lines)] = pipeline;
        return pipeline;
    }

    /// <summary>Godot's canvas blend modes, as fixed-function state.</summary>
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
        foreach (CanvasEffect? effect in _effects.Values)
            effect?.Dispose();
        _plain.Dispose();
        _vertex.Dispose();
    }
}
