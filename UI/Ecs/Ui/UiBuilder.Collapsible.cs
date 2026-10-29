using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

public sealed partial class UiBuilder
{
    /// <summary>
    /// Opens a collapsible section in the current parent: a header button that
    /// folds the body, then the body, which is left current so the caller fills
    /// it. Close it with <see cref="EndCollapsible"/>. The section and its body
    /// size to their content, so put it in a vertical stack.
    /// </summary>
    public UiBuilder Collapsible(
        string title,
        float width,
        bool expanded = true,
        float spacing = UiTheme.ContainerSpacing,
        float headerHeight = UiTheme.TreeRowHeight,
        Color? titleColor = null)
    {
        var clear = new Color(0, 0, 0, 0);
        Container(Vector2.Zero, new Vector2(width, 4), StackDirection.Vertical,
            spacing: 2, color: clear, cross: StackCrossAlignment.Start,
            autoHeight: true, main: StackMainAlignment.Start);
        Entity section = _current;

        Entity header = Button(UiCollapsibleControl.Header(title, expanded),
            new Vector2(width, headerHeight), UiTheme.PanelAlt);
        header.AddComponent(new TextAlignment(TextAlign.Left));
        header.AddComponent(new TextStyle(UiTheme.FontSize, titleColor ?? UiTheme.Accent));

        Container(Vector2.Zero, new Vector2(width, 4), StackDirection.Vertical,
            spacing: spacing, color: clear, cross: StackCrossAlignment.Start,
            autoHeight: true, main: StackMainAlignment.Start);
        Entity body = _current;
        if (!expanded)
        {
            body.GetComponent<Position>().Value = UiLayout.Hidden;
            body.AddTag<LayoutIgnore>();
        }

        section.AddComponent(new UiCollapsible
        {
            Header = header,
            Body = body,
            Title = title,
            Expanded = expanded,
        });
        return this;
    }

    /// <summary>Closes the body and the section opened by <see cref="Collapsible"/>.</summary>
    public UiBuilder EndCollapsible()
    {
        End();
        return End();
    }
}

/// <summary>Reads and drives a collapsible section built by <see cref="UiBuilder.Collapsible"/>.</summary>
public static class UiCollapsibleControl
{
    public const string ExpandedGlyph = "▾";
    public const string CollapsedGlyph = "▸";

    /// <summary>The header label: the arrow for the state, then the title.</summary>
    public static string Header(string title, bool expanded) =>
        $"{(expanded ? ExpandedGlyph : CollapsedGlyph)}  {title}";

    public static bool IsExpanded(Entity section) =>
        !section.IsNull && section.TryGetComponent(out UiCollapsible state) && state.Expanded;

    /// <summary>Folds or unfolds the section; the system applies it next frame.</summary>
    public static void SetExpanded(Entity section, bool expanded)
    {
        if (!section.IsNull && section.HasComponent<UiCollapsible>())
            section.GetComponent<UiCollapsible>().Expanded = expanded;
    }

    /// <summary>Retitles the section, for a header that carries a count or a name.</summary>
    public static void SetTitle(Entity section, string title)
    {
        if (!section.IsNull && section.HasComponent<UiCollapsible>())
            section.GetComponent<UiCollapsible>().Title = title;
    }
}
