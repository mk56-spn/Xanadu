using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>
/// Adding, shaping and pinning cloths. A new cloth is a dress sized to the
/// rig: its top edge at the chest of the bone it hangs from, its bodice pinned
/// to that bone down to the bone's joint (the waist, for a torso that pivots
/// at the hips), and the bones below the waist as colliders, so the skirt
/// drapes over the legs. Any rig gets one that fits; any of it can be changed.
/// </summary>
public static class XafClothAuthoring
{
    /// <summary>Share of the rig's height the default dress is long, and wide at its top and hem.</summary>
    private const float LengthShare = 0.68f;
    private const float TopShare = 0.17f;
    private const float HemShare = 0.38f;

    /// <summary>Share of the dress above the bone's joint: the bodice, pinned.</summary>
    private const float BodiceShare = 0.35f;

    /// <summary>Default collider radius, as a share of the rig's height: about a leg's half-thickness.</summary>
    private const float RadiusShare = 0.04f;

    public const float MinRadius = 0f;
    public const float MaxRadius = 200f;

    /// <summary>Adds a dress hanging from a bone and returns its index, or -1 for a bad bone.</summary>
    public static int Add(XafPackage package, int boneIndex)
    {
        if (boneIndex < 0 || boneIndex >= package.Skeleton.Bones.Count)
            return -1;

        Transform2D[] rest = XafRig.WorldBones(package, XafClothLayout.RestPose(package));
        float height = RigHeight(rest);
        XafBone bone = package.Skeleton.Bones[boneIndex];
        Vector2 joint = rest[boneIndex].Origin;
        var cloth = new XafCloth
        {
            Id = UniqueId(package, "dress"),
            BoneId = bone.Id,
            Length = MathF.Round(height * LengthShare),
            TopWidth = MathF.Round(height * TopShare),
            HemWidth = MathF.Round(height * HemShare),
        };
        cloth.Offset = rest[boneIndex].AffineInverse() * (joint + new Vector2(0f, -cloth.Length * BodiceShare));

        // The bodice: every row down to the joint rides the bone.
        Vector2[] points = XafClothLayout.RestPoints(package, cloth, rest);
        for (int row = 0; row < cloth.Rows; row++)
        {
            if (row > 0 && points[cloth.Index(0, row)].Y > joint.Y + 0.5f)
                break;
            for (int column = 0; column < cloth.Columns; column++)
                cloth.Pins.Add(new XafClothPin { Column = column, Row = row, BoneId = bone.Id });
        }

        // Colliders: the bones whose capsule hangs below the waist inside the skirt's reach.
        float radius = MathF.Round(height * RadiusShare);
        float hem = points[cloth.Index(0, cloth.Rows - 1)].Y;
        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
        {
            if (i == boneIndex || IsAncestor(package, i, boneIndex))
                continue;

            var collider = new XafClothCollider { BoneId = package.Skeleton.Bones[i].Id, Radius = radius };
            if (XafClothLayout.Capsule(package, collider, rest) is not { } capsule)
                continue;

            float middle = (capsule.From.Y + capsule.To.Y) / 2f;
            float top = MathF.Min(capsule.From.Y, capsule.To.Y);
            if (middle > joint.Y + cloth.Length * 0.1f && top < hem)
                cloth.Colliders.Add(collider);
        }

        package.Cloths.Add(cloth);
        return package.Cloths.Count - 1;
    }

    public static bool Delete(XafPackage package, int index)
    {
        if (index < 0 || index >= package.Cloths.Count)
            return false;
        package.Cloths.RemoveAt(index);
        return true;
    }

    /// <summary>Pins a point to a bone, or frees it if it is pinned already. False for a bad point or bone.</summary>
    public static bool TogglePin(XafPackage package, int index, int column, int row, int boneIndex)
    {
        if (index < 0 || index >= package.Cloths.Count || boneIndex < 0 || boneIndex >= package.Skeleton.Bones.Count)
            return false;

        XafCloth cloth = package.Cloths[index];
        if (column < 0 || column >= cloth.Columns || row < 0 || row >= cloth.Rows)
            return false;

        int removed = cloth.Pins.RemoveAll(pin => pin.Column == column && pin.Row == row);
        if (removed == 0)
            cloth.Pins.Add(new XafClothPin { Column = column, Row = row, BoneId = package.Skeleton.Bones[boneIndex].Id });
        return true;
    }

    /// <summary>Makes a bone a collider, or stops it being one. False for a bad cloth or bone.</summary>
    public static bool ToggleCollider(XafPackage package, int index, int boneIndex)
    {
        if (index < 0 || index >= package.Cloths.Count || boneIndex < 0 || boneIndex >= package.Skeleton.Bones.Count)
            return false;

        XafCloth cloth = package.Cloths[index];
        string id = package.Skeleton.Bones[boneIndex].Id;
        if (cloth.Colliders.RemoveAll(collider => collider.BoneId == id) == 0)
        {
            float radius = cloth.Colliders.Count > 0 ? cloth.Colliders[0].Radius : 8f;
            cloth.Colliders.Add(new XafClothCollider { BoneId = id, Radius = radius });
        }
        return true;
    }

    /// <summary>Sets every collider's radius at once.</summary>
    public static bool SetColliderRadius(XafPackage package, int index, float radius)
    {
        if (index < 0 || index >= package.Cloths.Count || !float.IsFinite(radius))
            return false;
        foreach (XafClothCollider collider in package.Cloths[index].Colliders)
            collider.Radius = Math.Clamp(radius, MinRadius, MaxRadius);
        return true;
    }

