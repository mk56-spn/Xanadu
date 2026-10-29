using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

/// <summary>
/// Where one joint travels over a clip, in skeleton space: a point every
/// <see cref="AnimatorMotionPaths.Step"/>, how sharply the path turns at each,
/// and where it is at each key that moves it.
/// </summary>
public sealed class AnimatorMotionPath
{
    public int Bone;
    public Vector2[] Points = Array.Empty<Vector2>();
    public float[] Times = Array.Empty<float>();

    /// <summary>How far the path turns at each point, in degrees; zero at the ends and where it stands still.</summary>
    public float[] Turn = Array.Empty<float>();

    /// <summary>The joint at each key of the bone or an ancestor, the keys that can move it.</summary>
    public Vector2[] Keys = Array.Empty<Vector2>();
    public float[] KeyTimes = Array.Empty<float>();
}

/// <summary>
/// Builds motion paths: the selected bones' joints while editing, the
/// extremities (bones with no children) while previewing.
/// </summary>
public static class AnimatorMotionPaths
{
    /// <summary>One tick per frame at 30 fps: even spacing means even speed.</summary>
    public const float Step = 1f / 30f;

    /// <summary>A turn this sharp between two ticks is flagged.</summary>
    public const float SharpDegrees = 60f;

    /// <summary>Half the span drawn around the playhead when the path is windowed.</summary>
    public const float WindowSeconds = 0.5f;

    /// <summary>Movement smaller than this between ticks is standing still, so it has no direction to turn.</summary>
    private const float StillDistance = 0.25f;

    /// <summary>The bones that get a path: the selection while editing, the extremities while previewing.</summary>
    public static List<int> Bones(XafPackage package, AnimatorSelection selection, bool preview)
    {
        if (!preview)
        {
            List<int> selected = selection.Bones();
            selected.RemoveAll(bone => bone < 0 || bone >= package.Skeleton.Bones.Count);
            return selected;
        }

        var parents = new HashSet<int>();
        foreach (XafBone bone in package.Skeleton.Bones)
            parents.Add(bone.Parent);

        var leaves = new List<int>();
        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
        {
            if (package.Skeleton.Bones[i].Parent >= 0 && !parents.Contains(i))
                leaves.Add(i);
        }
        return leaves;
    }

    /// <summary>Samples the clip once per tick and reads every requested joint from the same pose.</summary>
    public static List<AnimatorMotionPath> Build(XafPackage package, XafClip clip, IReadOnlyList<int> bones)
    {
        var paths = new List<AnimatorMotionPath>();
        if (bones.Count == 0)
            return paths;

        float duration = Math.Max(0f, clip.Duration);
        int count = Math.Max(2, (int)Math.Ceiling(duration / Step) + 1);
        var times = new float[count];
        for (int i = 0; i < count; i++)
            times[i] = Math.Min(duration, i * Step);

        var points = new Vector2[bones.Count][];
        for (int b = 0; b < bones.Count; b++)
            points[b] = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            Transform2D[] world = XafRig.WorldBones(package, XafSampler.Sample(package, clip, times[i]));
            for (int b = 0; b < bones.Count; b++)
                points[b][i] = world[bones[b]].Origin;
        }

        for (int b = 0; b < bones.Count; b++)
        {
            var path = new AnimatorMotionPath
            {
                Bone = bones[b],
                Points = points[b],
                Times = times,
                Turn = Turns(points[b]),
            };
            AddKeys(package, clip, path);
            paths.Add(path);
        }
        return paths;
    }

    /// <summary>
    /// How far the direction of travel changes at <paramref name="b"/>, in
    /// degrees, coming from <paramref name="a"/> and going on to <paramref name="c"/>.
    /// </summary>
    public static float TurnDegrees(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 into = b - a;
        Vector2 outOf = c - b;
        if (into.Length() < StillDistance || outOf.Length() < StillDistance)
            return 0f;

        return Mathf.RadToDeg(Math.Abs(into.AngleTo(outOf)));
    }

    /// <summary>The points of a path inside the window around the playhead, or all of them.</summary>
    public static (int First, int Last) Span(AnimatorMotionPath path, bool windowed, float playhead)
    {
        if (!windowed || path.Times.Length == 0)
            return (0, path.Points.Length - 1);

        int first = 0;
        while (first < path.Times.Length - 1 && path.Times[first] < playhead - WindowSeconds)
            first++;
        int last = path.Times.Length - 1;
        while (last > first && path.Times[last] > playhead + WindowSeconds)
            last--;
        return (first, last);
    }

    private static float[] Turns(Vector2[] points)
    {
        var turns = new float[points.Length];
        for (int i = 1; i < points.Length - 1; i++)
            turns[i] = TurnDegrees(points[i - 1], points[i], points[i + 1]);
        return turns;
    }

    /// <summary>The keys that can move a joint are on its bone or any bone above it.</summary>
    private static void AddKeys(XafPackage package, XafClip clip, AnimatorMotionPath path)
    {
        var times = new SortedSet<float>();
        for (int bone = path.Bone; bone >= 0; bone = package.Skeleton.Bones[bone].Parent)
        {
            foreach (float time in XafTracks.KeyTimes(clip, new XafTrack(XafChannelTarget.Bone, package.Skeleton.Bones[bone].Id)))
                times.Add(time);
        }

        var keys = new List<Vector2>();
        var keyTimes = new List<float>();
        float last = float.NegativeInfinity;
        foreach (float time in times)
        {
            if (time - last <= XafTracks.TimeTolerance)
                continue;
            last = time;
            Transform2D[] world = XafRig.WorldBones(package, XafSampler.Sample(package, clip, time));
            keys.Add(world[path.Bone].Origin);
            keyTimes.Add(time);
        }
        path.Keys = keys.ToArray();
        path.KeyTimes = keyTimes.ToArray();
    }
}
