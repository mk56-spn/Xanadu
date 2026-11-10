using System;

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
    Handle Create(int width, int height, byte[] pixels, bool mipmaps);

    /// <summary>
    /// A texture of plain numbers, four 32-bit floats a texel from <paramref name="values"/>,
    /// rows top first, no mipmaps: data a shader reads texel by texel (<c>Load</c>), never filtered.
    /// </summary>
    Handle CreateFloat(int width, int height, float[] values);

    /// <summary>An image file (a <c>res://</c> path), loaded once and kept; the default id when it can't be read.</summary>
    Handle Load(string path);

    /// <summary>Frees a texture made by <see cref="Create"/>.</summary>
    void Free(Handle texture);
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
    public Handle Create(int width, int height, byte[] pixels, bool mipmaps) => default;

    public Handle CreateFloat(int width, int height, float[] values) => default;

    public Handle Load(string path) => default;

    public void Free(Handle texture) { }
}
