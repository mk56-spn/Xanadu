using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Verlet chains: the swinging strands that hang off a bone.</summary>
public static class AnimatorChainCommands
{
    /// <summary>The setting a chain inspector field edits.</summary>
    public enum Setting
    {
        Segments,
        Length,
        Width,
        Gravity,
        Lean,
        Damping,
        Collide,
        OffsetX,
        OffsetY,
        ColliderX,
        ColliderY,
    }

    /// <summary>
    /// Hangs a new chain off the selected bone and selects it, as one undo step.
    /// The simulation picks it up on its own; the rig is rebuilt so the chain list shows it.
    /// </summary>
    public static void Add(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => AddCore(session));

    private static void AddCore(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        int index = XafChainAuthoring.Add(package, selection.Bone);
        if (index < 0)
        {
            AnimatorSession.SetStatus(session, "SELECT A BONE FIRST");
            return;
        }

        selection.SelectOnlyChain(index);
        selection.HasKey = false;
        AnimatorSession.RigChanged(session);
        AnimatorSession.SetStatus(session, $"ADDED {package.Chains[index].Id}");
    }

    /// <summary>Deletes the selected chain, as one undo step.</summary>
    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => DeleteCore(session));

    private static void DeleteCore(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        int index = selection.Chain;
        if (index < 0 || index >= package.Chains.Count)
        {
            AnimatorSession.SetStatus(session, "SELECT A CHAIN FIRST");
            return;
        }

        XafChain chain = package.Chains[index];
        string id = chain.Id;
        XafChainAuthoring.Delete(package, index);

        // Leave the bone it hung from selected, so the next step has somewhere to go.
        selection.SelectOnlyBone(Math.Max(XafRig.FindBone(package, chain.BoneId), 0));
        AnimatorSession.RigChanged(session);
        AnimatorSession.SetStatus(session, $"DELETED {id}");
    }

    public static void Select(Entity session, int index)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (index < 0 || index >= package.Chains.Count)
            return;

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyChain(index);
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, $"SELECTED {package.Chains[index].Id}");
    }

    /// <summary>Sets one setting of the selected chain, as one undo step.</summary>
    public static void Set(Entity session, Setting setting, float value) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            int index = session.GetComponent<AnimatorSelection>().Chain;
            XafChain? chain = index >= 0 && index < package.Chains.Count ? package.Chains[index] : null;
            bool changed = setting switch
            {
                Setting.OffsetX when chain is not null =>
                    XafChainAuthoring.SetOffset(package, index, new Vector2(value, chain.Offset.Y)),
                Setting.OffsetY when chain is not null =>
                    XafChainAuthoring.SetOffset(package, index, new Vector2(chain.Offset.X, value)),
                Setting.ColliderX when chain is not null =>
                    XafChainAuthoring.SetCollideOffset(package, index, new Vector2(value, chain.CollideOffset.Y)),
                Setting.ColliderY when chain is not null =>
                    XafChainAuthoring.SetCollideOffset(package, index, new Vector2(chain.CollideOffset.X, value)),
                Setting.Segments => XafChainAuthoring.SetSegments(package, index, (int)MathF.Round(value)),
                Setting.Length => XafChainAuthoring.SetSegmentLength(package, index, value),
                Setting.Width => XafChainAuthoring.SetWidth(package, index, value),
                Setting.Gravity => XafChainAuthoring.SetGravity(package, index, value),
                Setting.Lean => XafChainAuthoring.SetLean(package, index, value),
                Setting.Collide => XafChainAuthoring.SetCollideRadius(package, index, value),
                _ => XafChainAuthoring.SetDamping(package, index, value),
            };
            AnimatorSession.SetStatus(
                session,
                changed ? $"{setting.ToString().ToUpperInvariant()} SET" : "SELECT A CHAIN FIRST");
        });
}
