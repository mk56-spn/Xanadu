using System.IO;
using MoonWorks.Graphics;
using Xanadu.Core.Storage;
using Xanadu.Core.Theme;
using Color = Xanadu.Core.Maths.Color;
using MwColor = MoonWorks.Graphics.Color;
using SDL = SDL3.SDL;

namespace Xanadu.Host;

/// <summary>
/// What the window shows while the host loads: the game's icon on the
/// background colour, with a progress bar under it. Drawn with clears and
/// blits only, so it needs nothing the loading is still making. Each
/// <see cref="Show"/> presents a frame and lets the window answer the desktop,
/// so a slow step (a cold shader compile) doesn't read as a hung window. The
/// last frame shown stays up until the first real frame replaces it.
/// </summary>
internal sealed class HostSplash : IDisposable
{
    /// <summary>Presents closer together than this are skipped: a warm launch's quick steps needn't each wait on vsync.</summary>
    private const double MinFrameMs = 30;

    private const uint BarWidth = 320;
    private const uint BarHeight = 4;
    private const uint IconGap = 32;

    private readonly GraphicsDevice _device;
    private readonly MoonWorks.Window _window;
    private readonly Texture? _icon;
    private readonly Texture _track;
    private readonly Texture _fill;
    private long _lastShown;

    public HostSplash(GraphicsDevice device, MoonWorks.Window window)
    {
        _device = device;
        _window = window;
        Color background = Palette.Background;
        using var uploader = new ResourceUploader(device);
        _icon = LoadIcon(uploader, background);
        _track = Solid(uploader, Palette.SurfaceDeep);
        _fill = Solid(uploader, Palette.Accent);
        uploader.UploadAndWait();
    }

    /// <summary>Draws the splash with the bar at <paramref name="progress"/> (0 to 1) and presents it.</summary>
    public void Show(float progress, bool force = false)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!force && _lastShown != 0 && System.Diagnostics.Stopwatch.GetElapsedTime(_lastShown, now).TotalMilliseconds < MinFrameMs)
            return;
        _lastShown = now;

        CommandBuffer commands = _device.AcquireCommandBuffer();
        Texture? swapchain = commands.AcquireSwapchainTexture(_window);
        if (swapchain is not null)
            Draw(commands, swapchain, progress);
        _device.Submit(commands);

        // Loading runs before the game loop, which would otherwise be the one answering the desktop.
        SDL.SDL_PumpEvents();
    }

    /// <summary>Records the splash into <paramref name="target"/>, filling it.</summary>
    private void Draw(CommandBuffer commands, Texture target, float progress)
    {
        Color background = Palette.Background;
        commands.EndRenderPass(commands.BeginRenderPass(
            new ColorTargetInfo(target, new MwColor(background.R, background.G, background.B, 1f))));

        uint width = target.Width, height = target.Height;
        uint iconSize = _icon is null ? 0 : Math.Min(_icon.Width, height / 3);
        uint barWidth = Math.Min(BarWidth, width * 2 / 3);
        uint top = (height - iconSize - IconGap - BarHeight) / 2;
        if (_icon is not null)
            Blit(commands, _icon, target, (width - iconSize) / 2, top, iconSize, iconSize);

        uint barX = (width - barWidth) / 2, barY = top + iconSize + IconGap;
        Blit(commands, _track, target, barX, barY, barWidth, BarHeight);
        uint filled = (uint)(barWidth * Math.Clamp(progress, 0f, 1f));
        if (filled > 0)
            Blit(commands, _fill, target, barX, barY, filled, BarHeight);
    }

    private static void Blit(CommandBuffer commands, Texture source, Texture destination, uint x, uint y, uint width, uint height) =>
        commands.Blit(new BlitInfo
        {
            Source = new BlitRegion(source),
            Destination = new BlitRegion { Texture = destination.Handle, X = x, Y = y, W = width, H = height },
            LoadOp = LoadOp.Load,
            Filter = Filter.Linear,
        });

    private static Texture Solid(ResourceUploader uploader, Color color)
    {
        ReadOnlySpan<byte> pixel = [Byte(color.R), Byte(color.G), Byte(color.B), 255];
        return uploader.CreateTexture2D("splash solid", pixel, TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler, 1, 1);
    }

    /// <summary>The icon already laid over the background: a blit copies pixels, it doesn't blend them.</summary>
    private static unsafe Texture? LoadIcon(ResourceUploader uploader, Color background)
    {
        string path = GamePaths.Full("Assets/icon.png");
        if (!File.Exists(path))
            return null;

        byte* pixels = ImageUtils.GetPixelDataFromBytes(File.ReadAllBytes(path), out uint width, out uint height, out _);
        if (pixels == null)
            return null;
        try
        {
            byte[] rgba = new byte[width * height * 4];
            byte[] under = [Byte(background.R), Byte(background.G), Byte(background.B)];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                int alpha = pixels[i + 3];
                for (int c = 0; c < 3; c++)
                    rgba[i + c] = (byte)((pixels[i + c] * alpha + under[c] * (255 - alpha) + 127) / 255);
                rgba[i + 3] = 255;
            }
            return uploader.CreateTexture2D("splash icon", rgba, TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler, width, height);
        }
        finally
        {
            ImageUtils.FreePixelData(pixels);
        }
    }

    private static byte Byte(float channel) => (byte)Math.Clamp(MathF.Round(channel * 255f), 0f, 255f);

    public void Dispose()
    {
        _icon?.Dispose();
        _track.Dispose();
        _fill.Dispose();
    }
}
