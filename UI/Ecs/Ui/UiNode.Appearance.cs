using System;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// Chains the appearance and behaviour of a node: fill, border, text style,
/// tooltips, and the raw component escape hatch. Each call edits the entity's
/// components and returns the node.
/// </summary>
public readonly partial struct UiNode
{
    // ----------------------------------------------------------------- look

    /// <summary>Sets the fill colour. Text colour is <see cref="TextColor"/>.</summary>
    public UiNode Fill(Color color)
    {
        if (IsNull)
            return this;

        if (_entity.TryGetComponent(out FillColor fill))
        {
            if (fill.Value == color)
                return this;

            fill.Value = color;
            _entity.AddComponent(fill);
        }
        else
        {
            _entity.AddComponent(new FillColor(color));
        }

        return Touch(layout: false);
    }

    /// <summary>Draws an outline around the node's fill.</summary>
    public UiNode Border(Color color, float width = 1)
    {
        if (IsNull)
            return this;

        _entity.AddComponent(new UiBorder(color, width));
        return Touch(layout: false);
    }

    /// <summary>Cuts the corners of the node's fill by <paramref name="size"/>.</summary>
    public UiNode Chamfer(float size)
    {
        if (IsNull)
            return this;

        _entity.AddComponent(new UiChamfer(size));
        return Touch(layout: false);
    }

    /// <summary>Draws above the node's siblings.</summary>
    public UiNode Raised()
    {
        if (IsNull)
            return this;

        _entity.AddTag<UiRaised>();
        return this;
    }

    /// <summary>Draws below the node's siblings, for full-screen backdrops.</summary>
    public UiNode Below() => With(new UiBackground());

    /// <summary>Marks the node as part of a floating overlay.</summary>
    public UiNode Overlay()
    {
        if (IsNull)
            return this;

        _entity.AddTag<UiOverlay>();
        return this;
    }

    // ----------------------------------------------------------------- text

    /// <summary>Font size of the node's text.</summary>
    public UiNode Font(int fontSize)
    {
        if (IsNull)
            return this;

        TextStyle style = _entity.TryGetComponent(out TextStyle current)
            ? current
            : new TextStyle(UiTheme.FontSize, UiTheme.Text);
        style.FontSize = fontSize;
        _entity.AddComponent(style);
        return Touch(layout: false);
    }

    /// <summary>Colour of the node's text.</summary>
    public UiNode TextColor(Color color)
    {
        if (IsNull)
            return this;

        TextStyle style = _entity.TryGetComponent(out TextStyle current)
            ? current
            : new TextStyle(UiTheme.FontSize, UiTheme.Text);
        style.Color = color;
        _entity.AddComponent(style);
        return Touch(layout: false);
    }

    /// <summary>Weight of the node's text.</summary>
    public UiNode Bold(bool bold = true)
    {
        if (IsNull)
            return this;

        TextStyle style = _entity.TryGetComponent(out TextStyle current)
            ? current
            : new TextStyle(UiTheme.FontSize, UiTheme.Text);
        style.Bold = bold;
        _entity.AddComponent(style);
        return Touch(layout: false);
    }

    /// <summary>Horizontal alignment of the node's text.</summary>
    public UiNode Align(TextAlign align) => With(new TextAlignment(align));

    /// <summary>A soft glow behind the node's text.</summary>
    public UiNode Glow(Color color, float radius = 12f, float intensity = 1.5f) =>
        With(new TextGlow(color, radius, intensity));

    /// <summary>An outline under the node's text fill.</summary>
    public UiNode Outline(Color color, int size = 4) =>
        With(new TextOutline(color, size));

    // ------------------------------------------------------------ behaviour

    /// <summary>Hover text, shown by the tooltip system.</summary>
    public UiNode Tooltip(string text) => With(new UiTooltip(text));

    /// <summary>Whether the node is hit-tested by pointer input.</summary>
    public UiNode Input(bool takesInput = true)
    {
        if (IsNull)
            return this;

        if (takesInput)
            _entity.AddTag<TakesInput>();
        else
            _entity.RemoveTag<TakesInput>();

        return this;
    }

    /// <summary>Greys the node out and drops any interaction state it held.</summary>
    public UiNode Disabled(bool disabled = true)
    {
        if (IsNull)
            return this;

        UiLayout.SetDisabled(_entity, disabled);
        return this;
    }

    /// <summary>Replaces the node's text without redrawing anything unchanged.</summary>
    public UiNode SetText(string value)
    {
        if (IsNull)
            return this;

        UiText.Set(_entity, value);
        return this;
    }

    // ------------------------------------------------------- raw components

    /// <summary>
    /// Attaches any component, the escape hatch for tool-specific state like a
    /// scene command or an editor button kind.
    /// </summary>
    public UiNode With<T>(T component) where T : struct, IComponent
    {
        if (IsNull)
            return this;

        _entity.AddComponent(component);
        return Touch(layout: false);
    }

    /// <summary>Adds a tag.</summary>
    public UiNode Tag<T>() where T : struct, ITag
    {
        if (IsNull)
            return this;

        _entity.AddTag<T>();
        return this;
    }

    /// <summary>Removes a component.</summary>
    public UiNode Without<T>() where T : struct, IComponent
    {
        if (IsNull)
            return this;

        _entity.RemoveComponent<T>();
        return Touch(layout: false);
    }

    /// <summary>Removes a tag.</summary>
    public UiNode Untag<T>() where T : struct, ITag
    {
        if (IsNull)
            return this;

        _entity.RemoveTag<T>();
        return this;
    }
}
