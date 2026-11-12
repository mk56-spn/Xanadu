using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Runs the <see cref="ToolAction"/> of each entity on the frame it is first pressed.</summary>
public sealed class AnimatorActionSystem : QuerySystem<UiCommand>
{
    private readonly HashSet<int> _held = new();
    private readonly List<ToolAction> _requests = new();

    /// <summary>A press can land on a menu item that hides itself the same frame.</summary>
    public AnimatorActionSystem() => Filter.WithDisabled();

    protected override void OnUpdate()
    {
        _requests.Clear();
        foreach (Entity entity in Query.ToEntityList())
        {
            if (!ToolAction.From(entity.GetComponent<UiCommand>(), out ToolAction action))
                continue;

            bool pressed = entity.Tags.Has<Pressed>();
            bool wasHeld = _held.Contains(entity.Id);
            if (pressed)
                _held.Add(entity.Id);
            else
                _held.Remove(entity.Id);

            if (pressed && !wasHeld)
                _requests.Add(action);
        }

        if (_requests.Count == 0)
            return;

        Entity session = AnimatorSession.Find(Query.Store);
        if (session.IsNull)
            return;

        foreach (ToolAction action in _requests)
            AnimatorCommands.Run(session, action);
    }
}
