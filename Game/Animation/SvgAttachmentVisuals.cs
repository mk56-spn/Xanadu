using System;
using System.Collections.Generic;
using Xanadu.Systems.Canvas;
using Xanadu.Items;
using Xanadu.Layering;
using Xanadu.Rendering;
using Xanadu.Systems;
using Xanadu.Systems.Shaders;

namespace Xanadu.Animation;

/// <summary>
/// Rasterises each attachment's SVG group, and each chain's art, into canvas
/// items, and builds each effect's polygons with their textures and shaders.
/// </summary>
/// <param name="canvas">The canvas item the art hangs from.</param>
/// <param name="containLayers">
/// Keep all the art at the canvas's own z and order its layers by draw index, so
/// no layer can reach past the canvas to whatever is drawn above it. Without this
/// a layer is a z index relative to the canvas, as the game's characters use.
/// </param>
public sealed partial class SvgAttachmentVisuals(Handle canvas, bool containLayers = false) : IAttachmentVisuals
{
    private readonly List<Visual> _visuals = new();
    private readonly List<ChainVisual> _chains = new();
    private readonly List<EffectVisual> _effects = new();
    private readonly List<ClothVisual> _cloths = new();

    // Godot sorts the items of one layer by draw index. AnimatorDrawOrder owns
    // those indices: attachments in list order, then cloth, chains and effects,
    // so a dress on the legs' layer covers the legs and stays under the next
    // layer up (Docs/render-architecture.md step 12).

    /// <summary>Pixels per unit art is rasterised at; the preview zooms well past one.</summary>
    private const float ArtScale = 4f;

    public void Reload(XafPackage package)
    {
        Release();
        for (int i = 0; i < package.Attachments.Count; i++)
        {
            XafAttachment attachment = package.Attachments[i];
            for (int art = 0; art < attachment.ArtCount; art++)
            {
                XafArt source = attachment.Art(art);
                if (!TryRaster(source.SvgPath, source.Group, out ArtTexture? texture, out Vector2 size))
                    continue;

                // Higher layers draw in front; equal layers keep the attachment order.
                // Only the attachment's own art shows until a pose says otherwise.
                Handle item = CreateItem(texture!, size, attachment.ZOrder, AnimatorDrawOrder.Attachment(i));
                CanvasServer.Current.SetVisible(item, art == 0);
                _visuals.Add(new Visual(i, art, item, texture!, new Rect2(-size / 2, size)));
            }
        }

        for (int i = 0; i < package.Chains.Count; i++)
        {
            XafChain chain = package.Chains[i];
            if (chain.HasArt && TryRaster(chain.SvgPath, chain.Group, out ArtTexture? texture, out Vector2 size))
                _chains.Add(new ChainVisual(i, texture!, size, XafChainArt.LayerOf(package, chain)));
        }

        for (int i = 0; i < package.Cloths.Count; i++)
        {
            XafCloth cloth = package.Cloths[i];
            Handle item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(item, canvas);
            SetLayer(item, cloth.ZOrder, AnimatorDrawOrder.Cloth(i));
            _cloths.Add(new ClothVisual(i, item));
        }

        ReloadEffects(package);
    }

    public void ReloadEffects(XafPackage package)
    {
        foreach (EffectVisual visual in _effects)
            CanvasServer.Current.Free(visual.Item);
        _effects.Clear();

        for (int i = 0; i < package.Effects.Count; i++)
        {
            XafEffect effect = package.Effects[i];
            int layer = DrawOrder.ClampLayer(effect.ZOrder);
            for (int primitive = 0; primitive < effect.Item.Primitives.Count; primitive++)
            {
                XifPrimitive item = effect.Item.Primitives[primitive];
                if (item.IsPolygon &&
                    TryPolygon(item, effect.Item, layer, AnimatorDrawOrder.Effect(i, primitive)) is { } visual)
                {
                    _effects.Add(visual with { Index = i });
                }
            }
        }

        ReloadParticles(package);
    }

    public void ApplyEffects(XafPose pose, Transform2D[] effects, Transform2D placement)
    {
        ApplyParticles(pose, effects, placement);
        foreach (EffectVisual visual in _effects)
        {
            float alpha = visual.Index < pose.Effects.Length ? pose.Effects[visual.Index].Alpha : 0f;
            CanvasServer.Current.SetVisible(visual.Item, alpha > 0f && visual.Index < effects.Length);
            if (alpha <= 0f || visual.Index >= effects.Length)
                continue;

            CanvasServer.Current.SetTransform(visual.Item, placement * effects[visual.Index]);
            CanvasServer.Current.SetModulate(visual.Item, new Color(1f, 1f, 1f, alpha));
            visual.Alpha = alpha;
        }
    }

