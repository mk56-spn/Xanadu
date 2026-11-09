using Godot;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// The offscreen surfaces self-contained layers draw into
/// (docs/render-architecture.md §5.6): an offscreen target per layer, made when the
/// layer's pass is turned on and freed when it is turned off, so a plain layer
/// costs nothing.
/// </summary>
public interface IPassSurfaces
{
    /// <summary>The surface for a layer, made on first use and sized to the view.</summary>
    IPassSurface For(int layer, Vector2 size);

    /// <summary>Frees a layer's surface; harmless when it has none.</summary>
    void Release(int layer);

    /// <summary>Frees every surface, such as when the editor is left.</summary>
    void ReleaseAll();
}

/// <summary>One self-contained layer's canvas and the texture it renders to.</summary>
public interface IPassSurface
{
    /// <summary>The canvas item the layer's content draws into.</summary>
    Rid Canvas { get; }

    /// <summary>The texture to composite, premultiplied, as a canvas texture id; default when unavailable.</summary>
    Rid TextureId { get; }

    /// <summary>Resizes to the view; the content stays in the same coordinates.</summary>
    void Resize(Vector2 size);
}
