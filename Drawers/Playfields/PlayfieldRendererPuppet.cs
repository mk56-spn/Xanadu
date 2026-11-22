using System;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// The stand-in bone puppet (<see cref="Puppet"/>): each bone a rectangle from
/// its joint to its child's (or its tip), turning with the bone; the head a square. The far arm and
/// leg are drawn first and darker, the near ones last and brightest, so it
/// reads three-quarter on. It fills the body's box, feet on its bottom edge.
/// </summary>
public static partial class PlayfieldRenderer
{
    /// <summary>How thick each bone is drawn, playfield units.</summary>
    private static float PuppetWidth(string bone) => bone switch
    {
        "pelvis" => 26f,
        "spine" => 21f,
        "chest" => 38f,
        "upper_arm_near" or "upper_arm_far" => 12f,
        "forearm_near" or "forearm_far" => 10f,
        "hand_near" or "hand_far" => 10f,
        "thigh_near" or "thigh_far" => 15f,
        "shin_near" or "shin_far" => 12f,
        "foot_near" or "foot_far" => 9f,
        _ => 0f,
    };

    private const float PuppetHeadRadius = 10f;

    /// <summary>
    /// Draws the puppet round <paramref name="center"/> (the body's centre on
    /// screen), flashing white by <paramref name="flash"/>.
    /// </summary>
    public static void DrawPuppet(IRectCanvas canvas, Handle rid, in Puppet puppet, Vector2 center, float scale, float alpha, float flash)
    {
        if (puppet.Package is not { } package || puppet.Bones.Length != package.Skeleton.Bones.Count)
            return;

        float side = puppet.FacingLeft ? -1f : 1f;
        Vector2 Screen(Vector2 rig) => center + new Vector2(rig.X * side, rig.Y + PlayRules.BodyHeight / 2f) * scale;

        // Back to front: far limbs, the body, near limbs.
        Span<int> order = stackalloc int[package.Skeleton.Bones.Count];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        var bones = package.Skeleton.Bones;
        for (int i = 1; i < order.Length; i++)
        {
            for (int j = i; j > 0 && bones[order[j]].ZOrder < bones[order[j - 1]].ZOrder; j--)
                (order[j], order[j - 1]) = (order[j - 1], order[j]);
        }

        Color edge = new(BodyEdge.R * 0.6f, BodyEdge.G * 0.6f, BodyEdge.B * 0.7f, alpha);
        foreach (int i in order)
        {
            XafBone bone = bones[i];
            Transform2D at = puppet.Bones[i];
            bool far = bone.ZOrder < 0;
            Color fill = far
                ? new Color(BodyFill.R * 0.55f, BodyFill.G * 0.6f, BodyFill.B * 0.75f, alpha)
                : new Color(BodyFill.R, BodyFill.G, BodyFill.B, alpha);
            fill = Mix(fill, new Color(1f, 1f, 1f, alpha), flash * 0.8f);

            if (bone.Id == "head")
            {
                // A square standing on the neck, turned with the head.
                Vector2 neck = Screen(at.Origin);
                Vector2 top = Screen(at * bone.Tip);
                float size = PuppetHeadRadius * 2f * scale;
                Segment(canvas, rid, neck, top, size, fill, edge, 2.5f * scale);
                continue;
            }

            float width = PuppetWidth(bone.Id);
            if (width <= 0f)
                continue;

            Vector2 from = Screen(at.Origin);
            Vector2 to = Screen(EndOf(package, puppet.Bones, i));
            float w = width * scale;
            Segment(canvas, rid, from, to, w, fill, edge, 2f * scale);
        }

        static Color Mix(Color a, Color b, float t) =>
            new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A);
    }

    /// <summary>Where a bone's segment ends in rig space: its first child's joint, else its tip.</summary>
    private static Vector2 EndOf(XafPackage package, Transform2D[] world, int bone)
    {
        var bones = package.Skeleton.Bones;
        for (int i = 0; i < bones.Count; i++)
        {
            // The torso's own run: spine to chest, chest to neck; a limb runs to its next joint.
            if (bones[i].Parent == bone && bones[i].ZOrder == bones[bone].ZOrder)
                return world[i].Origin;
        }

        return world[bone] * bones[bone].Tip;
    }

    /// <summary>
    /// A limb segment: a rectangle <paramref name="width"/> across from
    /// <paramref name="from"/> to <paramref name="to"/>, turned along it, filled
    /// and edged.
    /// </summary>
    private static void Segment(IRectCanvas canvas, Handle rid, Vector2 from, Vector2 to, float width, Color fill, Color edge, float edgeWidth)
    {
        Vector2 along = to - from;
        if (along.LengthSquared() < 0.01f)
            return;
        Vector2 across = new Vector2(-along.Y, along.X) / along.Length() * (width / 2f);
        Span<Vector2> corners = stackalloc Vector2[5];
        corners[0] = from - across;
        corners[1] = to - across;
        corners[2] = to + across;
        corners[3] = from + across;
        corners[4] = corners[0];
        canvas.DrawPolygon(rid, corners[..4], fill);
        canvas.DrawPolyline(rid, corners, edge, MathF.Max(1f, edgeWidth));
    }
}
