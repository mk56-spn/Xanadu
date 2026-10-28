using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Xanadu.Ecs;

/// <summary>Writes UI text without dirtying anything that did not change.</summary>
public static class UiText
{
    /// <summary>
    /// Sets <paramref name="entity"/>'s text when it has one and the value
    /// differs. Pass a <paramref name="buffer"/> inside a system so the change is
    /// deferred; outside one the entity is dirtied directly.
    /// </summary>
    public static void Set(Entity entity, string value, CommandBuffer? buffer = null)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        ref Text text = ref entity.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;

        if (buffer is null)
            entity.AddTag<Dirty>();
        else
            buffer.AddTag<Dirty>(entity.Id);
    }
}
