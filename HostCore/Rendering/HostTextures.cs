using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Storage;
using Xanadu.Systems.Canvas;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Host.Rendering;

/// <summary>
/// The host's <see cref="ITextureServer"/> (Docs/host.md §5.7):
/// uploads pixels into GPU textures the renderer draws by id, with mipmaps
/// made on the GPU, and loads image files through IRO.
/// </summary>
internal sealed class HostTextures(GraphicsDevice device, CanvasTree tree, CanvasRenderer renderer) : ITextureServer
{
    private readonly Dictionary<string, Handle> _loaded = new(StringComparer.Ordinal);

    public Handle Create(int width, int height, byte[] pixels, bool mipmaps)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height * 4)
            return default;

        Handle rid = tree.MintRid();
        renderer.Textures[rid.Id] = Upload(width, height, pixels.AsSpan(0, width * height * 4), TextureFormat.R8G8B8A8Unorm, 4, mipmaps);
        return rid;
    }

    public Handle CreateFloat(int width, int height, float[] values)
    {
        if (width <= 0 || height <= 0 || values.Length < width * height * 4)
            return default;

        Handle rid = tree.MintRid();
        renderer.Textures[rid.Id] = Upload(
            width, height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan(0, width * height * 4)),
            TextureFormat.R32G32B32A32Float, 16, mipmaps: false);
        return rid;
    }

    public unsafe Handle Load(string path)
    {
        if (path.Length == 0)
            return default;
        if (_loaded.TryGetValue(path, out Handle known))
            return known;

        Handle rid = default;
        string file = GamePaths.Full(path);
        if (File.Exists(file) && file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            // At the document's own size, as Godot's SVG import (scale 1) made it.
            try
            {
                Xanadu.Animation.RasterImage image = Xanadu.Animation.SvgRaster.Render(File.ReadAllText(file), 1f);
                rid = Create(image.Width, image.Height, image.Pixels, mipmaps: true);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                Console.Error.WriteLine($"SVG {path}: {exception.Message}");
            }
        }
        else if (File.Exists(file))
        {
            byte* data = ImageUtils.GetPixelDataFromBytes(File.ReadAllBytes(file), out uint width, out uint height, out uint size);
            if (data is not null)
            {
                var pixels = new byte[size];
                Marshal.Copy((IntPtr)data, pixels, 0, (int)size);
                ImageUtils.FreeBufferData((IntPtr)data);
                rid = Create((int)width, (int)height, pixels, mipmaps: true);
            }
        }

        if (rid.Id == 0)
            Console.Error.WriteLine($"Texture {path} could not be loaded.");
        _loaded[path] = rid;
        return rid;
    }

    public void Free(Handle texture)
    {
        if (renderer.Textures.Remove(texture.Id, out Texture? gpu))
            gpu.Dispose();
    }

    private Texture Upload(int width, int height, ReadOnlySpan<byte> pixels, TextureFormat format, int texelBytes, bool mipmaps)
    {
        uint levels = mipmaps ? (uint)Math.Floor(Math.Log2(Math.Max(width, height))) + 1 : 1;
        // Images stay colour targets as they always were (mipmaps blit); plain float data is only ever read.
        TextureUsageFlags usage = format == TextureFormat.R8G8B8A8Unorm
            ? TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget
            : TextureUsageFlags.Sampler;
        Texture texture = Texture.Create2D(device, "image", (uint)width, (uint)height, format, usage, levels);

        using var transfer = TransferBuffer.Create<byte>(device, TransferBufferUsage.Upload, (uint)(width * height * texelBytes));
        pixels.CopyTo(transfer.Map<byte>(false));
        transfer.Unmap();
        CommandBuffer commands = device.AcquireCommandBuffer();
        CopyPass copy = commands.BeginCopyPass();
        copy.UploadToTexture(new TextureTransferInfo(transfer), new TextureRegion(texture) { W = (uint)width, H = (uint)height }, false);
        commands.EndCopyPass(copy);
        if (levels > 1)
            SDL3.SDL.SDL_GenerateMipmapsForGPUTexture(commands.Handle, texture.Handle);
        Fence fence = device.SubmitAndAcquireFence(commands);
        device.WaitForFence(fence);
        device.ReleaseFence(fence);
        return texture;
    }
}
