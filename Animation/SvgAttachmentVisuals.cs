using System;
using System.Collections.Generic;
using Godot;

namespace Writer.Animation;

/// <summary>Rasterises each attachment's SVG group into a canvas item.</summary>
public sealed class SvgAttachmentVisuals(Rid canvas) : IAttachmentVisuals
{
    private readonly List<Visual> _visuals = new();

    public void Reload(XafPackage package)
    {
        Release();
        for (int i = 0; i < package.Attachments.Count; i++)
        {
            XafAttachment attachment = package.Attachments[i];
            string path = ProjectSettings.GlobalizePath(attachment.SvgPath);
            if (!System.IO.File.Exists(path))
                continue;

            try
            {
                string svg = XafSvgDocument.IsolateGroup(
                    System.IO.File.ReadAllText(path),
                    attachment.Group);
                var image = new Image();
                Error error = image.LoadSvgFromString(svg, 1f);
                if (error != Error.Ok)
                    throw new InvalidOperationException(error.ToString());

                ImageTexture texture = ImageTexture.CreateFromImage(image);
                Rid item = RenderingServer.CanvasItemCreate();
                RenderingServer.CanvasItemSetParent(item, canvas);
                var bounds = new Rect2(-image.GetSize() / 2, image.GetSize());
                RenderingServer.CanvasItemAddTextureRect(item, bounds, texture.GetRid());
                _visuals.Add(new Visual(i, item, texture, bounds));
            }
            catch (Exception)
            {
                // A missing or malformed SVG must not stop rig editing.
            }
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
    }

    private sealed record Visual(int Index, Rid Item, ImageTexture Texture, Rect2 Bounds);
}
