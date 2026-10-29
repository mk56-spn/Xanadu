using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Animation;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Draws motion paths under the rig: a line per joint that brightens from the
/// clip's start to its end, a tick per frame so spacing shows speed, a dot at
/// each key that moves the joint, a ring where it is now, and warm segments
/// where the path turns sharper than <see cref="AnimatorMotionPaths.SharpDegrees"/>.
/// Paths are rebuilt only when the clip, the rig or the bones change.
/// </summary>
public sealed class AnimatorMotionPathOverlay(IRectCanvas canvas, Rid target)
{
    private static readonly Color Start = new(Palette.Info, 0.25f);
    private static readonly Color End = new(Palette.Cyan, 0.9f);
    private static readonly Color Tick = new(Palette.TextMuted, 0.55f);
    private static readonly Color Key = Palette.Accent;
    private static readonly Color Here = Colors.White;
    private static readonly Color Sharp = Palette.Accent;
    private static readonly Color Sharpest = Palette.Danger;

    /// <summary>At this turn the warning is fully red; it starts yellow at the threshold.</summary>
    private const float SharpestDegrees = 120f;

    private List<AnimatorMotionPath> _paths = new();
    private int? _fingerprint;

    public void Draw(
        in AnimatorDocument document,
        in AnimatorModes modes,
        AnimatorSelection selection,
        bool preview,
        Transform2D placement)
    {
        if (!modes.MotionPaths || modes.Rest || AnimatorSession.CurrentClip(document) is not { } clip)
            return;

        List<int> bones = AnimatorMotionPaths.Bones(document.Package, selection, preview);
        int fingerprint = Fingerprint(document.Package, clip, bones);
        if (fingerprint != _fingerprint)
        {
            _fingerprint = fingerprint;
            _paths = AnimatorMotionPaths.Build(document.Package, clip, bones);
        }

        float playhead = document.Runtime.CurrentTime;
        foreach (AnimatorMotionPath path in _paths)
            DrawPath(path, placement, modes.PathWindow, playhead, clip.Duration);
    }

    private void DrawPath(AnimatorMotionPath path, Transform2D placement, bool windowed, float playhead, float duration)
    {
        (int first, int last) = AnimatorMotionPaths.Span(path, windowed, playhead);
        float span = Math.Max(0.0001f, duration);
        for (int i = first + 1; i <= last; i++)
        {
            Vector2 from = placement * path.Points[i - 1];
            Vector2 to = placement * path.Points[i];
            float sharp = Math.Max(path.Turn[i - 1], path.Turn[i]);
            Color color = sharp >= AnimatorMotionPaths.SharpDegrees
                ? Warning(sharp)
                : Start.Lerp(End, path.Times[i] / span);
            canvas.DrawLine(target, from, to, color, sharp >= AnimatorMotionPaths.SharpDegrees ? 3f : 2f);
        }

        for (int i = first; i <= last; i++)
            canvas.DrawCircle(target, placement * path.Points[i], 1.5f, Tick);

        for (int i = 0; i < path.Keys.Length; i++)
        {
            float time = path.KeyTimes[i];
            if (windowed && Math.Abs(time - playhead) > AnimatorMotionPaths.WindowSeconds)
                continue;
            canvas.DrawCircle(target, placement * path.Keys[i], 4f, Key);
        }

        Vector2 here = placement * At(path, playhead);
        canvas.DrawCircleOutline(target, here, 7f, Here, 1.5f, 24);
    }

    /// <summary>Yellow at the threshold, red at <see cref="SharpestDegrees"/> and past it.</summary>
    private static Color Warning(float degrees)
    {
        float t = Math.Clamp(
            (degrees - AnimatorMotionPaths.SharpDegrees) / (SharpestDegrees - AnimatorMotionPaths.SharpDegrees),
            0f, 1f);
        return Sharp.Lerp(Sharpest, t);
    }

    /// <summary>The path's point at a time, between the two ticks around it.</summary>
    private static Vector2 At(AnimatorMotionPath path, float time)
    {
        if (path.Points.Length == 0)
            return Vector2.Zero;

        float index = Math.Clamp(time / AnimatorMotionPaths.Step, 0f, path.Points.Length - 1);
        int below = (int)Math.Floor(index);
        int above = Math.Min(below + 1, path.Points.Length - 1);
        return path.Points[below].Lerp(path.Points[above], index - below);
    }

    /// <summary>Everything a path depends on: the clip's keys, the rig's rest pose, and which bones.</summary>
    private static int Fingerprint(XafPackage package, XafClip clip, List<int> bones)
    {
        var hash = new HashCode();
        hash.Add(clip.Id);
        hash.Add(clip.Duration);
        hash.Add(clip.Loop);
        hash.Add(clip.LoopStart);
        hash.Add(clip.LoopEnd);
        foreach (int bone in bones)
            hash.Add(bone);
        foreach (XafBone bone in package.Skeleton.Bones)
        {
            hash.Add(bone.Parent);
            hash.Add(bone.Rest);
            hash.Add(bone.CanTranslate);
        }
        foreach (XafChannel channel in clip.Channels)
        {
            hash.Add(channel.TargetId);
            hash.Add(channel.Property);
            foreach (XafKeyframe key in channel.Keyframes)
            {
                hash.Add(key.Time);
                hash.Add(key.Value);
                hash.Add(key.Interpolation);
                hash.Add(key.TangentIn);
                hash.Add(key.TangentOut);
            }
        }
        return hash.ToHashCode();
    }
}
