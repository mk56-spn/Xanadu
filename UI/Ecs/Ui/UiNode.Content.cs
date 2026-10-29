using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>Creates the leaf content of a UI tree: text, buttons, shapes.</summary>
public readonly partial struct UiNode
{
    /// <summary>Line height the metrics assume, as a share of the font size.</summary>
    public const float TextLineHeight = 1.5f;

    /// <summary>
    /// A text label. Size, colour, alignment and weight are part of the text's
    /// identity, so they are parameters; glow, outline and live updates are
    /// chained. Left is the default: centring is a statement, not a fallback.
    /// The box is measured from the text, so call sites stop carrying guessed
    /// widths; <c>Size</c> pins it when a fixed box matters.
    /// </summary>
    public UiNode Text(
        string value,
        int size = UiType.Body,
        Color? color = null,
        TextAlign align = TextAlign.Left,
        bool bold = false)
    {
        UiBuilder builder = BeginChildren();
        Color ink = color ?? UiTheme.Text;
        Entity created = builder.Text(value, Measure(value, size), size, ink, align);
        if (bold)
            created.AddComponent(new TextStyle(size, ink, bold: true));
        return Leaf(created);
    }

    /// <summary>A button with fill, label and hover/flash state, sized to its label.</summary>
    public UiNode Button(
        string label,
        int size = UiType.Body,
        Color? color = null)
    {
        UiBuilder builder = BeginChildren();
        Entity created = builder.Button(
            label,
            new Vector2(
                UiTextMetrics.Width(label, size) + UiTheme.ContainerPadding * 2,
                UiTheme.ButtonHeight),
            color,
            fontSize: size);
        return Leaf(created);
    }

    /// <summary>
    /// An icon button that falls back to one or two letters when the texture is
    /// missing, so a tool can ship before every icon is drawn.
    /// </summary>
    public UiNode IconButton(
        string fallback,
        string? iconPath,
        float size = UiTheme.ButtonHeight,
        Color? color = null,
        int fontSize = UiType.Label)
    {
        UiBuilder builder = BeginChildren();
        Entity created = builder.IconButton(
            fallback,
            iconPath,
            new Vector2(size, size),
            color,
            fontSize);
        return Leaf(created);
    }

    /// <summary>A plain rectangle, optionally carrying text.</summary>
    public UiNode Rect(Color color, string? text = null, Vector2? size = null)
    {
        UiBuilder builder = BeginChildren();
        Entity created = builder.RectEntity(
            Vector2.Zero,
            size ?? Vector2.Zero,
            color,
            text);
        return Leaf(created);
    }

    /// <summary>An image drawable backed by a texture, sized to it by default.</summary>
    public UiNode Image(Texture2D texture, Vector2? size = null)
    {
        UiBuilder builder = BeginChildren();
        Entity created = builder.ImageEntity(
            Vector2.Zero,
            size ?? texture.GetSize(),
            texture);
        return Leaf(created);
    }

    /// <summary>A polygon or polyline drawn in the node's local space.</summary>
    public UiNode Shape(
        Vector2[] points,
        Color fill,
        Color stroke = default,
        float strokeWidth = 0,
        bool closed = true)
    {
        UiBuilder builder = BeginChildren();
        Entity created = builder.ShapeEntity(
            points,
            fill,
            stroke,
            strokeWidth,
            closed);
        return Leaf(created);
    }

    /// <summary>
    /// A labelled dropdown. The button and its option list are both siblings of
    /// the node this is called on, so an open list is never clipped by a panel.
    /// </summary>
    public UiDropdownNode Dropdown(
        string label,
        IReadOnlyList<string> options,
        int selected = 0,
        Vector2? size = null,
        Color? buttonColor = null,
        Func<int, Color>? optionColor = null)
    {
        UiBuilder builder = BeginChildren();
        UiDropdownControl control = builder.Dropdown(
            label,
            options,
            selected,
            size,
            closeOthers: true,
            buttonColor,
            optionColor);
        return new UiDropdownNode(_surface!, control);
    }

    /// <summary>The box a label of this text and size wants.</summary>
    public static Vector2 Measure(string value, int fontSize) =>
        new(UiTextMetrics.Width(value, fontSize), fontSize * TextLineHeight);
}
