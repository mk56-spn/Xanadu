using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Cloths in the animator: adding a dress, its settings, and the clicks that
/// pin its points to bones and choose the bones it drapes over.
/// </summary>
public static class AnimatorClothCommands
{
    /// <summary>How near a click must be to a cloth point to pin it, in screen pixels.</summary>
    public const float PointHitRadius = 12f;

    /// <summary>The setting a cloth inspector field edits.</summary>
    public enum Setting
    {
        OffsetX,
        OffsetY,
        Columns,
        Rows,
        TopWidth,
        HemWidth,
        Length,
        Gravity,
        Lean,
        Damping,
        Stiffness,
        Layer,
        HitSize,
    }

    /// <summary>The selected cloth, if it still exists.</summary>
    public static int Selected(Entity session)
    {
        int cloth = session.GetComponent<AnimatorSelection>().Cloth;
        int count = session.GetComponent<AnimatorDocument>().Package.Cloths.Count;
        return cloth >= 0 && cloth < count ? cloth : -1;
    }

    /// <summary>Adds a dress hanging from the selected bone and selects it, as one undo step.</summary>
    public static void Add(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
            int index = XafClothAuthoring.Add(package, selection.Bone);
            if (index < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT THE BONE THE DRESS HANGS FROM FIRST");
                return;
            }

            selection.SelectOnlyCloth(index);
            selection.HasKey = false;
            AnimatorSession.RigChanged(session);
            XafCloth cloth = package.Cloths[index];
            AnimatorSession.SetStatus(session,
                $"ADDED {cloth.Id.ToUpperInvariant()}: {cloth.Pins.Count} POINTS PINNED, {cloth.Colliders.Count} COLLIDERS");
        });

    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = Selected(session);
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            if (index < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A CLOTH FIRST");
                return;
            }

            XafCloth cloth = package.Cloths[index];
            XafClothAuthoring.Delete(package, index);
            session.GetComponent<AnimatorSelection>().SelectOnlyBone(Math.Max(XafRig.FindBone(package, cloth.BoneId), 0));
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(session, $"DELETED {cloth.Id.ToUpperInvariant()}");
        });

    public static void Select(Entity session, int index)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (index < 0 || index >= package.Cloths.Count)
            return;

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyCloth(index);
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, $"SELECTED {package.Cloths[index].Id.ToUpperInvariant()}");
    }

    /// <summary>Starts a pick mode, or stops it when it is already on.</summary>
    public static void TogglePick(Entity session, AnimatorClothPick pick)
    {
        ref AnimatorCloths cloths = ref session.GetComponent<AnimatorCloths>();
        if (Selected(session) < 0)
        {
            cloths.Picking = AnimatorClothPick.None;
            AnimatorSession.SetStatus(session, "SELECT A CLOTH FIRST");
            return;
        }

        cloths.Picking = cloths.Picking == pick ? AnimatorClothPick.None : pick;
        AnimatorSession.SetStatus(session, cloths.Picking switch
        {
            AnimatorClothPick.Bone => "CLICK THE BONE THE CLOTH IS LAID OUT FROM",
            AnimatorClothPick.Pins => "CLICK POINTS TO PIN OR FREE THEM",
            AnimatorClothPick.Colliders => "CLICK BONES THE CLOTH DRAPES OVER",
            _ => "DONE",
        });
    }

    /// <summary>
    /// A click in the preview while a pick mode is on: gives the cloth its
    /// bone, pins or frees the point under the pointer, or toggles the bone
    /// under it as a collider. A click on nothing ends the mode.
    /// </summary>
    public static void Click(Entity session, Vector2 pointer, Transform2D placement, Transform2D[] world)
    {
        ref AnimatorCloths cloths = ref session.GetComponent<AnimatorCloths>();
        AnimatorClothPick pick = cloths.Picking;
        int index = Selected(session);
        if (index < 0)
        {
            cloths.Picking = AnimatorClothPick.None;
            return;
        }

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (pick == AnimatorClothPick.Pins)
        {
            IReadOnlyList<Vector2> points = cloths.Simulation.Points(index);
            int point = AnimatorClothGeometry.PointAt(points, placement, pointer, PointHitRadius);
            if (point < 0)
            {
                cloths.Picking = AnimatorClothPick.None;
                AnimatorSession.SetStatus(session, "DONE PINNING");
                return;
            }

            XafCloth cloth = package.Cloths[index];
            int bone = AnimatorClothGeometry.NearestBone(package, world, points[point]);
            (int column, int row) = (point % cloth.Columns, point / cloth.Columns);
            AnimatorHistoryCommands.Edit(session, () => XafClothAuthoring.TogglePin(package, index, column, row, bone));
            AnimatorSession.SetStatus(session, cloth.IsPinned(column, row)
                ? $"PINNED TO {package.Skeleton.Bones[bone].Id}"
                : "FREED");
            return;
        }

        int picked = AnimatorGeometry.PickBoneOrLabel(world, placement, pointer);
        if (picked < 0)
        {
            cloths.Picking = AnimatorClothPick.None;
            AnimatorSession.SetStatus(session, "DONE");
            return;
        }

        if (pick == AnimatorClothPick.Colliders)
        {
            AnimatorHistoryCommands.Edit(session, () => XafClothAuthoring.ToggleCollider(package, index, picked));
            string id = package.Skeleton.Bones[picked].Id;
            AnimatorSession.SetStatus(session,
                package.Cloths[index].Colliders.Exists(collider => collider.BoneId == id)
                    ? $"{id} IS A COLLIDER"
                    : $"{id} IS NO LONGER A COLLIDER");
            return;
        }

        cloths.Picking = AnimatorClothPick.None;
        AnimatorHistoryCommands.Edit(session, () => SetBone(package, index, picked));
        AnimatorSession.SetStatus(session, $"LAID OUT FROM {package.Skeleton.Bones[picked].Id}");
    }

    /// <summary>Moves the cloth to another bone without moving it: the top edge keeps its rest position.</summary>
    private static void SetBone(XafPackage package, int index, int bone)
    {
        XafCloth cloth = package.Cloths[index];
        Transform2D[] rest = XafRig.WorldBones(package, XafClothLayout.RestPose(package));
        int old = XafRig.FindBone(package, cloth.BoneId);
        Vector2 top = old >= 0 ? rest[old] * cloth.Offset : cloth.Offset;
        cloth.BoneId = package.Skeleton.Bones[bone].Id;
        cloth.Offset = rest[bone].AffineInverse() * top;
    }

    /// <summary>Sets one setting of the selected cloth, as one undo step.</summary>
    public static void Set(Entity session, Setting setting, float value) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = Selected(session);
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            if (index < 0 || !float.IsFinite(value))
            {
                AnimatorSession.SetStatus(session, index < 0 ? "SELECT A CLOTH FIRST" : "NOT A NUMBER");
                return;
            }

            XafCloth cloth = package.Cloths[index];
            float size = Math.Clamp(value, 1f, XafCloth.MaxSize);
            switch (setting)
            {
                case Setting.OffsetX:
                    cloth.Offset.X = value;
                    break;
                case Setting.OffsetY:
                    cloth.Offset.Y = value;
                    break;
                case Setting.Columns:
                    XafClothAuthoring.SetGrid(package, index, (int)MathF.Round(value), cloth.Rows);
                    break;
                case Setting.Rows:
                    XafClothAuthoring.SetGrid(package, index, cloth.Columns, (int)MathF.Round(value));
                    break;
                case Setting.TopWidth:
                    cloth.TopWidth = size;
                    break;
                case Setting.HemWidth:
                    cloth.HemWidth = size;
                    break;
                case Setting.Length:
                    cloth.Length = size;
                    break;
                case Setting.Gravity:
                    cloth.Gravity = value;
                    break;
                case Setting.Lean:
                    cloth.Lean = value;
                    break;
                case Setting.Damping:
                    cloth.Damping = Math.Clamp(value, 0f, 1f);
                    break;
                case Setting.Stiffness:
                    cloth.Stiffness = Math.Clamp(value, 0f, 1f);
                    break;
                case Setting.Layer:
                    cloth.ZOrder = Math.Clamp((int)MathF.Round(value), XafAttachment.MinLayer, XafAttachment.MaxLayer);
                    AnimatorSession.RigChanged(session);
                    break;
                case Setting.HitSize:
                    XafClothAuthoring.SetColliderRadius(package, index, value);
                    break;
            }
            AnimatorSession.SetStatus(session, $"{setting.ToString().ToUpperInvariant()} SET");
        });

    /// <summary>Sets the fill from <c>#rrggbb</c> or <c>#rrggbbaa</c>; anything else is refused.</summary>
    public static void SetFill(Entity session, string typed)
    {
        int index = Selected(session);
        string hex = typed.Trim().TrimStart('#');
        if (index < 0 || hex.Length is not (6 or 8) ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            AnimatorSession.SetStatus(session, index < 0 ? "SELECT A CLOTH FIRST" : "TYPE A COLOUR AS #RRGGBB");
            return;
        }

        AnimatorHistoryCommands.Edit(session, () =>
            session.GetComponent<AnimatorDocument>().Package.Cloths[index].Fill = new Color("#" + hex));
        AnimatorSession.SetStatus(session, "COLOUR SET");
    }
}

/// <summary>Finding cloth points and the bones near them on screen.</summary>
public static class AnimatorClothGeometry
{
    /// <summary>The point nearest the pointer within <paramref name="radius"/> screen pixels, or -1.</summary>
    public static int PointAt(IReadOnlyList<Vector2> points, Transform2D placement, Vector2 pointer, float radius)
    {
        int best = -1;
        float bestDistance = radius;
        for (int i = 0; i < points.Count; i++)
        {
            float distance = (placement * points[i]).DistanceTo(pointer);
            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>The bone whose segment, from its joint to its first child's, is nearest a rig-space point.</summary>
    public static int NearestBone(XafPackage package, Transform2D[] world, Vector2 point)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < package.Skeleton.Bones.Count && i < world.Length; i++)
        {
            var collider = new XafClothCollider { BoneId = package.Skeleton.Bones[i].Id };
            if (XafClothLayout.Capsule(package, collider, world) is not { } segment)
                continue;

            float distance = XafClothLayout.ClosestOnSegment(point, segment.From, segment.To).DistanceTo(point);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }
}
