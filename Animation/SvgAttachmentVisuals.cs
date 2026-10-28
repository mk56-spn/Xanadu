using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>Rasterises each attachment's SVG group, and each chain's art, into canvas items.</summary>
public sealed class SvgAttachmentVisuals(Rid canvas) : IAttachmentVisuals
{
    private readonly List<Visual> _visuals = new();
    private readonly List<ChainVisual> _chains = new();

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

    /// <summary>
    /// One chain's art: the texture shared by its pieces, the layer they all draw
    /// on, and a canvas item per segment.
    /// </summary>
    private sealed record ChainVisual(int Index, ImageTexture Texture, Vector2 Size, int Layer)
    {
        public List<Rid> Items { get; } = new();
    }
}
