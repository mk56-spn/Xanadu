using System;
using System.Collections.Generic;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator;

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
                RenderingServer.CanvasItemAddTextureRect(
                    item,
                    new Rect2(-image.GetSize() / 2, image.GetSize()),
                    texture.GetRid());
                _visuals.Add(new Visual(i, item, texture));
            }
            catch (Exception)
            {
                // A missing or malformed SVG must not stop rig editing.
            }
        }
    }

    public void Apply(Transform2D[] attachments, Vector2 origin)
    {
        foreach (Visual visual in _visuals)
        {
            if (visual.Index >= attachments.Length)
                continue;

            RenderingServer.CanvasItemSetTransform(
                visual.Item,
                new Transform2D(0f, origin) * attachments[visual.Index]);
        }
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

    private sealed record Visual(int Index, Rid Item, ImageTexture Texture);
}
