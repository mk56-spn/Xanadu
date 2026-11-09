using System;
using MoonWorks.Graphics;
using Xanadu.Systems.Canvas;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Host.Text;

/// <summary>
/// Builds a <see cref="GlyphAtlas"/>'s strip on the GPU the way the Godot build
/// did: one square cell per glyph, white with coverage in alpha, each glyph
/// scaled so its larger side fills <see cref="GlyphAtlas.Fill"/> of the cell and
/// its ink centred, plus mipmaps when asked.
/// </summary>
internal static class GlyphStrips
{
    public static Texture Build(GraphicsDevice device, HostText text, GlyphAtlas atlas)
    {
        int cell = atlas.CellSize, width = cell * Math.Max(1, atlas.Count);
        var pixels = new uint[width * cell];
        Array.Fill(pixels, 0x00FFFFFFu);
        float room = cell * atlas.Fill;
        for (int i = 0; i < atlas.Count; i++)
        {
            GlyphBitmap measured = text.RenderInk(atlas.Glyphs[i], cell);
            if (measured.Width == 0 || measured.Height == 0)
                continue;

            float scale = room / Math.Max(measured.Width, measured.Height);
            GlyphBitmap ink = text.RenderInk(atlas.Glyphs[i], Math.Max(1, (int)MathF.Floor(cell * scale)));
            int x0 = cell * i + (cell - ink.Width) / 2, y0 = (cell - ink.Height) / 2;
            for (int y = 0; y < ink.Height; y++)
                for (int x = 0; x < ink.Width; x++)
                {
                    int px = x0 + x, py = y0 + y;
                    if (px < cell * i || px >= cell * (i + 1) || py < 0 || py >= cell)
                        continue;
                    pixels[py * width + px] = 0x00FFFFFFu | (uint)ink.Coverage[y * ink.Width + x] << 24;
                }
        }

        uint levels = atlas.Mipmaps ? (uint)Math.Floor(Math.Log2(Math.Max(width, cell))) + 1 : 1;
        Texture texture = Texture.Create2D(device, "glyph strip", (uint)width, (uint)cell,
            TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget, levels);

        using var transfer = TransferBuffer.Create<uint>(device, TransferBufferUsage.Upload, (uint)pixels.Length);
        pixels.CopyTo(transfer.Map<uint>(false));
        transfer.Unmap();
        CommandBuffer commands = device.AcquireCommandBuffer();
        CopyPass copy = commands.BeginCopyPass();
        copy.UploadToTexture(new TextureTransferInfo(transfer), new TextureRegion(texture) { W = (uint)width, H = (uint)cell }, false);
        commands.EndCopyPass(copy);
        if (levels > 1)
            SDL3.SDL.SDL_GenerateMipmapsForGPUTexture(commands.Handle, texture.Handle);
        Fence fence = device.SubmitAndAcquireFence(commands);
        device.WaitForFence(fence);
        device.ReleaseFence(fence);
        return texture;
    }
}