    /// <summary>
    /// Changes the grid's size, keeping the pins and colliders that still fit:
    /// pins on rows or columns that no longer exist are dropped.
    /// </summary>
    public static bool SetGrid(XafPackage package, int index, int columns, int rows)
    {
        if (index < 0 || index >= package.Cloths.Count)
            return false;

        XafCloth cloth = package.Cloths[index];
        cloth.Columns = Math.Clamp(columns, XafCloth.MinColumns, XafCloth.MaxColumns);
        cloth.Rows = Math.Clamp(rows, XafCloth.MinRows, XafCloth.MaxRows);
        cloth.Pins.RemoveAll(pin => pin.Column >= cloth.Columns || pin.Row >= cloth.Rows);
        return true;
    }

    public static bool UsesBone(XafPackage package, string boneId) =>
        package.Cloths.Exists(cloth =>
            cloth.BoneId == boneId ||
            cloth.Pins.Exists(pin => pin.BoneId == boneId) ||
            cloth.Colliders.Exists(collider => collider.BoneId == boneId));

    public static void RenameBone(XafPackage package, string old, string id)
    {
        foreach (XafCloth cloth in package.Cloths)
        {
            if (cloth.BoneId == old)
                cloth.BoneId = id;
            foreach (XafClothPin pin in cloth.Pins)
            {
                if (pin.BoneId == old)
                    pin.BoneId = id;
            }
            foreach (XafClothCollider collider in cloth.Colliders)
            {
                if (collider.BoneId == old)
                    collider.BoneId = id;
            }
        }
    }

    private static bool IsAncestor(XafPackage package, int candidate, int bone)
    {
        for (int parent = package.Skeleton.Bones[bone].Parent; parent >= 0; parent = package.Skeleton.Bones[parent].Parent)
        {
            if (parent == candidate)
                return true;
        }
        return false;
    }

    /// <summary>The rest pose's height from the highest joint to the lowest; a fallback for a one-bone rig.</summary>
    private static float RigHeight(Transform2D[] rest)
    {
        if (rest.Length == 0)
            return 180f;

        float top = float.MaxValue;
        float bottom = float.MinValue;
        foreach (Transform2D bone in rest)
        {
            top = MathF.Min(top, bone.Origin.Y);
            bottom = MathF.Max(bottom, bone.Origin.Y);
        }
        return bottom - top > 1f ? bottom - top : 180f;
    }

    private static string UniqueId(XafPackage package, string stem)
    {
        if (!package.Cloths.Exists(cloth => cloth.Id == stem))
            return stem;
        for (int n = 2; ; n++)
        {
            string id = $"{stem}_{n}";
            if (!package.Cloths.Exists(cloth => cloth.Id == id))
                return id;
        }
    }
}

/// <summary>What makes a cloth invalid in a file.</summary>
public static class XafClothValidation
{
    public static void Validate(XafPackage package, HashSet<string> boneIds, List<string> issues)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (XafCloth cloth in package.Cloths)
        {
            if (string.IsNullOrWhiteSpace(cloth.Id))
                issues.Add("Cloth has an empty id.");
            else if (!ids.Add(cloth.Id))
                issues.Add($"Duplicate cloth id '{cloth.Id}'.");
            if (!boneIds.Contains(cloth.BoneId))
                issues.Add($"Cloth '{cloth.Id}' references missing bone '{cloth.BoneId}'.");
            if (cloth.Columns < XafCloth.MinColumns || cloth.Columns > XafCloth.MaxColumns ||
                cloth.Rows < XafCloth.MinRows || cloth.Rows > XafCloth.MaxRows)
            {
                issues.Add($"Cloth '{cloth.Id}' has an invalid grid.");
            }
            if (!InRange(cloth.TopWidth) || !InRange(cloth.HemWidth) || !(cloth.Length > 0f) || !InRange(cloth.Length))
                issues.Add($"Cloth '{cloth.Id}' has an invalid size.");
            if (!float.IsFinite(cloth.Gravity) || !float.IsFinite(cloth.Lean) ||
                !float.IsFinite(cloth.Offset.X) || !float.IsFinite(cloth.Offset.Y))
            {
                issues.Add($"Cloth '{cloth.Id}' has a non-finite value.");
            }
            if (!(cloth.Damping >= 0f && cloth.Damping <= 1f) || !(cloth.Stiffness >= 0f && cloth.Stiffness <= 1f))
                issues.Add($"Cloth '{cloth.Id}' has an invalid damping or stiffness.");
            if (cloth.ZOrder < XafAttachment.MinLayer || cloth.ZOrder > XafAttachment.MaxLayer)
                issues.Add($"Cloth '{cloth.Id}' has a layer out of range.");
            foreach (XafClothPin pin in cloth.Pins)
            {
                if (pin.Column < 0 || pin.Column >= cloth.Columns || pin.Row < 0 || pin.Row >= cloth.Rows)
                    issues.Add($"Cloth '{cloth.Id}' pins a point outside its grid.");
                if (!boneIds.Contains(pin.BoneId))
                    issues.Add($"Cloth '{cloth.Id}' pins to missing bone '{pin.BoneId}'.");
            }
            foreach (XafClothCollider collider in cloth.Colliders)
            {
                if (!boneIds.Contains(collider.BoneId))
                    issues.Add($"Cloth '{cloth.Id}' collides with missing bone '{collider.BoneId}'.");
                if (!(collider.Radius >= XafClothAuthoring.MinRadius && collider.Radius <= XafClothAuthoring.MaxRadius))
                    issues.Add($"Cloth '{cloth.Id}' has an invalid collider radius.");
            }
        }
    }

    private static bool InRange(float value) => value >= 0f && value <= XafCloth.MaxSize;
}
