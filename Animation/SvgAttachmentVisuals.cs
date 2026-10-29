using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Items;
using Xanadu.Systems.Shaders;

namespace Xanadu.Animation;

/// <summary>
/// Rasterises each attachment's SVG group, and each chain's art, into canvas
/// items, and builds each effect's polygons with their textures and shaders.
/// </summary>
public sealed class SvgAttachmentVisuals(Rid canvas) : IAttachmentVisuals
{
    private readonly List<Visual> _visuals = new();
    private readonly List<ChainVisual> _chains = new();
    private readonly List<EffectVisual> _effects = new();

    public void Reload(XafPackage package)
    {
        Release();
        for (int i = 0; i < package.Attachments.Count; i++)
        {
            XafAttachment attachment = package.Attachments[i];
            if (!TryRaster(attachment.SvgPath, attachment.Group, out ImageTexture? texture, out Vector2 size))
                continue;

            // Higher layers draw in front; equal layers keep the attachment order.
            Rid item = CreateItem(
                texture!,
                size,
                Math.Clamp(attachment.ZOrder, XafAttachment.MinLayer, XafAttachment.MaxLayer));
            _visuals.Add(new Visual(i, item, texture!, new Rect2(-size / 2, size)));
        }

        int chainLayer = XafChainArt.Layer(package);
        for (int i = 0; i < package.Chains.Count; i++)
        {
            XafChain chain = package.Chains[i];
            if (chain.HasArt && TryRaster(chain.SvgPath, chain.Group, out ImageTexture? texture, out Vector2 size))
                _chains.Add(new ChainVisual(i, texture!, size, chainLayer));
        }

        for (int i = 0; i < package.Effects.Count; i++)
        {
            XafEffect effect = package.Effects[i];
            int layer = Math.Clamp(effect.ZOrder, XafAttachment.MinLayer, XafAttachment.MaxLayer);
            foreach (XifPrimitive primitive in effect.Item.Primitives)
            {
                if (primitive.IsPolygon && TryPolygon(primitive, effect.Item, layer) is { } visual)
                    _effects.Add(visual with { Index = i });
            }
        }
    }

    public void ApplyEffects(XafPose pose, Transform2D[] effects, Transform2D placement)
    {
        foreach (EffectVisual visual in _effects)
        {
            float alpha = visual.Index < pose.Effects.Length ? pose.Effects[visual.Index].Alpha : 0f;
            RenderingServer.CanvasItemSetVisible(visual.Item, alpha > 0f && visual.Index < effects.Length);
            if (alpha <= 0f || visual.Index >= effects.Length)
                continue;

            RenderingServer.CanvasItemSetTransform(visual.Item, placement * effects[visual.Index]);
            RenderingServer.CanvasItemSetModulate(visual.Item, new Color(1f, 1f, 1f, alpha));
        }
    }

    public void Apply(Transform2D[] attachments, Transform2D placement)
    {
        foreach (Visual visual in _visuals)
        {
            if (visual.Index >= attachments.Length)
                continue;

            RenderingServer.CanvasItemSetTransform(
                visual.Item,
                placement * attachments[visual.Index]);
        }
    }

    public void ApplyChains(XafPackage package, XafChainSimulation simulation, Transform2D placement)
    {
        foreach (ChainVisual visual in _chains)
        {
            if (visual.Index >= package.Chains.Count)
                continue;

            XafChain chain = package.Chains[visual.Index];
            IReadOnlyList<Vector2> points = simulation.Points(visual.Index);
            int segments = Math.Max(0, points.Count - 1);

            // Segments can be added or removed while editing, so the pieces follow the points.
            while (visual.Items.Count < segments)
                visual.Items.Add(CreateItem(visual.Texture, visual.Size, visual.Layer));
            while (visual.Items.Count > segments)
            {
                RenderingServer.FreeRid(visual.Items[^1]);
                visual.Items.RemoveAt(visual.Items.Count - 1);
            }

            for (int s = 0; s < segments; s++)
            {
                RenderingServer.CanvasItemSetTransform(
                    visual.Items[s],
                    placement * XafChainArt.Segment(points[s], points[s + 1], s, segments, chain.Width, visual.Size));
            }
        }
    }

    public Rect2 Bounds(Transform2D[] attachments)
    {
        Rect2? merged = null;
        foreach (Visual visual in _visuals)
        {
            if (visual.Index >= attachments.Length)
                continue;

            Rect2 placed = attachments[visual.Index] * visual.Bounds;
            merged = merged is { } current ? current.Merge(placed) : placed;
        }

        return merged ?? new Rect2();
    }

