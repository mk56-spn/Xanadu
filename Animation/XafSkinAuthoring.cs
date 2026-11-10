using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>
/// Makes and edits an attachment's stored skin: its mesh traced from the art's
/// outline (<see cref="XafSkinBuilder"/>), its weights worked out by heat
/// diffusion over the bones that bend it in the rest pose
/// (<see cref="XafHeatWeights"/>), and weights painted by hand.
/// </summary>
public static class XafSkinAuthoring
{
    /// <summary>How far apart a skin's points are, in the art's units.</summary>
    public const float DefaultSpacing = 3f;

    /// <summary>
    /// Traces the attachment's art (<paramref name="svg"/>, its SVG text) into a
    /// mesh and weights it to the bones that bend it, replacing any skin it had.
    /// The attachment becomes meshed if it was not. False when the art has no
    /// outline to trace.
    /// </summary>
    public static bool Build(XafPackage package, int attachmentIndex, string svg, float spacing = DefaultSpacing)
    {
        if (attachmentIndex < 0 || attachmentIndex >= package.Attachments.Count)
            return false;

        XafAttachment attachment = package.Attachments[attachmentIndex];
        Rect2 box = XafSvgOutline.ViewBox(svg);
        List<Vector2[]> outline = XafSvgOutline.Polygons(svg, attachment.Group);
        if (outline.Count == 0)
            return false;

        (Vector2[] points, int[] triangles) = XafSkinBuilder.Build(outline, spacing);
        if (triangles.Length == 0)
            return false;

        attachment.Mesh ??= new XafMesh();
        var skin = new XafSkinMesh { ArtSize = box.Size, Points = points, Triangles = triangles };
        attachment.Mesh.Skin = skin;
        Reweigh(package, attachmentIndex, outline);
        return true;
    }

    /// <summary>
    /// Works the skin's weights out again by heat diffusion over the bones that
    /// bend the attachment now, in the rest pose, throwing away painted ones.
    /// </summary>
    public static void Reweigh(XafPackage package, int attachmentIndex, IReadOnlyList<Vector2[]> outline)
    {
        XafAttachment attachment = package.Attachments[attachmentIndex];
        if (attachment.Mesh?.Skin is not { } skin)
            return;
        Vector2 artSize = skin.ArtSize;

        XafPose rest = XafClothLayout.RestPose(package);
        Transform2D[] bones = XafRig.WorldBones(package, rest);
        Transform2D placed = XafRig.WorldAttachments(package, rest, bones)[attachmentIndex];
        Transform2D artToRig = placed * new Transform2D(0f, -artSize / 2);

        List<int> influences = XafSkinWeights.Influences(package, attachment);
        var segments = new List<(Vector2, Vector2)>();
        foreach (int bone in influences)
            segments.Add(XafSkinBones.Segment(package, bones, bone));

        var points = new Vector2[skin.Points.Length];
        for (int i = 0; i < points.Length; i++)
            points[i] = artToRig * skin.Points[i];
        Transform2D rigToArt = artToRig.AffineInverse();

        skin.Bones = influences.ConvertAll(bone => package.Skeleton.Bones[bone].Id);
        skin.Weights = XafHeatWeights.Compute(points, skin.Triangles, segments,
            spot => XafSvgOutline.Inside(outline, rigToArt * spot));
        skin.Version++;
    }