    public void Apply(Transform2D[] attachments, Transform2D placement)
    {
        foreach (Visual visual in _visuals)
        {
            if (visual.Index >= attachments.Length)
                continue;

            CanvasServer.Current.SetTransform(
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
            if (chain.Sheet)
            {
                DrawSheet(visual, chain, points, placement);
                continue;
            }
            int segments = Math.Max(0, points.Count - 1);

            // Segments can be added or removed while editing, so the pieces follow the points.
            while (visual.Items.Count < segments)
            {
                Handle piece = CreateItem(
                    visual.Texture,
                    visual.Size,
                    visual.Layer,
                    AnimatorDrawOrder.Chain(visual.Index, visual.Items.Count));
                visual.Items.Add(piece);
            }
            while (visual.Items.Count > segments)
            {
                CanvasServer.Current.Free(visual.Items[^1]);
                visual.Items.RemoveAt(visual.Items.Count - 1);
            }

            for (int s = 0; s < segments; s++)
            {
                CanvasServer.Current.SetTransform(
                    visual.Items[s],
                    placement * XafChainArt.Segment(points[s], points[s + 1], s, segments, chain.Width, visual.Size));
            }
        }
    }

    /// <summary>A sheet chain: one item, redrawn each frame as the strip along its points.</summary>
    private void DrawSheet(ChainVisual visual, XafChain chain, IReadOnlyList<Vector2> points, Transform2D placement)
    {
        while (visual.Items.Count > 1)
        {
            CanvasServer.Current.Free(visual.Items[^1]);
            visual.Items.RemoveAt(visual.Items.Count - 1);
        }
        if (visual.Items.Count == 0)
            visual.Items.Add(CreateItem(
                visual.Texture,
                visual.Size,
                visual.Layer,
                AnimatorDrawOrder.Chain(visual.Index, 0)));

        Handle item = visual.Items[0];
        CanvasServer.Current.Clear(item);
        CanvasServer.Current.SetTransform(item, Transform2D.Identity);
        (Vector2[] vertices, Vector2[] uvs, int[] triangles) = XafChainArt.Sheet(points, chain.Width);
        if (triangles.Length == 0)
            return;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = placement * vertices[i];
        var colors = new Color[vertices.Length];
        Array.Fill(colors, Colors.White);
        CanvasServer.Current.AddTriangleArray(item, triangles, vertices, colors, uvs,
            texture: visual.Texture.Id);
    }

    public void ApplyCloths(XafPackage package, XafClothSimulation simulation, Transform2D placement)
    {
        foreach (ClothVisual visual in _cloths)
        {
            CanvasServer.Current.Clear(visual.Item);
            if (visual.Index >= package.Cloths.Count)
                continue;

            XafCloth cloth = package.Cloths[visual.Index];
            IReadOnlyList<Vector2> points = simulation.Points(visual.Index);
            if (points.Count != cloth.Count)
                continue;

            var placed = new Vector2[points.Count];
            for (int i = 0; i < placed.Length; i++)
                placed[i] = placement * points[i];

            // The grid is rebuilt when its size changes, so the triangles follow the cloth.
            if (visual.Columns != cloth.Columns || visual.Rows != cloth.Rows)
            {
                visual.Columns = cloth.Columns;
                visual.Rows = cloth.Rows;
                visual.Triangles = XafClothLayout.Triangles(cloth.Columns, cloth.Rows);
            }

            var colors = new Color[placed.Length];
            for (int row = 0; row < cloth.Rows; row++)
            {
                // A little darker towards the hem, so the sheet reads as hanging cloth, not a flat shape.
                Color shade = cloth.Fill.Darkened(0.25f * row / Math.Max(1, cloth.Rows - 1));
                for (int column = 0; column < cloth.Columns; column++)
                    colors[cloth.Index(column, row)] = shade;
            }

            CanvasServer.Current.AddTriangleArray(visual.Item, visual.Triangles, placed, colors);
        }
    }

    public Rect2 Bounds(Transform2D[] attachments)
    {
        Rect2? merged = null;
        foreach (Visual visual in _visuals)
        {
            if (visual.Index >= attachments.Length || visual.Art != 0)
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
            CanvasServer.Current.Free(visual.Item);
            visual.Texture.Dispose();
        }
        _visuals.Clear();

        foreach (ChainVisual chain in _chains)
        {
            foreach (Handle item in chain.Items)
                CanvasServer.Current.Free(item);
            chain.Texture.Dispose();
        }
        _chains.Clear();

        foreach (EffectVisual effect in _effects)
            CanvasServer.Current.Free(effect.Item);
        _effects.Clear();

        foreach (ClothVisual cloth in _cloths)
            CanvasServer.Current.Free(cloth.Item);
        _cloths.Clear();

        ReleaseParticles();
        _particlePackage = null;
    }

    /// <summary>
    /// One polygon primitive as a hidden canvas item: its outline triangulated,
    /// tinted by its fill, textured across its bounds when its ref names a
    /// texture, and drawn with its shader, or the item's, when one is common.
    /// An outline that cannot be triangulated is skipped rather than drawn wrong.
    /// </summary>
    private EffectVisual? TryPolygon(XifPrimitive primitive, Xif item, int layer, int order)
    {
        Vector2[] points = primitive.Points.ToArray();
        int[] triangles = points.Length >= 3 ? Triangulate.Polygon(points) : Array.Empty<int>();
        if (triangles.Length == 0)
            return null;

        Handle texture = primitive.Ref.Length > 0 ? TextureServer.Current.Load(primitive.Ref) : default;
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

        Handle canvasItem = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(canvasItem, canvas);
        SetLayer(canvasItem, layer, order);
        CanvasServer.Current.SetVisible(canvasItem, false);
        CanvasServer.Current.AddTriangleArray(canvasItem, triangles, points, colors, uvs,
            texture: texture);

        string shader = !primitive.Shader.IsNone ? primitive.Shader.Path : item.PrimaryCommonShader;
        if (shader.Length > 0)
            CanvasServer.Current.SetMaterial(canvasItem, new CanvasMaterial(shader));

        return new EffectVisual(-1, canvasItem);
    }

    /// <summary>
    /// Rasterises one group of an SVG file. A missing or malformed file is
    /// skipped, since it must not stop rig editing.
    /// </summary>
    private static bool TryRaster(string svgPath, string group, out ArtTexture? texture, out Vector2 size)
    {
        texture = null;
        size = Vector2.Zero;
        string path = Xanadu.Storage.GamePaths.Full(svgPath);
        if (!System.IO.File.Exists(path))
            return false;

        try
        {
            string svg = XafSvgDocument.IsolateGroup(System.IO.File.ReadAllText(path), group);
            texture = Raster(svg, out size);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// SVG to texture at <see cref="ArtScale"/> pixels per unit, with mipmaps, so
    /// art stays sharp zoomed in and clean scaled down. The size is in units.
    /// Colour is bled into the transparent pixels first, or filtering and the
    /// mipmaps pull their black into the edges (<see cref="ArtAlphaBleed"/>).
    /// </summary>
    private static ArtTexture Raster(string svg, out Vector2 size)
    {
        RasterImage image = SvgRaster.Render(svg, ArtScale);
        size = new Vector2(image.Width, image.Height) / ArtScale;
        ArtAlphaBleed.Apply(image.Pixels, image.Width, image.Height);
        return new ArtTexture(TextureServer.Current.Create(image.Width, image.Height, image.Pixels, mipmaps: true), image);
    }

    private Handle CreateArtItem()
    {
        Handle item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(item, canvas);
        return item;
    }

    private void SetLayer(Handle item, int layer, int order)
    {
        layer = DrawOrder.ClampLayer(layer);
        if (containLayers)
        {
            CanvasServer.Current.SetDrawIndex(item, DrawOrder.Banded(layer, order));
            return;
        }

        CanvasServer.Current.SetZIndex(item, layer);
        CanvasServer.Current.SetDrawIndex(item, order);
    }

    private Handle CreateItem(ArtTexture texture, Vector2 size, int layer, int order = 0)
    {
        Handle item = CreateArtItem();
        SetLayer(item, layer, order);
        CanvasServer.Current.AddTextureRect(item, new Rect2(-size / 2, size), texture.Id);
        return item;
    }

    /// <summary>A rasterised SVG group: its texture, and its pixels for reading coverage.</summary>
    private sealed record ArtTexture(Handle Id, RasterImage Image)
    {
        public void Dispose() => TextureServer.Current.Free(Id);
    }

    /// <summary>
    /// One art option of one attachment; <see cref="Mesh"/> is set once it has been
    /// drawn as a mesh, and <see cref="Layer"/> holds the layer it was last given.
    /// </summary>
    private sealed record Visual(int Index, int Art, Handle Item, ArtTexture Texture, Rect2 Bounds)
    {
        public int? Layer { get; set; }

        public MeshCache? Mesh { get; set; }

        /// <summary>Whether the last pose showed this art.</summary>
        public bool Shown { get; set; } = Art == 0;
    }

    /// <summary>One polygon of an effect; its texture is held by the texture server.</summary>
    private sealed record EffectVisual(int Index, Handle Item)
    {
        /// <summary>The alpha the pose last gave it, which presence keeps when it shades the effect.</summary>
        public float Alpha { get; set; }
    }

    /// <summary>
    /// One chain's art: the texture shared by its pieces, the layer they all draw
    /// on, and a canvas item per segment.
    /// </summary>
    /// <summary>One cloth's canvas item, redrawn every frame, and the grid its triangles were built for.</summary>
    private sealed class ClothVisual(int index, Handle item)
    {
        public int Index { get; } = index;
        public Handle Item { get; } = item;
        public int Columns { get; set; }
        public int Rows { get; set; }
        public int[] Triangles { get; set; } = Array.Empty<int>();
    }

    private sealed record ChainVisual(int Index, ArtTexture Texture, Vector2 Size, int Layer)
    {
        public List<Handle> Items { get; } = new();
    }
}