    public void Release()
    {
        foreach (Visual visual in _visuals)
        {
            RenderingServer.FreeRid(visual.Item);
            visual.Texture.Dispose();
        }
        _visuals.Clear();

        foreach (ChainVisual chain in _chains)
        {
            foreach (Rid item in chain.Items)
                RenderingServer.FreeRid(item);
            chain.Texture.Dispose();
        }
        _chains.Clear();

        foreach (EffectVisual effect in _effects)
            RenderingServer.FreeRid(effect.Item);
        _effects.Clear();
    }

    /// <summary>
    /// One polygon primitive as a hidden canvas item: its outline triangulated,
    /// tinted by its fill, textured across its bounds when its ref names a
    /// texture, and drawn with its shader, or the item's, when one is common.
    /// An outline that cannot be triangulated is skipped rather than drawn wrong.
    /// </summary>
    private EffectVisual? TryPolygon(XifPrimitive primitive, Xif item, int layer)
    {
        Vector2[] points = primitive.Points.ToArray();
        int[] triangles = points.Length >= 3 ? Geometry2D.TriangulatePolygon(points) : Array.Empty<int>();
        if (triangles.Length == 0)
            return null;

        Texture2D? texture = primitive.Ref.Length > 0 && ResourceLoader.Exists(primitive.Ref)
            ? ResourceLoader.Load<Texture2D>(primitive.Ref)
            : null;
        var bounds = new Rect2(points[0], Vector2.Zero);
        foreach (Vector2 point in points)
            bounds = bounds.Expand(point);
        var uvs = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            uvs[i] = new Vector2(
                bounds.Size.X > 0f ? (points[i].X - bounds.Position.X) / bounds.Size.X : 0f,
                bounds.Size.Y > 0f ? (points[i].Y - bounds.Position.Y) / bounds.Size.Y : 0f);
        }

        var colors = new Color[points.Length];
        Array.Fill(colors, primitive.Fill);

        Rid canvasItem = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(canvasItem, canvas);
        RenderingServer.CanvasItemSetZIndex(canvasItem, layer);
        RenderingServer.CanvasItemSetVisible(canvasItem, false);
        RenderingServer.CanvasItemAddTriangleArray(canvasItem, triangles, points, colors, uvs,
            texture: texture?.GetRid() ?? default);

        string shader = !primitive.Shader.IsNone ? primitive.Shader.Path : item.PrimaryCommonShader;
        if (shader.Length > 0 && CommonShaderMaterials.MaterialOf(shader) is { IsValid: true } material)
            RenderingServer.CanvasItemSetMaterial(canvasItem, material);
        return new EffectVisual(-1, canvasItem, texture);
    }

    /// <summary>
    /// Rasterises one group of an SVG file at one pixel per unit. A missing or
    /// malformed file is skipped, since it must not stop rig editing.
    /// </summary>
    private static bool TryRaster(string svgPath, string group, out ImageTexture? texture, out Vector2 size)
    {
        texture = null;
        size = Vector2.Zero;
        string path = ProjectSettings.GlobalizePath(svgPath);
        if (!System.IO.File.Exists(path))
            return false;

        try
        {
            string svg = XafSvgDocument.IsolateGroup(System.IO.File.ReadAllText(path), group);
            var image = new Image();
            Error error = image.LoadSvgFromString(svg, 1f);
            if (error != Error.Ok)
                throw new InvalidOperationException(error.ToString());

            texture = ImageTexture.CreateFromImage(image);
            size = image.GetSize();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private Rid CreateItem(ImageTexture texture, Vector2 size, int zIndex)
    {
        Rid item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(item, canvas);
        RenderingServer.CanvasItemSetZIndex(item, zIndex);
        RenderingServer.CanvasItemAddTextureRect(item, new Rect2(-size / 2, size), texture.GetRid());
        return item;
    }

    private sealed record Visual(int Index, Rid Item, ImageTexture Texture, Rect2 Bounds);

    /// <summary>One polygon of an effect; the texture is held so it outlives the canvas item drawing it.</summary>
    private sealed record EffectVisual(int Index, Rid Item, Texture2D? Texture);

    /// <summary>
    /// One chain's art: the texture shared by its pieces, the layer they all draw
    /// on, and a canvas item per segment.
    /// </summary>
    private sealed record ChainVisual(int Index, ImageTexture Texture, Vector2 Size, int Layer)
    {
        public List<Rid> Items { get; } = new();
    }
}
