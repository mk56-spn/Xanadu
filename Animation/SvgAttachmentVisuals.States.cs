using System;
using System.Collections.Generic;
using System.Linq;
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
            DrawMesh(package, pose, attachment, mesh, visual, bones, attachments[visual.Index],
                restBones, restAttachments[visual.Index], placement);
        }
    }

    private static void DrawMesh(XafPackage package, XafPose pose, XafAttachment attachment, XafMesh mesh,
        Visual visual, Transform2D[] bones, Transform2D placed, Transform2D[] restBones,
        Transform2D restAttachment, Transform2D placement)
    {
        if (mesh.Skin is { Triangles.Length: > 0 } skin)
        {
            DrawSkin(package, pose, attachment, skin, visual, bones, placed, restBones, restAttachment, placement);
            return;
        }

        if (visual.Mesh is not { } cache || cache.Detail != mesh.Detail || cache.Skin is not null)
        {
            (Vector2[] grid, Vector2[] uvs) = XafSkinGrid.Grid(mesh, visual.Bounds);
            (int columns, int rows) = XafSkinGrid.Points(mesh, visual.Bounds.Size);
            bool[]? covered = Coverage(mesh, visual);
            cache = new MeshCache(mesh.Detail, grid, uvs, XafSkinGrid.Triangles(mesh, visual.Bounds.Size, covered),
                columns, rows, covered);
            visual.Mesh = cache;
        }

        List<int> influences = XafSkinWeights.Influences(package, attachment);
        if (influences.Count == 0)
            return;

        // Weights depend only on the rest pose, so they are worked out again
        // only when it, the bones or the blend change, not every frame.
        var key = new WeightKey(restAttachment, mesh.Blend, influences.ConvertAll(bone => (bone, restBones[bone])));
        if (cache.Weights is null || cache.Key is null || !cache.Key.Matches(key))
        {
            var restPoints = new Vector2[cache.Grid.Length];
            for (int i = 0; i < restPoints.Length; i++)
                restPoints[i] = restAttachment * cache.Grid[i];
            cache.RestPoints = restPoints;
            cache.Weights = cache.Covered is { } covered
                ? XafSkinWeights.Weights(package, influences, restPoints, restBones, mesh.Blend, cache.Columns, cache.Rows, covered)
                : XafSkinWeights.Weights(package, influences, restPoints, restBones, mesh.Blend);
            cache.Key = key;
        }

        DrawBent(package, pose, influences, cache, visual, bones, placed, restBones, restAttachment, placement);
    }

    /// <summary>
    /// Draws a stored skin: its own traced points and triangles, bent by its
    /// stored weights. Its points are in art space, from the raster's top left,
    /// so each is its texture spot over the art's size.
    /// </summary>
    private static void DrawSkin(XafPackage package, XafPose pose, XafAttachment attachment, XafSkinMesh skin,
        Visual visual, Transform2D[] bones, Transform2D placed, Transform2D[] restBones,
        Transform2D restAttachment, Transform2D placement)
    {
        List<int> influences = XafSkinWeights.Influences(package, attachment);
        if (influences.Count == 0)
            return;

        var key = new WeightKey(restAttachment, skin.Version, influences.ConvertAll(bone => (bone, restBones[bone])));
        if (visual.Mesh is not { } cache || cache.Skin != skin || cache.Key is null || !cache.Key.Matches(key))
        {
            var local = new Vector2[skin.Points.Length];
            var uvs = new Vector2[skin.Points.Length];
            var restPoints = new Vector2[skin.Points.Length];
            for (int i = 0; i < local.Length; i++)
            {
                local[i] = visual.Bounds.Position + skin.Points[i];
                uvs[i] = skin.Points[i] / visual.Bounds.Size;
                restPoints[i] = restAttachment * local[i];
            }
            cache = new MeshCache(-1, local, uvs, skin.Triangles, 0, 0, null)
            {
                Skin = skin,
                RestPoints = restPoints,
                Weights = XafSkinAuthoring.WeightsOver(package, skin, influences),
                Key = key,
            };
            visual.Mesh = cache;
        }

        DrawBent(package, pose, influences, cache, visual, bones, placed, restBones, restAttachment, placement);
    }

    /// <summary>Bends a mesh's rest points by its weights to the posed bones and draws it, keeping where it went.</summary>
    private static void DrawBent(XafPackage package, XafPose pose, List<int> influences, MeshCache cache,
        Visual visual, Transform2D[] bones, Transform2D placed, Transform2D[] restBones,
        Transform2D restAttachment, Transform2D placement)
    {
        var squash = new Transform2D[bones.Length];
        for (int i = 0; i < squash.Length; i++)
            squash[i] = XafRig.Foreshorten(package, pose, i);

        Vector2[] bent = XafSkinDeform.Deform(influences, cache.Weights!, cache.RestPoints, restBones, bones,
            restAttachment, placed, squash);
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

    /// <summary>Which grid cells the art covers, read from its raster's opacity; null when it cannot be read.</summary>
    private static bool[]? Coverage(XafMesh mesh, Visual visual)
    {
        Image? image = visual.Texture.GetImage();
        if (image is null || image.IsEmpty())
            return null;
        if (image.IsCompressed())
            image.Decompress();

        Vector2I size = image.GetSize();
        return XafSkinGrid.Covered(mesh, visual.Bounds.Size, uv => image.GetPixel(
            Math.Clamp((int)(uv.X * (size.X - 1)), 0, size.X - 1),
            Math.Clamp((int)(uv.Y * (size.Y - 1)), 0, size.Y - 1)).A);
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
    private sealed record MeshCache(int Detail, Vector2[] Grid, Vector2[] Uvs, int[] Triangles, int Columns, int Rows,
        bool[]? Covered)
    {
        public Vector2[] Drawn { get; set; } = Array.Empty<Vector2>();

        public Vector2[] RestPoints { get; set; } = Array.Empty<Vector2>();

        public float[][]? Weights { get; set; }

        public WeightKey? Key { get; set; }

        /// <summary>The stored skin this was built from, or null for the grid.</summary>
        public XafSkinMesh? Skin { get; set; }
    }

    /// <summary>What a mesh's weights were worked out from: they hold while it does.</summary>
    private sealed record WeightKey(Transform2D RestAttachment, float Blend, List<(int Bone, Transform2D Pose)> Bones)
    {
        public bool Matches(WeightKey other) =>
            RestAttachment == other.RestAttachment && Blend == other.Blend &&
            Bones.Count == other.Bones.Count && Bones.SequenceEqual(other.Bones);
    }
}
