using System.Collections.Generic;
using Xanadu.Core.Theme;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>What a row of the rig tree is: a bone, part, chain or cloth, by its index in the package.</summary>
public readonly record struct AnimatorRigRow(AnimatorRigRowKind Kind, int Index);

public enum AnimatorRigRowKind
{
    Bone,
    Part,
    Chain,
    Cloth,
}

/// <summary>
/// The rig as one tree: bones, with the parts, chains and cloth that hang off
/// each under it, every kind in its own colour. The UI tree keys its rows by
/// text, so <see cref="TreeId"/> and <see cref="Row"/> are the one place a row
/// becomes text and back: bone rows keep the bone's id, the others are
/// "part:3", "chain:0", "cloth:1", so ids never clash.
/// A bone something else drives in the current clip, such as a limb bent by a
/// pin or an ellipse, carries that target's shape after its name: solid on the
/// limb's end, faint on the bones it bends.
/// </summary>
public static class AnimatorRigTree
{
    public static readonly Color Bone = UiTheme.Text;
    public static readonly Color Part = Palette.Accent;
    public static readonly Color Chain = Palette.Info;
    public static readonly Color Cloth = Palette.Rose;

    private const string PartPrefix = "part:";
    private const string ChainPrefix = "chain:";
    private const string ClothPrefix = "cloth:";

    private static string PartId(int index) => PartPrefix + index;
    private static string ChainId(int index) => ChainPrefix + index;
    private static string ClothId(int index) => ClothPrefix + index;

    /// <summary>The tree's key for a row.</summary>
    public static string TreeId(AnimatorRigRow row, RigPackage package) => row.Kind switch
    {
        AnimatorRigRowKind.Part => PartId(row.Index),
        AnimatorRigRowKind.Chain => ChainId(row.Index),
        AnimatorRigRowKind.Cloth => ClothId(row.Index),
        _ => row.Index >= 0 && row.Index < package.Skeleton.Bones.Count ? package.Skeleton.Bones[row.Index].Id : "",
    };

    /// <summary>The row a tree key names, or null when nothing in the package has it.</summary>
    public static AnimatorRigRow? Row(string? id, RigPackage package)
    {
        if (id is null)
            return null;
        if (Indexed(id, PartPrefix, package.Attachments.Count) is int part)
            return new AnimatorRigRow(AnimatorRigRowKind.Part, part);
        if (Indexed(id, ChainPrefix, package.Chains.Count) is int chain)
            return new AnimatorRigRow(AnimatorRigRowKind.Chain, chain);
        if (Indexed(id, ClothPrefix, package.Cloths.Count) is int cloth)
            return new AnimatorRigRow(AnimatorRigRowKind.Cloth, cloth);
        int bone = RigRig.FindBone(package, id);
        return bone >= 0 ? new AnimatorRigRow(AnimatorRigRowKind.Bone, bone) : null;
    }

    private static int? Indexed(string id, string prefix, int count) =>
        id.StartsWith(prefix, StringComparison.Ordinal) &&
        int.TryParse(id.AsSpan(prefix.Length), out int index) && index >= 0 && index < count
            ? index
            : null;

    /// <summary>Every row of the rig, in the order the package holds them.</summary>
    public static List<UiTreeItem> Items(RigPackage package, RigClip? clip)
    {
        var items = new List<UiTreeItem>();
        var bones = new HashSet<string>();
        Dictionary<string, List<UiTreeBadge>> owners = Owners(package, clip);
        foreach (RigBone bone in package.Skeleton.Bones)
        {
            string? parent = bone.Parent >= 0 ? package.Skeleton.Bones[bone.Parent].Id : null;
            UiTreeBadge[]? badges = owners.TryGetValue(bone.Id, out List<UiTreeBadge>? by) ? [.. by] : null;
            items.Add(new UiTreeItem(bone.Id, parent, bone.Id, Tint: Bone, Badges: badges));
            bones.Add(bone.Id);
        }

        for (int i = 0; i < package.Attachments.Count; i++)
        {
            RigAttachment part = package.Attachments[i];
            int above = package.AttachmentParents[i];
            string? parent = above >= 0 ? PartId(above) : bones.Contains(part.BoneId) ? part.BoneId : null;
            items.Add(new UiTreeItem(PartId(i), parent, part.Id + (part.Hidden ? "  (hidden)" : ""), Tint: Part));
        }

        for (int i = 0; i < package.Chains.Count; i++)
        {
            RigChain chain = package.Chains[i];
            items.Add(new UiTreeItem(ChainId(i), bones.Contains(chain.BoneId) ? chain.BoneId : null, chain.Id, Tint: Chain));
        }

        for (int i = 0; i < package.Cloths.Count; i++)
        {
            RigCloth cloth = package.Cloths[i];
            items.Add(new UiTreeItem(ClothId(i), bones.Contains(cloth.BoneId) ? cloth.BoneId : null, cloth.Id, Tint: Cloth));
        }
        return items;
    }

