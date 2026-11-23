using System.Collections.Generic;
using Xanadu.Core.Items;
using Xanadu.Core.Keys;

namespace Xanadu.Model.Animation;

/// <summary>Deep copy and structural comparison of packages, used for undo snapshots.</summary>
public static class RigPackageCopy
{
    public static RigPackage Clone(RigPackage package) => new(XifCopy.Clone(package.Root));

    public static bool AreEqual(RigPackage a, RigPackage b) => XifCopy.AreEqual(a.Root, b.Root);

    public static RigCloth Clone(RigCloth cloth)
    {
        var copy = new RigCloth
        {
            BoneId = cloth.BoneId,
            Offset = cloth.Offset,
            Columns = cloth.Columns,
            Rows = cloth.Rows,
            TopWidth = cloth.TopWidth,
            HemWidth = cloth.HemWidth,
            Length = cloth.Length,
            Gravity = cloth.Gravity,
            Lean = cloth.Lean,
            Damping = cloth.Damping,
            Stiffness = cloth.Stiffness,
            ZOrder = cloth.ZOrder,
        };
        foreach (RigClothPin pin in cloth.Pins)
            copy.Pins.Add(new RigClothPin { Column = pin.Column, Row = pin.Row, BoneId = pin.BoneId });
        foreach (RigClothCollider collider in cloth.Colliders)
            copy.Colliders.Add(new RigClothCollider { BoneId = collider.BoneId, Radius = collider.Radius });
        foreach (RigClothTether tether in cloth.Tethers)
        {
            copy.Tethers.Add(new RigClothTether
            {
                Column = tether.Column, Row = tether.Row, BoneId = tether.BoneId, Radius = tether.Radius,
            });
        }
        return copy;
    }

    public static RigClip Clone(RigClip clip)
    {
        var copy = new RigClip
        {
            Id = clip.Id,
            Clock = clip.Clock,
            Duration = clip.Duration,
            Loop = clip.Loop,
            LoopStart = clip.LoopStart,
            LoopEnd = clip.LoopEnd,
            Events = clip.Events.ConvertAll(item => new RigEvent { Time = item.Time, Name = item.Name }),
            Targets = clip.Targets.ConvertAll(target => target with { }),
        };
        foreach (KeyChannel channel in clip.Channels)
            copy.Channels.Add(channel.Copy());
        return copy;
    }

    internal static bool BoneEqual(RigBone a, RigBone b) =>
        a.Id == b.Id && a.Parent == b.Parent && a.Rest == b.Rest &&
        a.ZOrder == b.ZOrder && a.AllowTranslation == b.AllowTranslation && a.Bend == b.Bend &&
        a.Limited == b.Limited && a.LimitMin == b.LimitMin && a.LimitMax == b.LimitMax && a.Tip == b.Tip;

    internal static bool ClipEqual(RigClip a, RigClip b) =>
        a.Id == b.Id && a.Clock == b.Clock && a.Duration == b.Duration && a.Loop == b.Loop &&
        a.LoopStart == b.LoopStart && a.LoopEnd == b.LoopEnd &&
        ListEqual(a.Channels, b.Channels, ChannelEqual) &&
        ListEqual(a.Events, b.Events, (x, y) => x.Time == y.Time && x.Name == y.Name) &&
        ListEqual(a.Targets, b.Targets, (x, y) => x == y);

    internal static bool ClothEqual(RigCloth a, RigCloth b) =>
        a.BoneId == b.BoneId && a.Offset == b.Offset &&
        a.Columns == b.Columns && a.Rows == b.Rows && a.TopWidth == b.TopWidth &&
        a.HemWidth == b.HemWidth && a.Length == b.Length && a.Gravity == b.Gravity &&
        a.Lean == b.Lean && a.Damping == b.Damping && a.Stiffness == b.Stiffness &&
        a.ZOrder == b.ZOrder &&
        ListEqual(a.Pins, b.Pins, (x, y) => x.Column == y.Column && x.Row == y.Row && x.BoneId == y.BoneId) &&
        ListEqual(a.Colliders, b.Colliders, (x, y) => x.BoneId == y.BoneId && x.Radius == y.Radius) &&
        ListEqual(a.Tethers, b.Tethers, (x, y) =>
            x.Column == y.Column && x.Row == y.Row && x.BoneId == y.BoneId && x.Radius == y.Radius);

    internal static bool TransitionEqual(RigTransition a, RigTransition b) =>
        a.From == b.From && a.To == b.To && a.Fade == b.Fade && a.Curve == b.Curve;

    private static bool ChannelEqual(KeyChannel a, KeyChannel b) =>
        a.Address == b.Address && ListEqual(a.Keys, b.Keys, (x, y) => x.Same(y));

    internal static bool ListEqual<T>(
        IReadOnlyList<T> a,
        IReadOnlyList<T> b,
        System.Func<T, T, bool> equal)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!equal(a[i], b[i]))
                return false;
        }
        return true;
    }
}
