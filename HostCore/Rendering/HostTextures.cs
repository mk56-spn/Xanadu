using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Godot;
using MoonWorks.Graphics;
using Xanadu.Storage;
using Xanadu.Systems.Canvas;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Host.Rendering;

/// <summary>
/// The host's <see cref="ITextureServer"/> (docs/moonworks-port.md step 8):
/// uploads pixels into GPU textures the renderer draws by id, with mipmaps
/// made on the GPU, and loads image files through IRO.
/// </summary>
internal sealed class HostTextures(GraphicsDevice device, CanvasTree tree, CanvasRenderer renderer) : ITextureServer
{
    private readonly Dictionary<string, Rid> _loaded = new(StringComparer.Ordinal);

    public Rid Create(int width, int height, byte[] pixels, bool mipmaps)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height * 4)
            return default;

        Rid rid = tree.MintRid();
        renderer.Textures[rid.Id] = Upload(width, height, pixels, mipmaps);
        return rid;
    }

    public unsafe Rid Load(string path)
    {
        if (path.Length == 0)
            return default;
        if (_loaded.TryGetValue(path, out Rid known))
            return known;

        Rid rid = default;
        string file = GamePaths.Globalize(path);
        if (File.Exists(file))
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

    public void Free(Rid texture)
    {
        if (renderer.Textures.Remove(texture.Id, out Texture? gpu))
            gpu.Dispose();
    }

    private Texture Upload(int width, int height, byte[] pixels, bool mipmaps)
    {
        uint levels = mipmaps ? (uint)Math.Floor(Math.Log2(Math.Max(width, height))) + 1 : 1;
        Texture texture = Texture.Create2D(device, "image", (uint)width, (uint)height, TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget, levels);

        using var transfer = TransferBuffer.Create<byte>(device, TransferBufferUsage.Upload, (uint)(width * height * 4));
        pixels.AsSpan(0, width * height * 4).CopyTo(transfer.Map<byte>(false));
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
