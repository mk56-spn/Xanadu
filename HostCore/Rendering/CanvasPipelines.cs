using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonWorks.Graphics;
using Xanadu.Rendering;
using Xanadu.Storage;
using BlendFactor = MoonWorks.Graphics.BlendFactor;
using BlendOp = MoonWorks.Graphics.BlendOp;
using Shader = MoonWorks.Graphics.Shader;
using Xanadu.Slang;

namespace Xanadu.Host.Rendering;

/// <summary>A fragment effect ready to draw: its shader object and what it binds.</summary>
internal sealed class CanvasEffect(Shader shader, CompiledShader compiled, ComputePipeline? update = null) : IDisposable
{
    public Shader Shader { get; } = shader;
    public CompiledShader Compiled { get; } = compiled;

    /// <summary>A kept-state effect's once-a-frame <c>update</c>; null when it keeps none.</summary>
    public ComputePipeline? Update { get; } = update;

    public void Dispose()
    {
        Shader.Dispose();
        Update?.Dispose();
    }
}

/// <summary>
/// The canvas's pipelines: the standard vertex stage with each fragment effect,
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
    private readonly Dictionary<(CanvasEffect, BlendMode, CustomBlend?), GraphicsPipeline> _pipelines = new();

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
    public CanvasEffect Effect(Xanadu.Systems.Canvas.CanvasMaterial? material)
    {
        if (material?.Source is not { } source)
            return Effect(material?.Shader ?? "");

        // Keyed by the code, not the name: two slots may share a name, and an edit makes a new effect.
        string key = SourceKey(material, source);
        if (!_effects.TryGetValue(key, out CanvasEffect? effect))
        {
            try
            {
                effect = Make(material.Whole
                    ? SlangShaders.CompileWholeEffect(material.Shader, source)
                    : SlangShaders.CompileEffectBody(material.Shader, source, material.KeepsState));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Shader {material.Shader} failed to compile; drawing plain.\n{exception.Message}");
            }

            _effects[key] = effect;
        }

        return effect ?? _plain;
    }

    private static string SourceKey(Xanadu.Systems.Canvas.CanvasMaterial material, string source) =>
        (material.Whole ? "whole:" : material.KeepsState ? "state:" : "source:") + source;

    /// <summary>The effect at a <c>res://</c> path (entry point <c>shade</c>), or the plain one if it can't be built.</summary>
    public CanvasEffect Effect(string path)
    {
        if (path.Length == 0)
            return _plain;

        if (!_effects.TryGetValue(path, out CanvasEffect? effect))
        {
            effect = TryCompile(path);
            _effects[path] = effect;
        }

        return effect ?? _plain;
    }

    private CanvasEffect? TryCompile(string path)
    {
        string file = GamePaths.Globalize(path);
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"Shader {path} not found; drawing plain.");
            return null;
        }

        try
        {
            return Make(SlangShaders.CompileReflected(file, "shade", "fragment", samplerSpace: 2, uniformSpace: 3));
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
                NumReadWriteStorageBuffers = 1,
                NumUniformBuffers = step.UniformBuffers,
                ThreadCountX = 1, ThreadCountY = 1, ThreadCountZ = 1,
            })
            : null;
        return new CanvasEffect(shader, compiled, update);
    }

    /// <summary>
    /// Drops a level-code material's effect and its pipelines, for code that won't
    /// be drawn again (a live-edited shader's old version). SDL releases them once
    /// the GPU is done with them.
    /// </summary>
    public void Forget(Xanadu.Systems.Canvas.CanvasMaterial material)
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
        if (_effects.Remove(path, out CanvasEffect? effect))
            Drop(effect);
    }

    /// <summary>Frees an effect and every pipeline made with it.</summary>
    private void Drop(CanvasEffect? effect)
    {
        if (effect is null)
            return;

        foreach (((CanvasEffect Effect, BlendMode Blend, CustomBlend? Custom) key, GraphicsPipeline pipeline) in _pipelines.ToArray())
        {
            if (key.Effect != effect)
                continue;
            pipeline.Dispose();
            _pipelines.Remove(key);
        }

        effect.Dispose();
    }

    /// <summary>Raised when a material is forgotten, so what the renderer keeps for it (its state) can go too.</summary>
    public event Action<Xanadu.Systems.Canvas.CanvasMaterial>? Forgotten;

    /// <summary>Compiles a material's effect and its pipeline now, so its first draw doesn't stall.</summary>
    public void Warm(Xanadu.Systems.Canvas.CanvasMaterial material) =>
        Pipeline(Effect(material), material);

    /// <summary>
    /// Compiles every shipped effect (each <c>.slang</c> under <c>Assets/Shaders</c>
    /// outside the library) with mix; called once at start, so screens and the
    /// playfield never compile one mid-song.
    /// </summary>
    public void WarmShipped()
    {
        string root = GamePaths.Globalize("res://Assets/Shaders");
        string library = Path.Combine(root, "Library");
        foreach (string file in Directory.EnumerateFiles(root, "*.slang", SearchOption.AllDirectories))
        {
            if (file.StartsWith(library, StringComparison.Ordinal))
                continue;

            string path = "res://Assets/Shaders/" + Path.GetRelativePath(root, file).Replace('\\', '/');
            Pipeline(Effect(path), BlendMode.Mix);
        }
    }

    /// <summary>The pipeline for an effect drawn with a material's blend: its custom one when it has it, else its mode.</summary>
    public GraphicsPipeline Pipeline(CanvasEffect effect, Xanadu.Systems.Canvas.CanvasMaterial? material) =>
        material?.Custom is { } custom
            ? Pipeline(effect, BlendMode.Mix, custom.PipelineKey)
            : Pipeline(effect, material?.Blend ?? BlendMode.Mix);

    public GraphicsPipeline Pipeline(CanvasEffect effect, BlendMode blend, CustomBlend? custom = null)
    {
        if (_pipelines.TryGetValue((effect, blend, custom), out GraphicsPipeline? pipeline))
            return pipeline;

        pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = _vertex,
            FragmentShader = effect.Shader,
            VertexInputState = VertexInputState.CreateSingleBinding<CanvasVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new ColorTargetDescription { Format = _format, BlendState = custom is { } state ? Blend(state) : Blend(blend) }],
            },
        });
        _pipelines[(effect, blend, custom)] = pipeline;
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