    /// <summary>A fingerprint of what <see cref="Owners"/> reads, so the tree is refreshed only when it changes.</summary>
    public static int OwnersKey(RigClip? clip)
    {
        var hash = new HashCode();
        hash.Add(clip?.Id);
        if (clip is not null)
        {
            foreach (RigLimbTarget target in clip.Targets)
            {
                hash.Add(target.BoneId);
                hash.Add(target.Shape);
                hash.Add(target.HoldFacing);
            }
        }
        return hash.ToHashCode();
    }

    /// <summary>The colour of pins and ellipses, as the preview draws them.</summary>
    public static readonly Color Target = Palette.Warning;

    /// <summary>
    /// What drives each bone in the clip besides its own keys: the pins and
    /// ellipses whose baked keys sit on it, as badges in their own shape.
    /// </summary>
    public static Dictionary<string, List<UiTreeBadge>> Owners(RigPackage package, RigClip? clip)
    {
        var owners = new Dictionary<string, List<UiTreeBadge>>();
        if (clip is null)
            return owners;

        foreach (RigLimbTarget target in clip.Targets)
        {
            if (!RigTargets.IsValid(package, target))
                continue;
            bool ellipse = target.Shape == RigTargetShape.Ellipse;
            UiSdfShape shape = ellipse ? UiSdfShape.Ellipse : UiSdfShape.Box;
            string kind = ellipse ? "ellipse" : "pin";
            Add(target.BoneId, new UiTreeBadge(shape, Target, Filled: !ellipse, Tip: $"Follows a {kind}"));
            foreach ((int bone, _) in RigTargets.Channels(package, target))
            {
                string id = package.Skeleton.Bones[bone].Id;
                if (id != target.BoneId)
                    Add(id, new UiTreeBadge(shape, new Color(Target, 0.5f), Filled: !ellipse, Tip: $"Bent by the {kind} on {target.BoneId}"));
            }
        }
        return owners;

        void Add(string bone, UiTreeBadge badge)
        {
            if (!owners.TryGetValue(bone, out List<UiTreeBadge>? list))
                owners[bone] = list = [];
            if (!list.Contains(badge))
                list.Add(badge);
        }
    }

    /// <summary>The intent a click on a row sends: selecting that bone, part, chain or cloth.</summary>
    public static IIntent Select(AnimatorRigRow row) => row.Kind switch
    {
        AnimatorRigRowKind.Part => new SelectPart(row.Index),
        AnimatorRigRowKind.Chain => new SelectChain(row.Index),
        AnimatorRigRowKind.Cloth => new SelectCloth(row.Index),
        _ => new SelectBone(row.Index),
    };

    /// <summary>What deleting a part, chain or cloth row runs, and what its button says.</summary>
    public static (string Label, IIntent Kind) Delete(AnimatorRigRow row) => row.Kind switch
    {
        AnimatorRigRowKind.Chain => ("DELETE CHAIN", new DeleteChain()),
        AnimatorRigRowKind.Cloth => ("DELETE CLOTH", new DeleteCloth()),
        _ => ("DELETE PART", new DeleteEffect()),
    };

    /// <summary>The row the selection shows on: a chain, a cloth, a part, else the bone.</summary>
    public static AnimatorRigRow Selected(in AnimatorSelection selection, RigPackage package)
    {
        if (selection.Chain >= 0 && selection.Chain < package.Chains.Count)
            return new AnimatorRigRow(AnimatorRigRowKind.Chain, selection.Chain);
        if (selection.Cloth >= 0 && selection.Cloth < package.Cloths.Count)
            return new AnimatorRigRow(AnimatorRigRowKind.Cloth, selection.Cloth);
        if (selection.Attachment >= 0 && selection.Attachment < package.Attachments.Count)
            return new AnimatorRigRow(AnimatorRigRowKind.Part, selection.Attachment);
        return new AnimatorRigRow(AnimatorRigRowKind.Bone, selection.Bone);
    }
}