    /// <summary>
    /// Adds <paramref name="amount"/> (taking away if negative) to one bone's
    /// weight at each of the given points, scaled by each point's falloff, and
    /// shares what is left of the point among its other bones as they were.
    /// The bone joins the skin's bones if it was not among them.
    /// </summary>
    public static void Paint(XafSkinMesh skin, string bone, IReadOnlyList<(int Point, float Falloff)> points, float amount)
    {
        int slot = skin.Bones.IndexOf(bone);
        if (slot < 0)
        {
            skin.Bones.Add(bone);
            slot = skin.Bones.Count - 1;
            for (int i = 0; i < skin.Weights.Length; i++)
            {
                var grown = new float[skin.Bones.Count];
                Array.Copy(skin.Weights[i], grown, skin.Weights[i].Length);
                skin.Weights[i] = grown;
            }
        }

        foreach ((int point, float falloff) in points)
        {
            if (point < 0 || point >= skin.Weights.Length)
                continue;
            float[] row = skin.Weights[point];
            float before = row[slot];
            float after = Math.Clamp(before + amount * falloff, 0f, 1f);
            float othersBefore = 1f - before;
            float othersAfter = 1f - after;
            for (int b = 0; b < row.Length; b++)
            {
                if (b == slot)
                    continue;
                row[b] = othersBefore > 1e-6f ? row[b] * othersAfter / othersBefore : 0f;
            }
            row[slot] = after;

            // Taking a point's only bone away leaves it nothing: it goes back to the skin's first bone.
            float total = 0f;
            foreach (float weight in row)
                total += weight;
            if (total <= 1e-6f)
                row[slot == 0 && row.Length > 1 ? 1 : 0] = 1f;
            XafSkinMesh.Tidy(row);
        }
        skin.Version++;
    }

    /// <summary>
    /// The skin's weights as rows over <paramref name="influences"/>, the bones
    /// the attachment is bent by now, matched by id; a bone the skin knows that
    /// no longer bends it gives up its share to the rest.
    /// </summary>
    public static float[][] WeightsOver(XafPackage package, XafSkinMesh skin, IReadOnlyList<int> influences)
    {
        var slots = new int[skin.Bones.Count];
        for (int s = 0; s < slots.Length; s++)
        {
            int bone = XafRig.FindBone(package, skin.Bones[s]);
            slots[s] = bone < 0 ? -1 : IndexOf(influences, bone);
        }

        var rows = new float[skin.Weights.Length][];
        for (int p = 0; p < rows.Length; p++)
        {
            var row = new float[influences.Count];
            for (int s = 0; s < slots.Length; s++)
            {
                if (slots[s] >= 0)
                    row[slots[s]] += skin.Weights[p][s];
            }
            float total = 0f;
            foreach (float weight in row)
                total += weight;
            if (total <= 1e-6f)
                row[0] = 1f;
            else
            {
                for (int b = 0; b < row.Length; b++)
                    row[b] /= total;
            }
            rows[p] = row;
        }
        return rows;
    }

    /// <summary>
    /// Where each of a skin's points is in rig space in a pose, bent by its
    /// stored weights, as it is drawn: for picking points under a brush.
    /// </summary>
    public static Vector2[] Posed(XafPackage package, int attachmentIndex, XafPose pose)
    {
        XafAttachment attachment = package.Attachments[attachmentIndex];
        if (attachment.Mesh?.Skin is not { } skin)
            return Array.Empty<Vector2>();

        XafPose rest = XafClothLayout.RestPose(package);
        Transform2D[] restBones = XafRig.WorldBones(package, rest);
        Transform2D restAttachment = XafRig.WorldAttachments(package, rest, restBones)[attachmentIndex];
        Transform2D[] bones = XafRig.WorldBones(package, pose);
        Transform2D placed = XafRig.WorldAttachments(package, pose, bones)[attachmentIndex];
        List<int> influences = XafSkinWeights.Influences(package, attachment);
        if (influences.Count == 0)
            return Array.Empty<Vector2>();

        var restPoints = new Vector2[skin.Points.Length];
        for (int i = 0; i < restPoints.Length; i++)
            restPoints[i] = restAttachment * (skin.Points[i] - skin.ArtSize / 2);
        var squash = new Transform2D[bones.Length];
        for (int i = 0; i < squash.Length; i++)
            squash[i] = XafRig.Foreshorten(package, pose, i);
        return XafSkinDeform.Deform(influences, WeightsOver(package, skin, influences), restPoints, restBones, bones,
            restAttachment, placed, squash);
    }

    private static int IndexOf(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return i;
        }
        return -1;
    }
}
