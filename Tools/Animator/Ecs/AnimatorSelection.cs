using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

/// <summary>One selected key set: every channel key of a bone or attachment at one time.</summary>
public readonly record struct AnimatorKeyRef(
    XafChannelTarget Target,
    string Id,
    float Time,
    XafChannelProperty? Property = null)
{
    public XafTrack Track => new(Target, Id);

    /// <summary>Whether this ref is the key set of a target at a time.</summary>
    public bool Is(XafTrack track, float time) =>
        Property is null &&
        Target == track.Target && Id == track.Id &&
        System.Math.Abs(Time - time) <= XafTracks.TimeTolerance;

    /// <summary>Whether this ref is exactly the key on a row at a time.</summary>
    public bool Is(AnimatorTrackRow row, float time) =>
        Target == row.Target && Id == row.Id && Property == row.Property &&
        System.Math.Abs(Time - time) <= XafTracks.TimeTolerance;
}

/// <summary>
/// The selected bone and attachment (indices, -1 for none) and the selected
/// timeline keys. The first key is the primary one, the anchor that the
/// playhead and drags follow; a box select can add more in
/// <see cref="Extra"/>.
/// </summary>
public struct AnimatorSelection : IComponent
{
    public int Bone;
    public int Attachment;

    /// <summary>The selected Verlet chain; -1 for none.</summary>
    public int Chain;

    /// <summary>The selected effect; -1 for none.</summary>
    public int Effect;

    /// <summary>The selected cloth; -1 for none.</summary>
    public int Cloth;

    /// <summary>The selected bones beside the primary one; null when only one is selected.</summary>
    public List<int>? ExtraBones;

    private bool _hasKey;

    /// <summary>
    /// A key is selected. Clearing it drops <see cref="Extra"/> too, so a
    /// leftover group never outlives its primary key.
    /// </summary>
    public bool HasKey
    {
        readonly get => _hasKey;
        set
        {
            _hasKey = value;
            if (!value)
                Extra = null;
        }
    }

    public XafChannelTarget KeyTarget;
    public string KeyId;
    public XafChannelProperty? KeyProperty;
    public float KeyTime;

    /// <summary>The other selected keys beside the primary one; null when only one is selected.</summary>
    public List<AnimatorKeyRef>? Extra;

    /// <summary>Every selected key, the primary one first.</summary>
    public readonly List<AnimatorKeyRef> Keys()
    {
        var keys = new List<AnimatorKeyRef>();
        if (!_hasKey)
            return keys;

        keys.Add(new AnimatorKeyRef(KeyTarget, KeyId, KeyTime, KeyProperty));
        if (Extra is not null)
            keys.AddRange(Extra);
        return keys;
    }

    public readonly bool IsKeySelected(XafTrack track, float time) =>
        _hasKey && Keys().Exists(key => key.Is(track, time));

    public readonly bool IsKeySelected(AnimatorTrackRow row, float time) =>
        _hasKey && Keys().Exists(key => key.Is(row, time));

    public static AnimatorSelection None => new()
    {
        Bone = -1,
        Attachment = -1,
        Chain = -1,
        Effect = -1,
        Cloth = -1,
        HasKey = false,
        KeyId = "",
    };

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

    /// <summary>Selects one bone, dropping any other bones, the attachment, chain, effect and cloth.</summary>
    public void SelectOnlyBone(int index) => SelectOnly(index, ref Bone);

    public void SelectOnlyAttachment(int index) => SelectOnly(index, ref Attachment);

    public void SelectOnlyChain(int index) => SelectOnly(index, ref Chain);

    public void SelectOnlyEffect(int index) => SelectOnly(index, ref Effect);

    public void SelectOnlyCloth(int index) => SelectOnly(index, ref Cloth);

    /// <summary>Selects one of the five targets and clears the rest.</summary>
    private void SelectOnly(int index, ref int slot)
    {
        Bone = -1;
        Attachment = -1;
        Chain = -1;
        Effect = -1;
        Cloth = -1;
        slot = index;
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
        Chain = -1;
        Effect = -1;
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
