using System;
using System.Collections.Generic;
using Xanadu.Core.Keys;
using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator.Ecs;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Which parts of the shown pose no key holds: posed by hand but different from
/// what the current clip samples at the playhead, so scrubbing, playing or
/// saving would lose them. Nothing is unkeyed in rest mode or without a clip.
/// </summary>
public static class AnimatorUnkeyed
{
    /// <summary>The unkeyed bones and attachments by index, or null when nothing can be unkeyed.</summary>
    public static (bool[] Bones, bool[] Attachments)? Find(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (session.GetComponent<AnimatorModes>().Rest)
            return null;

        RigClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return null;

        RigPose shown = document.Runtime.Pose;
        RigPose sampled = RigSampler.Sample(document.Package, clip, document.Runtime.CurrentTime);
        var bones = new bool[shown.Bones.Length];
        for (int i = 0; i < bones.Length && i < sampled.Bones.Length; i++)
            bones[i] = !Near(shown.Bones[i], sampled.Bones[i]);

        var attachments = new bool[shown.Attachments.Length];
        for (int i = 0; i < attachments.Length && i < sampled.Attachments.Length; i++)
            attachments[i] = !Near(shown.Attachments[i], sampled.Attachments[i]);
        return (bones, attachments);
    }

    /// <summary>
    /// The parts the current clip keys whose first key comes after 0: the
    /// sampler holds that first key back to the start, so a pose keyed mid-clip
    /// silently becomes the opening pose too. Empty in rest mode or without a clip.
    /// </summary>
    public static List<string> MissingStartKey(Entity session)
    {
        var parts = new List<string>();
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (session.GetComponent<AnimatorModes>().Rest || AnimatorSession.CurrentClip(document) is not { } clip)
            return parts;

        foreach (var channel in clip.Channels)
        {
            if (channel.Keys.Count == 0)
                continue;
            float first = float.MaxValue;
            foreach (var key in channel.Keys)
                first = Math.Min(first, key.Time);
            string name = channel.Address.Part.Length > 0 ? channel.Address.Part : channel.Address.Node;
            if (first > KeyChannel.TimeTolerance && !parts.Contains(name))
                parts.Add(name);
        }
        return parts;
    }

    /// <summary>The warning for parts with no key at 0, e.g. "NO KEY AT 0: ARM, HEAD · THEIR POSE HOLDS FROM THE START".</summary>
    public static string MissingStartNote(List<string> parts)
    {
        string names = parts.Count <= 3 ? string.Join(", ", parts) : $"{string.Join(", ", parts.GetRange(0, 3))} +{parts.Count - 3}";
        return $"NO KEY AT 0: {names.ToUpperInvariant()} · THEIR POSE HOLDS FROM THE START";
    }

    /// <summary>How many bones and attachments are unkeyed.</summary>
    public static int Count(Entity session)
    {
        if (Find(session) is not { } found)
            return 0;

        int count = 0;
        foreach (bool unkeyed in found.Bones)
            count += unkeyed ? 1 : 0;
        foreach (bool unkeyed in found.Attachments)
            count += unkeyed ? 1 : 0;
        return count;
    }

    /// <summary>Whether the bone at <paramref name="bone"/> is unkeyed.</summary>
    public static bool IsBone(Entity session, int bone) =>
        Find(session) is { } found && bone >= 0 && bone < found.Bones.Length && found.Bones[bone];

    /// <summary>Whether the attachment at <paramref name="attachment"/> is unkeyed.</summary>
    public static bool IsAttachment(Entity session, int attachment) =>
        Find(session) is { } found && attachment >= 0 && attachment < found.Attachments.Length &&
        found.Attachments[attachment];

    /// <summary>The status line for <paramref name="count"/> unkeyed parts, e.g. "3 POSED BUT NOT KEYED".</summary>
    public static string Note(int count) =>
        count == 1 ? "1 PART POSED BUT NOT KEYED" : $"{count} PARTS POSED BUT NOT KEYED";

    private static bool Near(RigTransform a, RigTransform b) =>
        a.Translation.DistanceSquaredTo(b.Translation) < 0.0001f &&
        Math.Abs(a.Rotation - b.Rotation) < 0.001f &&
        a.Scale.DistanceSquaredTo(b.Scale) < 0.0001f;
}
