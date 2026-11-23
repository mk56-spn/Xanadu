using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Core.Layering;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Cloths in the animator: adding a dress, its settings, and the clicks that
/// pin its points to bones and choose the bones it drapes over.
/// </summary>
public static class AnimatorClothCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<AddCloth>((s, _) => Add(s));
        session.On<SelectCloth>((s, e) => Select(s, e.Cloth));
        session.On<DeleteCloth>((s, _) => Delete(s));
        session.On<PickCloth>((s, e) => TogglePick(s, e.Pick));
    }

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
        TetherReach,
    }

    /// <summary>Adds a dress hanging from the selected bone and selects it, as one undo step.</summary>
    public static void Add(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
            int index = RigClothAuthoring.Add(package, selection.Bone);
            if (index < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT THE BONE THE DRESS HANGS FROM FIRST");
                return;
            }

            selection.SelectOnlyCloth(index);
            selection.HasKey = false;
            AnimatorSession.RigChanged(session);
            RigCloth cloth = package.Cloths[index];
            AnimatorSession.SetStatus(session,
                $"ADDED {cloth.Id.ToUpperInvariant()}: {cloth.Pins.Count} POINTS PINNED, {cloth.Colliders.Count} COLLIDERS");
        });

    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = AnimatorSelected.Cloth(session);
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            if (index < 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A CLOTH FIRST");
                return;
            }

            RigCloth cloth = package.Cloths[index];
            RigClothAuthoring.Delete(package, index);
            session.GetComponent<AnimatorSelection>().SelectOnlyBone(Math.Max(RigRig.FindBone(package, cloth.BoneId), 0));
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(session, $"DELETED {cloth.Id.ToUpperInvariant()}");
        });

    public static void Select(Entity session, int index)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
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
        if (AnimatorSelected.Cloth(session) < 0)
        {
            cloths.Picking = AnimatorClothPick.None;
            AnimatorSession.SetStatus(session, "SELECT A CLOTH FIRST");
            return;
        }

        cloths.Picking = cloths.Picking == pick ? AnimatorClothPick.None : pick;
        AnimatorSession.SetStatus(session, cloths.Picking switch
        {
            AnimatorClothPick.Bone => "CLICK THE BONE THE CLOTH IS LAID OUT FROM",
            AnimatorClothPick.Pins => "CLICK A POINT: FREE > PINNED > TETHERED > FREE",
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
        int index = AnimatorSelected.Cloth(session);
        if (index < 0)
        {
            cloths.Picking = AnimatorClothPick.None;
            return;
        }

        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
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

            RigCloth cloth = package.Cloths[index];
            int bone = AnimatorClothGeometry.NearestBone(package, world, points[point]);
            (int column, int row) = (point % cloth.Columns, point / cloth.Columns);
            AnimatorHistoryCommands.Edit(session, () => RigClothAuthoring.CyclePoint(package, index, column, row, bone));
            string boneId = package.Skeleton.Bones[bone].Id;
            AnimatorSession.SetStatus(session,
                cloth.IsPinned(column, row) ? $"PINNED TO {boneId}" :
                cloth.IsTethered(column, row) ? $"TETHERED TO {boneId}" :
                "FREED");
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
            AnimatorHistoryCommands.Edit(session, () => RigClothAuthoring.ToggleCollider(package, index, picked));
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
    private static void SetBone(RigPackage package, int index, int bone)
    {
        RigCloth cloth = package.Cloths[index];
        Transform2D[] rest = RigRig.WorldBones(package, RigClothLayout.RestPose(package));
        int old = RigRig.FindBone(package, cloth.BoneId);
        Vector2 top = old >= 0 ? rest[old] * cloth.Offset : cloth.Offset;
        cloth.BoneId = package.Skeleton.Bones[bone].Id;
        cloth.Offset = rest[bone].AffineInverse() * top;
    }

    /// <summary>One setting of the selected cloth; false when no cloth is selected.</summary>
    public static bool TryGet(Entity session, Setting setting, out float value)
    {
        int index = AnimatorSelected.Cloth(session);
        value = 0f;
        if (index < 0)
            return false;
        RigCloth cloth = session.GetComponent<AnimatorDocument>().Package.Cloths[index];
        value = setting switch
        {
            Setting.OffsetX => cloth.Offset.X,
            Setting.OffsetY => cloth.Offset.Y,
            Setting.Columns => cloth.Columns,
            Setting.Rows => cloth.Rows,
            Setting.TopWidth => cloth.TopWidth,
            Setting.HemWidth => cloth.HemWidth,
            Setting.Length => cloth.Length,
            Setting.Gravity => cloth.Gravity,
            Setting.Lean => cloth.Lean,
            Setting.Damping => cloth.Damping,
            Setting.Stiffness => cloth.Stiffness,
            Setting.Layer => cloth.ZOrder,
            Setting.HitSize => cloth.Colliders.Count > 0 ? cloth.Colliders[0].Radius : 0f,
            _ => cloth.Tethers.Count > 0 ? cloth.Tethers[0].Radius : 0f,
        };
        return true;
    }

    /// <summary>Sets one setting of the selected cloth, as one undo step.</summary>
    public static void Set(Entity session, Setting setting, float value) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            int index = AnimatorSelected.Cloth(session);
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            if (index < 0 || !float.IsFinite(value))
            {
                AnimatorSession.SetStatus(session, index < 0 ? "SELECT A CLOTH FIRST" : "NOT A NUMBER");
                return;
            }

            RigCloth cloth = package.Cloths[index];
            float size = Math.Clamp(value, 1f, RigCloth.MaxSize);
            switch (setting)
            {
                case Setting.OffsetX:
                    cloth.Offset.X = value;
                    break;
                case Setting.OffsetY:
                    cloth.Offset.Y = value;
                    break;
                case Setting.Columns:
                    RigClothAuthoring.SetGrid(package, index, (int)MathF.Round(value), cloth.Rows);
                    break;
                case Setting.Rows:
                    RigClothAuthoring.SetGrid(package, index, cloth.Columns, (int)MathF.Round(value));
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
                    cloth.ZOrder = DrawOrder.ClampLayer((int)MathF.Round(value));
                    AnimatorSession.RigChanged(session);
                    break;
                case Setting.HitSize:
                    RigClothAuthoring.SetColliderRadius(package, index, value);
                    break;
                case Setting.TetherReach:
                    RigClothAuthoring.SetTetherRadius(package, index, value);
                    break;
            }
            AnimatorSession.SetStatus(session, $"{setting.ToString().ToUpperInvariant()} SET");
        });
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
    public static int NearestBone(RigPackage package, Transform2D[] world, Vector2 point)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < package.Skeleton.Bones.Count && i < world.Length; i++)
        {
            var collider = new RigClothCollider { BoneId = package.Skeleton.Bones[i].Id };
            if (RigClothLayout.Capsule(package, collider, world) is not { } segment)
                continue;

            float distance = RigClothLayout.ClosestOnSegment(point, segment.From, segment.To).DistanceTo(point);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }
}
