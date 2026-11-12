using System;
using System.IO;
using MoonWorks.Graphics;
using SDL3;
using Xanadu.Storage;

namespace Xanadu.Host;

/// <summary>
/// Gives the window the game's icon (<c>Assets/icon.png</c>, made from
/// <c>Assets/icon.svg</c>). On Wayland SDL sends it through
/// <c>xdg_toplevel_icon_v1</c>, which KWin supports; compositors without it keep
/// their generic icon.
/// </summary>
internal static class HostWindowIcon
{
    public static unsafe void Apply(IntPtr window)
    {
        string path = GamePaths.Full("Assets/icon.png");
        if (!File.Exists(path))
            return;

        byte* pixels = ImageUtils.GetPixelDataFromBytes(File.ReadAllBytes(path), out uint width, out uint height, out _);
        if (pixels == null)
            return;
        try
        {
            SDL.SDL_Surface* surface = SDL.SDL_CreateSurfaceFrom((int)width, (int)height,
                SDL.SDL_PixelFormat.SDL_PIXELFORMAT_RGBA32, (IntPtr)pixels, (int)width * 4);
            if (surface == null)
                return;
            if (!SDL.SDL_SetWindowIcon(window, (IntPtr)surface))
                Console.WriteLine($"Window icon not set: {SDL.SDL_GetError()}");
            SDL.SDL_DestroySurface((IntPtr)surface);
        }
        finally
        {
            ImageUtils.FreePixelData(pixels);
        }
    }
}
