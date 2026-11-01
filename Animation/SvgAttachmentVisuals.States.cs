using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

public sealed partial class SvgAttachmentVisuals
{
    /// <summary>
    /// Shows the art each attachment's state picks, on its layer, and hides the
    /// rest; then redraws every meshed attachment that shows as triangles bent
    /// by its bones, in canvas space.
    /// </summary>
    public void ApplyStates(XafPackage package, XafPose pose, Transform2D[] bones, Transform2D[] attachments,
        Transform2D placement)
    {
        Transform2D[]? restBones = null;
        Transform2D[]? restAttachments = null;
        foreach (Visual visual in _visuals)
        {
            if (visual.Index >= package.Attachments.Count || visual.Index >= attachments.Length)
                continue;

            XafAttachment attachment = package.Attachments[visual.Index];
            XafAttachmentState state = visual.Index < pose.AttachmentStates.Length
                ? pose.AttachmentStates[visual.Index]
                : default;
            RenderingServer.CanvasItemSetSelfModulate(visual.Item, attachment.SelfModulate);
            int art = state.Art >= 0 && state.Art < attachment.ArtCount ? state.Art : 0;
            bool shown = !state.Hidden && art == visual.Art;
            RenderingServer.CanvasItemSetVisible(visual.Item, shown);
            visual.Shown = shown;
            int layer = state.LayerOf(attachment);
            if (visual.Layer != layer)
            {
                SetLayer(visual.Item, layer, 0);
                visual.Layer = layer;
            }
            if (!shown || attachment.Mesh is not { } mesh)
                continue;

            if (restBones is null || restAttachments is null)
            {
                XafPose rest = XafClothLayout.RestPose(package);
                restBones = XafRig.WorldBones(package, rest);
                restAttachments = XafRig.WorldAttachments(package, rest, restBones);
            }
            DrawMesh(package, attachment, mesh, visual, bones, attachments[visual.Index],
                restBones, restAttachments[visual.Index], placement);
        }
    }

    private static void DrawMesh(XafPackage package, XafAttachment attachment, XafMesh mesh, Visual visual,
        Transform2D[] bones, Transform2D placed, Transform2D[] restBones, Transform2D restAttachment,
        Transform2D placement)
    {
        if (visual.Mesh is not { } cache || cache.Detail != mesh.Detail)
        {
            (Vector2[] grid, Vector2[] uvs) = XafSkin.Grid(mesh, visual.Bounds);
            cache = new MeshCache(mesh.Detail, grid, uvs, XafSkin.Triangles(mesh, visual.Bounds.Size));
            visual.Mesh = cache;
        }

        var restPoints = new Vector2[cache.Grid.Length];
        for (int i = 0; i < restPoints.Length; i++)
            restPoints[i] = restAttachment * cache.Grid[i];

        List<int> influences = XafSkin.Influences(package, attachment);
        if (influences.Count == 0)
            return;

        float[][] weights = XafSkin.Weights(package, influences, restPoints, restBones, mesh.Blend);
        Vector2[] bent = XafSkin.Deform(influences, weights, restPoints, restBones, bones, restAttachment, placed);
        for (int i = 0; i < bent.Length; i++)
            bent[i] = placement * bent[i];

        var colors = new Color[bent.Length];
        Array.Fill(colors, Colors.White);
        cache.Drawn = bent;
        RenderingServer.CanvasItemSetTransform(visual.Item, Transform2D.Identity);
        RenderingServer.CanvasItemClear(visual.Item);
        RenderingServer.CanvasItemAddTriangleArray(visual.Item, cache.Triangles, bent, colors, cache.Uvs,
            texture: visual.Texture.GetRid());
    }

    public bool TryMesh(int attachment, out Vector2[] points, out int[] triangles)
    {
        foreach (Visual visual in _visuals)
        {
            if (visual.Index == attachment && visual.Shown && visual.Mesh is { Drawn.Length: > 0 } cache)
            {
                points = cache.Drawn;
                triangles = cache.Triangles;
                return true;
            }
        }
        points = Array.Empty<Vector2>();
        triangles = Array.Empty<int>();
        return false;
    }

    /// <summary>A meshed visual's grid in the attachment's own space, built once per detail, and where it was last drawn.</summary>
    private sealed record MeshCache(int Detail, Vector2[] Grid, Vector2[] Uvs, int[] Triangles)
    {
        public Vector2[] Drawn { get; set; } = Array.Empty<Vector2>();
    }
}
