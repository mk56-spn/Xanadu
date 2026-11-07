using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>Writes UI text and colours without dirtying anything that did not change.</summary>
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
        Dirty(entity, buffer);
    }

    /// <summary>Sets <paramref name="entity"/>'s text colour when it has a style and the colour differs.</summary>
    public static void SetColor(Entity entity, Color color, CommandBuffer? buffer = null)
    {
        if (entity.IsNull || !entity.HasComponent<TextStyle>())
            return;

        ref TextStyle style = ref entity.GetComponent<TextStyle>();
        if (style.Color == color)
            return;

        style.Color = color;
        Dirty(entity, buffer);
    }

    /// <summary>Sets <paramref name="entity"/>'s fill when it has one and the colour differs.</summary>
    public static void SetFill(Entity entity, Color color, CommandBuffer? buffer = null)
    {
        if (entity.IsNull || !entity.HasComponent<FillColor>())
            return;

        ref FillColor fill = ref entity.GetComponent<FillColor>();
        if (fill.Value == color)
            return;

        fill.Value = color;
        Dirty(entity, buffer);
    }

    private static void Dirty(Entity entity, CommandBuffer? buffer)
    {
        if (buffer is null)
            entity.AddTag<Dirty>();
        else
            buffer.AddTag<Dirty>(entity.Id);
    }
}
