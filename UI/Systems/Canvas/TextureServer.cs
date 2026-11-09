using System;
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

/// <summary>The running host's <see cref="ITextureServer"/>; one that keeps nothing until a host calls <see cref="Use"/>.</summary>
public static class TextureServer
{
    public static ITextureServer Current { get; private set; } = new HeadlessTextureServer();

    public static void Use(ITextureServer server) => Current = server ?? throw new ArgumentNullException(nameof(server));
}

/// <summary>Makes no textures: every id is the default one, which draws as plain colour.</summary>
public sealed class HeadlessTextureServer : ITextureServer
{
    public Rid Create(int width, int height, byte[] pixels, bool mipmaps) => default;

    public Rid Load(string path) => default;

    public void Free(Rid texture) { }
}
