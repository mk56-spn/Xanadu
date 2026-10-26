using System.Collections.Generic;

namespace Writer.Tools.Animator;

/// <summary>
/// Bone multi-selection. <see cref="AnimatorSelection.Bone"/> stays the primary
/// bone that menus and the inspector act on; the extras are the rest of a
/// Shift-click group. Keying, nudging and resetting apply to the whole group.
/// </summary>
public partial struct AnimatorSelection
{
    /// <summary>Every selected bone, the primary one first.</summary>
    public readonly List<int> Bones()
    {
        var bones = new List<int>();
        if (Bone >= 0)
            bones.Add(Bone);
        if (ExtraBones is null)
            return bones;

        foreach (int bone in ExtraBones)
        {
            if (bone >= 0 && bone != Bone && !bones.Contains(bone))
                bones.Add(bone);
        }
        return bones;
    }

    public readonly bool IsBoneSelected(int index) =>
        index >= 0 && (index == Bone || (ExtraBones?.Contains(index) ?? false));

    /// <summary>Selects one bone, dropping any other bones and the attachment.</summary>
    public void SelectOnlyBone(int index)
    {
        Bone = index;
        Attachment = -1;
        ExtraBones = null;
    }

    public void SelectOnlyAttachment(int index)
    {
        Attachment = index;
        Bone = -1;
        ExtraBones = null;
    }

    /// <summary>
    /// Adds the bone to the group or takes it out, as Shift-clicking does.
    /// Taking the primary out promotes the first extra instead of clearing all.
    /// </summary>
    public void ToggleBone(int index)
    {
        if (index < 0)
            return;

        Attachment = -1;
        if (Bone == index)
        {
            if (ExtraBones is { Count: > 0 })
            {
                Bone = ExtraBones[0];
                ExtraBones.RemoveAt(0);
                if (ExtraBones.Count == 0)
                    ExtraBones = null;
            }
            else
            {
                Bone = -1;
            }
            return;
        }

        ExtraBones ??= new List<int>();
        if (ExtraBones.Remove(index))
        {
            if (ExtraBones.Count == 0)
                ExtraBones = null;
            return;
        }

        if (Bone < 0)
        {
            Bone = index;
            ExtraBones = null;
            return;
        }

        ExtraBones.Add(index);
    }

    /// <summary>Drops bones an undo, a rig edit or a load removed.</summary>
    public void ClampBones(int count)
    {
        if (Bone >= count)
            Bone = -1;

        int primary = Bone;
        if (ExtraBones is not null)
        {
            ExtraBones.RemoveAll(bone => bone < 0 || bone >= count || bone == primary);
            if (ExtraBones.Count == 0)
                ExtraBones = null;
        }

        if (Bone >= 0 || ExtraBones is not { Count: > 0 })
            return;

        Bone = ExtraBones[0];
        ExtraBones.RemoveAt(0);
        if (ExtraBones.Count == 0)
            ExtraBones = null;
    }
}
