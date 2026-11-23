using Friflo.Engine.ECS;
using Xanadu.Model.Animation;

namespace Xanadu.Model.Animator;

/// <summary>What the selection points at, checked against the document: -1 or null when it no longer exists.</summary>
public static class AnimatorSelected
{
    /// <summary>The selected cloth, if it still exists.</summary>
    public static int Cloth(Entity session)
    {
        int cloth = session.GetComponent<AnimatorSelection>().Cloth;
        int count = session.GetComponent<AnimatorDocument>().Package.Cloths.Count;
        return cloth >= 0 && cloth < count ? cloth : -1;
    }

    /// <summary>The target on the selected bone or a limb it bends, the one at the playhead if there are several.</summary>
    public static RigLimbTarget? Target(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        int bone = session.GetComponent<AnimatorSelection>().Bone;
        if (bone < 0 || AnimatorSession.CurrentClip(document) is not { } clip)
            return null;

        RigLimbTarget? found = null;
        foreach (RigLimbTarget target in clip.Targets)
        {
            if (RigRig.FindBone(document.Package, target.BoneId) != bone &&
                !RigTargets.Channels(document.Package, target).Exists(channel => channel.Bone == bone))
            {
                continue;
            }
            if (target.Covers(document.Runtime.CurrentTime))
                return target;
            found ??= target;
        }
        return found;
    }

    /// <summary>The selected event's index in the open clip, or -1.</summary>
    public static int Event(Entity session, out RigClip? clip)
    {
        clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        AnimatorEventSelection selection = session.GetComponent<AnimatorSessionState>().Event;
        return clip is not null && selection.Clip == clip.Id && selection.Index >= 0 &&
               selection.Index < clip.Events.Count
            ? selection.Index
            : -1;
    }

    /// <summary>The selected part, if it still exists.</summary>
    public static int Part(Entity session)
    {
        int part = session.GetComponent<AnimatorSelection>().Attachment;
        int count = session.GetComponent<AnimatorDocument>().Package.Attachments.Count;
        return part >= 0 && part < count ? part : -1;
    }
}
