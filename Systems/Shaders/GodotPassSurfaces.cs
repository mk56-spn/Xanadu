using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// The offscreen surfaces self-contained layers draw into
/// (docs/render-architecture.md §5.6): a SubViewport per layer, made when the
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

    /// <summary>The texture to composite, premultiplied; null when unavailable.</summary>
    Texture2D? Texture { get; }

    /// <summary>Resizes to the view; the content stays in the same coordinates.</summary>
    void Resize(Vector2 size);
}

/// <summary>SubViewport-backed pass surfaces, parented to the scene's canvas host.</summary>
public sealed class GodotPassSurfaces(Node host) : IPassSurfaces, IDisposable
{
    private readonly Dictionary<int, Surface> _surfaces = new();

    public IPassSurface For(int layer, Vector2 size)
    {
        if (!_surfaces.TryGetValue(layer, out Surface? surface))
        {
            surface = new Surface(host);
            _surfaces[layer] = surface;
        }

        surface.Resize(size);
        return surface;
    }

    public void Release(int layer)
    {
        if (_surfaces.Remove(layer, out Surface? surface))
            surface.Dispose();
    }

    public void ReleaseAll()
    {
        foreach (Surface surface in _surfaces.Values)
            surface.Dispose();
        _surfaces.Clear();
    }

    public void Dispose() => ReleaseAll();

    private sealed class Surface : IPassSurface, IDisposable
    {
        private readonly SubViewport _viewport = new()
        {
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Disable3D = true,
        };

        private readonly Node2D _content = new();

        public Surface(Node host)
        {
            host.AddChild(_viewport);
            _viewport.AddChild(_content);
        }

        public Rid Canvas => _content.GetCanvasItem();

        public Texture2D? Texture => GodotObject.IsInstanceValid(_viewport) ? _viewport.GetTexture() : null;

        public void Resize(Vector2 size)
        {
            var pixels = new Vector2I(
                Math.Max(1, (int)MathF.Ceiling(size.X)),
                Math.Max(1, (int)MathF.Ceiling(size.Y)));
            if (_viewport.Size != pixels)
                _viewport.Size = pixels;
        }

        public void Dispose()
        {
            _viewport.GetParent()?.RemoveChild(_viewport);
            _viewport.QueueFree();
        }
    }
}
