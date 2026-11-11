using System;
using System.IO;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Texture = MoonWorks.Graphics.Texture;
using Xanadu.Slang;

namespace Xanadu.Host.Rendering;

/// <summary>
/// Fills the screen copy's mip levels with a Gaussian-filtered pyramid
/// (<c>Shaders/screen_mips.slang</c>), as Godot built its screen mips, so a
/// frosted panel can read one blurry level and still look smooth.
/// </summary>
/// <remarks>
/// A level can't be drawn while another level of the same texture is bound to
/// be read, so each level is drawn into a scratch texture of its own size (the
/// next level reads that) and then copied into the screen copy's level.
/// Only the first <see cref="Levels"/> levels are kept up to date; the frost
/// reads around level 3.
/// </remarks>
internal sealed class ScreenMips : IDisposable
{
    /// <summary>How many levels below the full-size copy are rebuilt each copy.</summary>
    public const int Levels = 6;

    [StructLayout(LayoutKind.Sequential)]
    private struct Down { public float TexelX, TexelY, Pad0, Pad1; }

    private readonly GraphicsPipeline _halve;
    private readonly Sampler _sampler;
    private readonly Texture[] _scratch;
    private readonly uint[] _widths, _heights;

    public ScreenMips(GraphicsDevice device, TextureFormat format, uint width, uint height, uint levels)
    {
        string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "screen_mips.slang");
        Shader vertex = Shader.Create(device, SlangShaders.Compile(module, "fullscreen", "vertex"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex });
        Shader fragment = Shader.Create(device, SlangShaders.Compile(module, "halve", "fragment"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Fragment, NumSamplers = 1, NumUniformBuffers = 1 });
        _halve = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            VertexShader = vertex,
            FragmentShader = fragment,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new ColorTargetDescription { Format = format, BlendState = ColorTargetBlendState.NoBlend }],
            },
        });
        vertex.Dispose();
        fragment.Dispose();

        _sampler = Sampler.Create(device, SamplerCreateInfo.LinearClamp);

        int count = (int)Math.Min(Levels, levels - 1);
        _scratch = new Texture[count];
        _widths = new uint[count];
        _heights = new uint[count];
        for (int i = 0; i < count; i++)
        {
            _widths[i] = Math.Max(1, width >> (i + 1));
            _heights[i] = Math.Max(1, height >> (i + 1));
            _scratch[i] = Texture.Create2D(device, $"screen mip {i + 1}", _widths[i], _heights[i], format,
                TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);
        }
    }

    /// <summary>
    /// Rebuilds the levels from level 0 of <paramref name="screen"/>. Call
    /// between render passes; it leaves fragment uniform slot 0 changed.
    /// </summary>
    public void Build(CommandBuffer commands, Texture screen, uint width, uint height)
    {
        Texture source = screen;
        uint sourceWidth = width, sourceHeight = height;
        for (int i = 0; i < _scratch.Length; i++)
        {
            RenderPass pass = commands.BeginRenderPass(new ColorTargetInfo(_scratch[i], LoadOp.DontCare));
            pass.BindGraphicsPipeline(_halve);
            pass.BindFragmentSamplers(new TextureSamplerBinding(source, _sampler));
            commands.PushFragmentUniformData(new Down { TexelX = 1f / sourceWidth, TexelY = 1f / sourceHeight });
            pass.DrawPrimitives(3, 1, 0, 0);
            commands.EndRenderPass(pass);

            source = _scratch[i];
            sourceWidth = _widths[i];
            sourceHeight = _heights[i];
        }

        CopyPass copy = commands.BeginCopyPass();
        for (int i = 0; i < _scratch.Length; i++)
            copy.CopyTextureToTexture(
                new TextureLocation(_scratch[i]),
                new TextureLocation(screen) { MipLevel = (uint)(i + 1) },
                _widths[i], _heights[i], 1, false);
        commands.EndCopyPass(copy);
    }

    public void Dispose()
    {
        _halve.Dispose();
        _sampler.Dispose();
        foreach (Texture texture in _scratch)
            texture.Dispose();
    }
}
