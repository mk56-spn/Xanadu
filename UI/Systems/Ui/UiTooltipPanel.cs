using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// What every tooltip system shares: which control is hovered, how big its
/// text makes the panel, and writing that text and size onto the panel.
/// Text that fits on one line keeps a one-line panel as wide as it; longer text
/// wraps at <see cref="WrapWidth"/> and the panel grows as tall as its lines.
/// A panel made by <see cref="Create"/> holds its text in a label inside it,
/// inset by the padding; one that holds its own text still shows one line.
/// Where the panel goes and when it shows is up to each system.
/// </summary>
public static class UiTooltipPanel
{
    private const float CharWidth = 6.2f;
    private const float HorizontalPadding = 24f;
    private const float MinWidth = 96f;
    private const float MaxWidth = 360f;

    /// <summary>The panel's width once its text wraps.</summary>
    private const float WrapWidth = 480f;

    private const float VerticalPadding = 8f;

    /// <summary>
    /// A hidden tooltip panel of one-line <paramref name="height"/> on the builder's layer, its
    /// text in a label child that wraps when the text is long.
    /// </summary>
    public static Entity Create(UiBuilder ui, float height)
    {
        Vector2 size = Size("", height);
        Entity panel = ui.RectEntity(Vector2.Zero, size, new Color(UiTheme.Tooltip, 0.96f), takesInput: false);
        Entity label = ui.Use(panel).RectEntity(Vector2.Zero, size - new Vector2(HorizontalPadding, 0), new Color(0, 0, 0, 0), "", takesInput: false);
        ui.End();
        label.AddComponent(new TextStyle(UiTheme.SmallFontSize, UiTheme.Text));
        UiLayout.SetHidden(panel, true);
        return panel;
    }

    /// <summary>The entity that carries a panel's text: its label, or the panel itself.</summary>
    public static Entity Label(Entity tooltip)
    {
        foreach (Entity child in tooltip.ChildEntities)
        {
            if (child.HasComponent<Text>())
                return child;
        }

        return tooltip;
    }

    /// <summary>The topmost hovered entity that carries a tooltip, or default.</summary>
    public static Entity Hovered(EntityStore store)
    {
        foreach (Entity entity in store
                     .Query<UiTooltip>()
                     .AllTags(Tags.Get<Hovered>())
                     .Entities)
        {
            return entity;
        }

        return default;
    }

    /// <summary>
    /// The panel size that fits <paramref name="text"/>: one line within the width limits, or,
    /// past <see cref="MaxWidth"/>, wrapped at <see cref="WrapWidth"/> and as tall as its lines.
    /// </summary>
    public static Vector2 Size(string text, float height)
    {
        float line = HorizontalPadding + text.Length * CharWidth;
        if (line <= MaxWidth)
            return new(Math.Max(line, MinWidth), height);
        float lines = UiTextFit.WrappedHeight(text, UiTheme.SmallFontSize, WrapWidth - HorizontalPadding);
        return new(WrapWidth, Math.Max(height, lines + 2 * VerticalPadding));
    }

    /// <summary>Whether <see cref="Size"/> wraps this text.</summary>
    private static bool Wraps(string text) => HorizontalPadding + text.Length * CharWidth > MaxWidth;

    /// <summary>Puts the text and size on the panel (and its label, inset), marking them dirty only when either changed.</summary>
    public static void SetContent(Entity tooltip, string text, Vector2 size)
    {
        Resize(tooltip, size);
        Entity label = Label(tooltip);
        if (label != tooltip)
        {
            bool wraps = Wraps(text);
            Resize(label, size - (wraps ? new Vector2(HorizontalPadding, 2 * VerticalPadding) : new Vector2(HorizontalPadding, 0)));
            if (wraps != label.Tags.Has<TextWrap>())
            {
                if (wraps)
                    label.AddTag<TextWrap>();
                else
                    label.RemoveTag<TextWrap>();
                label.AddTag<Dirty>();
            }
        }

        UiText.Set(label, text);
    }

    private static void Resize(Entity entity, Vector2 size)
    {
        ref Rectangle rectangle = ref entity.GetComponent<Rectangle>();
        if (rectangle.Bounds.Size == size)
            return;
        rectangle.Bounds = new Rect2(-size / 2, size);
        entity.AddTag<Dirty>();
    }

    /// <summary>Empties the panel's text, so a hidden tooltip does not flash the old one when shown.</summary>
    public static void Clear(Entity tooltip)
    {
        UiText.Set(Label(tooltip), "");
    }
}
