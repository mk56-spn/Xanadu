using System;
using System.Collections.Generic;
using Xanadu.Systems.Shaders;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Host.Rendering;

/// <summary>
/// Self-contained layers' surfaces in the host (Docs/render-architecture.md
/// §5.4, Docs/host.md §5.3): a parentless canvas item drawn into a
/// frame-sized texture before the frame, made when a layer's pass turns on and
/// freed when it turns off. The texture is premultiplied.
/// </summary>
internal sealed class HostPassSurfaces(CanvasTree tree, CanvasRenderer renderer) : IPassSurfaces, IDisposable
{
    private readonly Dictionary<int, Surface> _surfaces = new();

    public IPassSurface For(int layer, Vector2 size)
    {
        if (!_surfaces.TryGetValue(layer, out Surface? surface))
        {
            Handle canvas = tree.CreateItem();
            Texture target = renderer.CreateTarget($"layer {layer}");
            Handle texture = tree.MintRid();
            renderer.Textures[texture.Id] = target;
            renderer.AddOffscreen(tree.Item(canvas)!, target);
            surface = new Surface(canvas, texture, target);
            _surfaces[layer] = surface;
        }

        return surface;
    }

    public void Release(int layer)
    {
        if (_surfaces.Remove(layer, out Surface? surface))
            Free(surface);
    }

    public void ReleaseAll()
    {
        foreach (Surface surface in _surfaces.Values)
            Free(surface);
        _surfaces.Clear();
    }

    public void Dispose() => ReleaseAll();

    private void Free(Surface surface)
    {
        if (tree.Item(surface.Canvas) is { } root)
            renderer.RemoveOffscreen(root);
        tree.Free(surface.Canvas);
        renderer.Textures.Remove(surface.TextureId.Id);
        surface.Target.Dispose();
    }

    private sealed class Surface(Handle canvas, Handle texture, Texture target) : IPassSurface
    {
        public Handle Canvas { get; } = canvas;
        public Handle TextureId { get; } = texture;
        public Texture Target { get; } = target;

        // The surface is always frame-sized: content keeps its coordinates.
        public void Resize(Vector2 size) { }
    }
}
