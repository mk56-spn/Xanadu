using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Verlet chains: the swinging strands that hang off a bone.</summary>
public static class AnimatorChainCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<AddChain>((s, _) => Add(s));
        session.On<SelectChain>((s, e) => Select(s, e.Chain));
        session.On<ToggleChainSheet>((s, _) => ToggleSheet(s));
        session.On<DeleteChain>((s, _) => Delete(s));
    }

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
        Layer,
    }

    /// <summary>
    /// Hangs a new chain off the selected bone and selects it, as one undo step.
    /// The simulation picks it up on its own; the rig is rebuilt so the chain list shows it.
    /// </summary>
    public static void Add(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => AddCore(session));

    private static void AddCore(Entity session)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        int index = RigChainAuthoring.Add(package, selection.Bone);
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

    /// <summary>Draws the selected chain as one sheet, or as pieces again, as one undo step.</summary>
    public static void ToggleSheet(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            int index = session.GetComponent<AnimatorSelection>().Chain;
            if (index < 0 || index >= package.Chains.Count)
            {
                AnimatorSession.SetStatus(session, "SELECT A CHAIN FIRST");
                return;
            }

            RigChain chain = package.Chains[index];
            chain.Sheet = !chain.Sheet;
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(session, $"{chain.Id} DRAWS AS {(chain.Sheet ? "ONE SHEET" : "PIECES")}");
        });

    /// <summary>Deletes the selected chain, as one undo step.</summary>
    public static void Delete(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => DeleteCore(session));

    private static void DeleteCore(Entity session)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        int index = selection.Chain;
        if (index < 0 || index >= package.Chains.Count)
        {
            AnimatorSession.SetStatus(session, "SELECT A CHAIN FIRST");
            return;
        }

        RigChain chain = package.Chains[index];
        string id = chain.Id;
        RigChainAuthoring.Delete(package, index);

        // Leave the bone it hung from selected, so the next step has somewhere to go.
        selection.SelectOnlyBone(Math.Max(RigRig.FindBone(package, chain.BoneId), 0));
        AnimatorSession.RigChanged(session);
        AnimatorSession.SetStatus(session, $"DELETED {id}");
    }

    public static void Select(Entity session, int index)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (index < 0 || index >= package.Chains.Count)
            return;

        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.SelectOnlyChain(index);
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, $"SELECTED {package.Chains[index].Id}");
    }

    /// <summary>One setting of the selected chain; false when no chain is selected.</summary>
    public static bool TryGet(Entity session, Setting setting, out float value)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        int index = session.GetComponent<AnimatorSelection>().Chain;
        value = 0f;
        if (index < 0 || index >= package.Chains.Count)
            return false;
        RigChain chain = package.Chains[index];
        value = setting switch
        {
            Setting.Segments => chain.Segments,
            Setting.Length => chain.SegmentLength,
            Setting.Width => chain.Width,
            Setting.Layer => chain.Layer ?? 0,
            Setting.Gravity => chain.Gravity,
            Setting.Lean => chain.Lean,
            Setting.Collide => chain.CollideRadius,
            Setting.OffsetX => chain.Offset.X,
            Setting.OffsetY => chain.Offset.Y,
            Setting.ColliderX => chain.CollideOffset.X,
            Setting.ColliderY => chain.CollideOffset.Y,
            _ => chain.Damping,
        };
        return true;
    }

    /// <summary>Sets one setting of the selected chain, as one undo step.</summary>
    public static void Set(Entity session, Setting setting, float value) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            RigPackage package = session.GetComponent<AnimatorDocument>().Package;
            int index = session.GetComponent<AnimatorSelection>().Chain;
            RigChain? chain = index >= 0 && index < package.Chains.Count ? package.Chains[index] : null;
            bool changed = setting switch
            {
                Setting.OffsetX when chain is not null =>
                    RigChainAuthoring.SetOffset(package, index, new Vector2(value, chain.Offset.Y)),
                Setting.OffsetY when chain is not null =>
                    RigChainAuthoring.SetOffset(package, index, new Vector2(chain.Offset.X, value)),
                Setting.ColliderX when chain is not null =>
                    RigChainAuthoring.SetCollideOffset(package, index, new Vector2(value, chain.CollideOffset.Y)),
                Setting.ColliderY when chain is not null =>
                    RigChainAuthoring.SetCollideOffset(package, index, new Vector2(chain.CollideOffset.X, value)),
                Setting.Segments => RigChainAuthoring.SetSegments(package, index, (int)MathF.Round(value)),
                Setting.Length => RigChainAuthoring.SetSegmentLength(package, index, value),
                Setting.Width => RigChainAuthoring.SetWidth(package, index, value),
                Setting.Layer => RigChainAuthoring.SetLayer(package, index, (int)MathF.Round(value)),
                Setting.Gravity => RigChainAuthoring.SetGravity(package, index, value),
                Setting.Lean => RigChainAuthoring.SetLean(package, index, value),
                Setting.Collide => RigChainAuthoring.SetCollideRadius(package, index, value),
                _ => RigChainAuthoring.SetDamping(package, index, value),
            };
            if (changed && setting == Setting.Layer)
                AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(
                session,
                changed ? $"{setting.ToString().ToUpperInvariant()} SET" : "SELECT A CHAIN FIRST");
        });
}
