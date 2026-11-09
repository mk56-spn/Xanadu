using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// Makes the textures canvas items draw with, from pixels or image files.
/// Whatever is drawn takes the returned id as a draw command's texture.
/// </summary>
public interface ITextureServer
{
    /// <summary>
    /// A texture from straight-alpha RGBA8 <paramref name="pixels"/>, rows top
    /// first; with <paramref name="mipmaps"/> it filters smoothly when drawn small.
    /// </summary>
    Rid Create(int width, int height, byte[] pixels, bool mipmaps);

    /// <summary>An image file (a <c>res://</c> path), loaded once and kept; the default id when it can't be read.</summary>
    Rid Load(string path);

    /// <summary>Frees a texture made by <see cref="Create"/>.</summary>
    void Free(Rid texture);
}

/// <summary>The running host's <see cref="ITextureServer"/>: Godot's until the MoonWorks host calls <see cref="Use"/>.</summary>
public static class TextureServer
{
    public static ITextureServer Current { get; private set; } = new GodotTextureServer();

    public static void Use(ITextureServer server) => Current = server ?? throw new ArgumentNullException(nameof(server));
}

/// <summary>Textures as Godot resources, held here so the GC can't free them under a canvas item.</summary>
public sealed class GodotTextureServer : ITextureServer
{
    private readonly Dictionary<Rid, Texture2D> _held = new();

    public Rid Create(int width, int height, byte[] pixels, bool mipmaps)
    {
        Image image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels);
        if (mipmaps)
            image.GenerateMipmaps();
        ImageTexture texture = ImageTexture.CreateFromImage(image);
        _held[texture.GetRid()] = texture;
        return texture.GetRid();
    }

    public Rid Load(string path)
    {
        if (path.Length == 0 || !ResourceLoader.Exists(path))
            return default;

        Texture2D texture = ResourceLoader.Load<Texture2D>(path);
        _held[texture.GetRid()] = texture;
        return texture.GetRid();
    }

    public void Free(Rid texture)
    {
        if (_held.Remove(texture, out Texture2D? held))
            held.Dispose();
    }
}
