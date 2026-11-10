using Friflo.Engine.ECS;
using Xanadu.Animation;

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

        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return null;

        XafPose shown = document.Runtime.Pose;
        XafPose sampled = XafSampler.Sample(document.Package, clip, document.Runtime.CurrentTime);
        var bones = new bool[shown.Bones.Length];
        for (int i = 0; i < bones.Length && i < sampled.Bones.Length; i++)
            bones[i] = !Near(shown.Bones[i], sampled.Bones[i]);

        var attachments = new bool[shown.Attachments.Length];
        for (int i = 0; i < attachments.Length && i < sampled.Attachments.Length; i++)
            attachments[i] = !Near(shown.Attachments[i], sampled.Attachments[i]);
        return (bones, attachments);
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

    private static bool Near(XafTransform a, XafTransform b) =>
        a.Translation.DistanceSquaredTo(b.Translation) < 0.0001f &&
        Mathf.Abs(a.Rotation - b.Rotation) < 0.001f &&
        a.Scale.DistanceSquaredTo(b.Scale) < 0.0001f;
}
