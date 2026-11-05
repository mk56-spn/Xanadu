using System.Collections.Generic;
using Xanadu.Items;

namespace Xanadu.Animation;

/// <summary>Deep copy and structural comparison of packages, used for undo snapshots.</summary>
public static class XafPackageCopy
{
    public static XafPackage Clone(XafPackage package)
    {
        var copy = new XafPackage { Major = package.Major, Minor = package.Minor };
        foreach (XafBone bone in package.Skeleton.Bones)
        {
            copy.Skeleton.Bones.Add(new XafBone
            {
                Id = bone.Id,
                Parent = bone.Parent,
                Rest = bone.Rest,
                ZOrder = bone.ZOrder,
                AllowTranslation = bone.AllowTranslation,
                Bend = bone.Bend,
                Limited = bone.Limited,
                LimitMin = bone.LimitMin,
                LimitMax = bone.LimitMax,
                Tip = bone.Tip,
            });
        }

        foreach (XafAttachment attachment in package.Attachments)
        {
            copy.Attachments.Add(new XafAttachment
            {
                SvgPath = attachment.SvgPath,
                Group = attachment.Group,
                BoneId = attachment.BoneId,
                Transform = attachment.Transform,
                ZOrder = attachment.ZOrder,
                SelfModulate = attachment.SelfModulate,
                Alternates = attachment.Alternates.ConvertAll(art => new XafArt { SvgPath = art.SvgPath, Group = art.Group }),
                Mesh = attachment.Mesh is { } mesh
                    ? new XafMesh { Detail = mesh.Detail, Blend = mesh.Blend, Bones = new List<string>(mesh.Bones), Skin = mesh.Skin?.Clone() }
                    : null,
            });
        }

        foreach (XafChain chain in package.Chains)
        {
            copy.Chains.Add(new XafChain
            {
                Id = chain.Id,
                BoneId = chain.BoneId,
                Offset = chain.Offset,
                Segments = chain.Segments,
                SegmentLength = chain.SegmentLength,
                Gravity = chain.Gravity,
                Damping = chain.Damping,
                SvgPath = chain.SvgPath,
                Group = chain.Group,
                Width = chain.Width,
                Lean = chain.Lean,
                CollideBoneId = chain.CollideBoneId,
                CollideOffset = chain.CollideOffset,
                CollideRadius = chain.CollideRadius,
                Sheet = chain.Sheet,
                Layer = chain.Layer,
            });
        }

        foreach (XafClip clip in package.Clips)
            copy.Clips.Add(Clone(clip));
        foreach (XafEffect effect in package.Effects)
        {
            copy.Effects.Add(new XafEffect
            {
                Id = effect.Id,
                BoneId = effect.BoneId,
                Transform = effect.Transform,
                ZOrder = effect.ZOrder,
                Item = XifCopy.Clone(effect.Item),
            });
        }
        foreach (XafCloth cloth in package.Cloths)
            copy.Cloths.Add(Clone(cloth));
        foreach (XafTransition rule in package.Transitions)
        {
            copy.Transitions.Add(new XafTransition
            {
                From = rule.From,
                To = rule.To,
                Fade = rule.Fade,
                Curve = rule.Curve,
            });
        }
        return copy;
    }

    public static bool AreEqual(XafPackage a, XafPackage b) =>
        ListEqual(a.Skeleton.Bones, b.Skeleton.Bones, BoneEqual) &&
        ListEqual(a.Attachments, b.Attachments, AttachmentEqual) &&
        ListEqual(a.Chains, b.Chains, ChainEqual) &&
        ListEqual(a.Clips, b.Clips, ClipEqual) &&
        ListEqual(a.Transitions, b.Transitions, TransitionEqual) &&
        ListEqual(a.Effects, b.Effects, EffectEqual) &&
        ListEqual(a.Cloths, b.Cloths, ClothEqual);

    public static XafCloth Clone(XafCloth cloth)
    {
        var copy = new XafCloth
        {
            Id = cloth.Id,
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
            Fill = cloth.Fill,
            ZOrder = cloth.ZOrder,
        };
        foreach (XafClothPin pin in cloth.Pins)
            copy.Pins.Add(new XafClothPin { Column = pin.Column, Row = pin.Row, BoneId = pin.BoneId });
        foreach (XafClothCollider collider in cloth.Colliders)
            copy.Colliders.Add(new XafClothCollider { BoneId = collider.BoneId, Radius = collider.Radius });
        foreach (XafClothTether tether in cloth.Tethers)
        {
            copy.Tethers.Add(new XafClothTether
            {
                Column = tether.Column, Row = tether.Row, BoneId = tether.BoneId, Radius = tether.Radius,
            });
        }
        return copy;
    }

