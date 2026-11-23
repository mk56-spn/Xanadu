using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Animator;

/// <summary>
/// Sends each button's <see cref="Intent"/> to the session on the frame the
/// button is first pressed; the session's listeners (<c>AnimatorCommands.Listen</c>) run it.
/// </summary>
public sealed class AnimatorActionSystem : QuerySystem<Intent>
{
    private readonly HashSet<int> _held = [];
    private readonly List<IIntent> _asked = [];

    /// <summary>A press can land on a menu item that hides itself the same frame.</summary>
    public AnimatorActionSystem() => Filter.WithDisabled();

    protected override void OnUpdate()
    {
        _asked.Clear();
        foreach (Entity entity in Query.Entities)
        {
            bool pressed = entity.Tags.Has<Pressed>();
            bool wasHeld = _held.Contains(entity.Id);
            if (pressed)
                _held.Add(entity.Id);
            else
                _held.Remove(entity.Id);

            if (pressed && !wasHeld)
                _asked.Add(entity.GetComponent<Intent>().Value);
        }

        if (_asked.Count == 0)
            return;

        Entity session = AnimatorSession.Find(Query.Store);
        if (session.IsNull)
            return;

        // Emitted after the loop: handlers rebuild panels.
        foreach (IIntent intent in _asked)
            intent.EmitOn(session);
    }
}
