using System;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// Hearing presses of one command enum's buttons through the store's native
/// <c>OnTagsChanged</c>: the input system adds <see cref="Pressed"/> to the
/// pressed entity at once, so the handler runs that frame, before the app's
/// own step, with the command and the entity that carries it.
/// </summary>
public static class UiCommandEvents
{
    /// <summary>
    /// Calls <paramref name="pressed"/> on the frame any entity carrying one of
    /// <typeparamref name="T"/>'s commands goes down. Dispose the result to stop
    /// listening; a tool that outlives its UI must, since the store keeps it.
    /// </summary>
    public static IDisposable OnPressed<T>(EntityStore store, Action<T, UiCommand, Entity> pressed)
        where T : struct, Enum
    {
        void Heard(TagsChanged changed)
        {
            if (!changed.AddedTags.Has<Pressed>())
                return;

            Entity entity = changed.Entity;
            if (entity.TryGetComponent(out UiCommand command) && command.Is(out T id))
                pressed(id, command, entity);
        }

        store.OnTagsChanged += Heard;
        return new Subscription(() => store.OnTagsChanged -= Heard);
    }

    private sealed class Subscription(Action stop) : IDisposable
    {
        private Action? _stop = stop;

        public void Dispose()
        {
            _stop?.Invoke();
            _stop = null;
        }
    }
}