    public static XafClip Clone(XafClip clip)
    {
        var copy = new XafClip
        {
            Id = clip.Id,
            Duration = clip.Duration,
            Loop = clip.Loop,
            LoopStart = clip.LoopStart,
            LoopEnd = clip.LoopEnd,
            Events = clip.Events.ConvertAll(item => new XafEvent { Time = item.Time, Name = item.Name }),
            Targets = clip.Targets.ConvertAll(target => target with { }),
        };
        foreach (XafChannel channel in clip.Channels)
        {
            var channelCopy = new XafChannel
            {
                Target = channel.Target,
                TargetId = channel.TargetId,
                Property = channel.Property,
            };
            foreach (XafKeyframe key in channel.Keyframes)
            {
                channelCopy.Keyframes.Add(new XafKeyframe
                {
                    Time = key.Time,
                    Value = key.Value,
                    Interpolation = key.Interpolation,
                    TangentIn = key.TangentIn,
                    TangentOut = key.TangentOut,
                });
            }
            copy.Channels.Add(channelCopy);
        }
        return copy;
    }

    private static bool BoneEqual(XafBone a, XafBone b) =>
        a.Id == b.Id && a.Parent == b.Parent && a.Rest == b.Rest &&
        a.ZOrder == b.ZOrder && a.AllowTranslation == b.AllowTranslation && a.Bend == b.Bend &&
        a.Limited == b.Limited && a.LimitMin == b.LimitMin && a.LimitMax == b.LimitMax && a.Tip == b.Tip;

    private static bool AttachmentEqual(XafAttachment a, XafAttachment b) =>
        a.SvgPath == b.SvgPath && a.Group == b.Group && a.BoneId == b.BoneId &&
        a.Transform == b.Transform && a.ZOrder == b.ZOrder && a.SelfModulate == b.SelfModulate &&
        ListEqual(a.Alternates, b.Alternates, (x, y) => x.SvgPath == y.SvgPath && x.Group == y.Group) &&
        MeshEqual(a.Mesh, b.Mesh);

    private static bool MeshEqual(XafMesh? a, XafMesh? b) =>
        a is null || b is null
            ? a is null && b is null
            : a.Detail == b.Detail && a.Blend == b.Blend && ListEqual(a.Bones, b.Bones, (x, y) => x == y) &&
              XafSkinMesh.AreEqual(a.Skin, b.Skin);

    private static bool ChainEqual(XafChain a, XafChain b) =>
        a.Id == b.Id && a.BoneId == b.BoneId && a.Offset == b.Offset &&
        a.Segments == b.Segments && a.SegmentLength == b.SegmentLength &&
        a.Gravity == b.Gravity && a.Damping == b.Damping &&
        a.SvgPath == b.SvgPath && a.Group == b.Group && a.Width == b.Width &&
        a.Lean == b.Lean && a.CollideBoneId == b.CollideBoneId &&
        a.CollideOffset == b.CollideOffset && a.CollideRadius == b.CollideRadius && a.Sheet == b.Sheet &&
        a.Layer == b.Layer;

    private static bool ClipEqual(XafClip a, XafClip b) =>
        a.Id == b.Id && a.Duration == b.Duration && a.Loop == b.Loop &&
        a.LoopStart == b.LoopStart && a.LoopEnd == b.LoopEnd &&
        ListEqual(a.Channels, b.Channels, ChannelEqual) &&
        ListEqual(a.Events, b.Events, (x, y) => x.Time == y.Time && x.Name == y.Name) &&
        ListEqual(a.Targets, b.Targets, (x, y) => x == y);

    private static bool ClothEqual(XafCloth a, XafCloth b) =>
        a.Id == b.Id && a.BoneId == b.BoneId && a.Offset == b.Offset &&
        a.Columns == b.Columns && a.Rows == b.Rows && a.TopWidth == b.TopWidth &&
        a.HemWidth == b.HemWidth && a.Length == b.Length && a.Gravity == b.Gravity &&
        a.Lean == b.Lean && a.Damping == b.Damping && a.Stiffness == b.Stiffness &&
        a.Fill == b.Fill && a.ZOrder == b.ZOrder &&
        ListEqual(a.Pins, b.Pins, (x, y) => x.Column == y.Column && x.Row == y.Row && x.BoneId == y.BoneId) &&
        ListEqual(a.Colliders, b.Colliders, (x, y) => x.BoneId == y.BoneId && x.Radius == y.Radius) &&
        ListEqual(a.Tethers, b.Tethers, (x, y) =>
            x.Column == y.Column && x.Row == y.Row && x.BoneId == y.BoneId && x.Radius == y.Radius);

    private static bool EffectEqual(XafEffect a, XafEffect b) =>
        a.Id == b.Id && a.BoneId == b.BoneId && a.Transform == b.Transform &&
        a.ZOrder == b.ZOrder && XifCopy.AreEqual(a.Item, b.Item);

    private static bool TransitionEqual(XafTransition a, XafTransition b) =>
        a.From == b.From && a.To == b.To && a.Fade == b.Fade && a.Curve == b.Curve;

    private static bool ChannelEqual(XafChannel a, XafChannel b) =>
        a.Target == b.Target && a.TargetId == b.TargetId && a.Property == b.Property &&
        ListEqual(a.Keyframes, b.Keyframes, KeyEqual);

    private static bool KeyEqual(XafKeyframe a, XafKeyframe b) =>
        a.Time == b.Time && a.Value == b.Value && a.Interpolation == b.Interpolation &&
        a.TangentIn == b.TangentIn && a.TangentOut == b.TangentOut;

    private static bool ListEqual<T>(
        List<T> a,
        List<T> b,
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
