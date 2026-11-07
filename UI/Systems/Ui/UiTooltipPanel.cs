using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>
/// What every tooltip system shares: which control is hovered, how wide its
/// text makes the panel, and writing that text and size onto the panel.
/// Where the panel goes and when it shows is up to each system.
/// </summary>
public static class UiTooltipPanel
{
    private const float CharWidth = 6.2f;
    private const float HorizontalPadding = 24f;
    private const float MinWidth = 96f;
    private const float MaxWidth = 360f;

    /// <summary>The topmost hovered entity that carries a tooltip, or default.</summary>
    public static Entity Hovered(EntityStore store)
    {
        foreach (Entity entity in store
                     .Query<UiTooltip>()
                     .AllTags(Tags.Get<Hovered>())
                     .ToEntityList())
        {
            return entity;
        }

        return default;
    }

    /// <summary>The panel size that fits <paramref name="text"/>, within the width limits.</summary>
    public static Vector2 Size(string text, float height) =>
        new(Mathf.Clamp(HorizontalPadding + text.Length * CharWidth, MinWidth, MaxWidth), height);

    /// <summary>Puts the text and size on the panel, marking it dirty only when either changed.</summary>
    public static void SetContent(Entity tooltip, string text, Vector2 size)
    {
        ref Rectangle rectangle = ref tooltip.GetComponent<Rectangle>();
        if (rectangle.Bounds.Size != size)
        {
            rectangle.Bounds = new Rect2(-size / 2, size);
            tooltip.AddTag<Dirty>();
        }

        UiText.Set(tooltip, text);
    }

    /// <summary>Empties the panel's text, so a hidden tooltip does not flash the old one when shown.</summary>
    public static void Clear(Entity tooltip)
    {
        UiText.Set(tooltip, "");
    }
}
