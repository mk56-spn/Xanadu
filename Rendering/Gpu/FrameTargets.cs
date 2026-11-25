using MoonWorks.Graphics;
using MwColor = MoonWorks.Graphics.Color;
using Rect = MoonWorks.Graphics.Rect;

namespace Xanadu.Rendering.Gpu;

/// <summary>
/// What the canvas draws into and reads back, all the size of the frame: the depth buffers, the
/// multisampled frame, the screen copy effects sample with its mips, the sampler, and the
/// stand-in textures bound where nothing is.
/// </summary>
internal sealed class FrameTargets : IDisposable
{
    /// <summary>The MSAA sample count asked for; drops to what the GPU can do.</summary>
    public const SampleCount WantedSamples = SampleCount.Four;

    private readonly GraphicsDevice _device;
    private readonly ScreenMips _mips;
    private Texture? _noLayers;

    public readonly uint Width, Height;
    public readonly TextureFormat Format, DepthFormat;

    /// <summary>The samples a pixel the main frame draws with; one when the GPU can't multisample the format.</summary>
    public readonly SampleCount Samples;

    /// <summary>
    /// The main frame's multisampled target (MSAA): the tree draws into it and
    /// it resolves into the frame at each pass end, so screen copies and the
    /// window see smoothed edges. Null when the GPU can't multisample the format.
    /// Offscreen roots stay single-sampled.
    /// </summary>
    public readonly Texture? Msaa;

    private readonly Texture? _msaaDepth;
    private readonly Texture _depth;

    /// <summary>
    /// The depth effects read (xanadu.stage's <c>sampleDepth</c>): single-sampled, so it can be
    /// sampled, filled from the opaque pre-pass before a job whose effects read it.
    /// </summary>
    public readonly Texture DepthRead;

    /// <summary>The target as an effect's <c>screenTexture</c> reads it, mipmapped for blurred reads.</summary>
    public readonly Texture Screen;

    /// <summary>Linear with linear mips (MaxLod unclamped), so mipmapped textures such as glyph strips filter smoothly.</summary>
    public readonly Sampler Sampler;

    /// <summary>One white pixel, bound wherever a draw names no texture or one that isn't there.</summary>
    public readonly Texture White;

    public FrameTargets(GraphicsDevice device, TextureFormat format, uint width, uint height)
    {
        _device = device;
        Width = width;
        Height = height;
        Format = format;
        DepthFormat = device.SupportedDepthFormat;

        var uploader = new ResourceUploader(device);
        White = uploader.CreateTexture2D("white", [0xFFFFFFFFu], TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler, 1, 1);
        uploader.UploadAndWait();
        uploader.Dispose();

        uint levels = (uint)Math.Floor(Math.Log2(Math.Max(width, height))) + 1;
        Screen = Texture.Create2D(device, "screen copy", width, height, format,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget, levels);
        _mips = new ScreenMips(device, format, width, height, levels);

        Samples = Supported(device, format, WantedSamples);
        if (Samples != SampleCount.One)
        {
            Msaa = Texture.Create2D(device, "msaa frame", width, height, format, TextureUsageFlags.ColorTarget, 1, Samples);
            _msaaDepth = Texture.Create2D(device, "msaa depth", width, height, DepthFormat, TextureUsageFlags.DepthStencilTarget, 1, Samples);
        }

        _depth = Texture.Create2D(device, "depth", width, height, DepthFormat, TextureUsageFlags.DepthStencilTarget);
        DepthRead = Texture.Create2D(device, "depth read", width, height, DepthFormat,
            TextureUsageFlags.DepthStencilTarget | TextureUsageFlags.Sampler);
        Sampler = Sampler.Create(device, SamplerCreateInfo.LinearClamp);
    }

    /// <summary>The whole frame, as a scissor.</summary>
    public Rect FullClip => new() { X = 0, Y = 0, W = (int)Width, H = (int)Height };

    /// <summary>Bound in place of the glyph layer array before the atlas has a page.</summary>
    public Texture NoLayers =>
        _noLayers ??= Texture.Create2DArray(_device, "no glyph layers", 1, 1, 1, TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler);

    /// <summary>Makes a target the size of the frame, for an offscreen root.</summary>
    public Texture CreateTarget(string name) =>
        Texture.Create2D(_device, name, Width, Height, TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);

    /// <summary>A pass on <paramref name="target"/> and the depth buffer; with <paramref name="msaa"/>, on the multisampled frame, resolved into the target when it ends.</summary>
    public RenderPass BeginPass(CommandBuffer commands, Texture target, MwColor clear, bool msaa, bool load)
    {
        var depth = new DepthStencilTargetInfo
        {
            Texture = msaa ? _msaaDepth!.Handle : _depth.Handle,
            ClearDepth = 1f,
            LoadOp = load ? LoadOp.Load : LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.DontCare,
            StencilStoreOp = StoreOp.DontCare,
        };
        if (!msaa)
            return commands.BeginRenderPass(depth, load ? new ColorTargetInfo(target, LoadOp.Load) : new ColorTargetInfo(target, clear));

        // Resolved and kept: a screen copy mid-frame reads the resolve, and the pass then carries on drawing.
        return commands.BeginRenderPass(depth, new ColorTargetInfo
        {
            Texture = Msaa!,
            LoadOp = load ? LoadOp.Load : LoadOp.Clear,
            ClearColor = clear,
            StoreOp = StoreOp.ResolveAndStore,
            ResolveTexture = target,
        });
    }

    /// <summary>A depth-only pass on <see cref="DepthRead"/>, cleared.</summary>
    public RenderPass BeginDepthReadPass(CommandBuffer commands) => commands.BeginRenderPass(new DepthStencilTargetInfo
    {
        Texture = DepthRead.Handle,
        ClearDepth = 1f,
        LoadOp = LoadOp.Clear,
        StoreOp = StoreOp.Store,
        StencilLoadOp = LoadOp.DontCare,
        StencilStoreOp = StoreOp.DontCare,
    });

    /// <summary>
    /// The target as it stands into <see cref="Screen"/>, with Gaussian-filtered
    /// mips for blurred reads. Leaves fragment uniform slot 0 changed.
    /// </summary>
    public void CopyScreen(CommandBuffer commands, Texture target)
    {
        using GpuScope scope = commands.Scope("screen copy + mips");
        commands.Blit(target, Screen, Filter.Linear);
        _mips.Build(commands, Screen, Width, Height);
    }

    /// <summary>The most samples a pixel, up to <paramref name="wanted"/>, the GPU can render <paramref name="format"/> with.</summary>
    private static SampleCount Supported(GraphicsDevice device, TextureFormat format, SampleCount wanted)
    {
        for (SampleCount samples = wanted; samples > SampleCount.One; samples--)
        {
            if (SDL3.SDL.SDL_GPUTextureSupportsSampleCount(device.Handle, (SDL3.SDL.SDL_GPUTextureFormat)format, (SDL3.SDL.SDL_GPUSampleCount)samples))
                return samples;
        }

        return SampleCount.One;
    }

    public void Dispose()
    {
        White.Dispose();
        _noLayers?.Dispose();
        Screen.Dispose();
        Msaa?.Dispose();
        _msaaDepth?.Dispose();
        _depth.Dispose();
        DepthRead.Dispose();
        _mips.Dispose();
        Sampler.Dispose();
    }
}
