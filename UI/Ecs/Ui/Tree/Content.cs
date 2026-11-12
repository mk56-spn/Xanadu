using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Tree;

/// <summary>
/// Text in <paramref name="type"/>'s size, measured to fit unless sized. With
/// <paramref name="fit"/> it asks for no width and is cut, with an ellipsis, to
/// whatever room layout gives it (<see cref="UiNode.Fit"/>).
/// </summary>
public sealed class Label(
    string text = "",
    int type = UiType.Body,
    Ink ink = Ink.Normal,
    TextAlign? align = null,
    bool fit = false,
    bool bold = false,
    bool mono = false,
    float? width = null,
    float? height = null,
    float grow = 0,
    bool stretch = false,
    string? tip = null)
    : UiSpec(width, height, null, grow, stretch, null, null, tip)
{
    protected override UiNode Create(UiNode parent)
    {
        UiNode node = parent.Text(text, type);
        if (ink == Ink.Dim)
            node.DimText();
        else if (ink == Ink.Accent)
            node.AccentText();
        if (align is { } where)
            node.Align(where);
        if (fit)
            node.Fit();
        if (bold)
            node.Bold();
        if (mono)
            node.Mono();
        return node;
    }
}

/// <summary>A button sized to its label in <paramref name="type"/>'s size; give it a <paramref name="cmd"/> to do something.</summary>
public sealed class Button(
    string label,
    int type = UiType.Label,
    Tone? tone = null,
    UiCommand? cmd = null,
    string? tip = null,
    float? width = null,
    float? height = null,
    float grow = 0,
    bool stretch = false)
    : UiSpec(width, height, null, grow, stretch, tone, cmd, tip)
{
    protected override UiNode Create(UiNode parent) => parent.Button(label, type);
}

/// <summary>A plain rectangle: a swatch, a stripe, a divider. Fill it with a <paramref name="tone"/> or a <paramref name="category"/>'s colour.</summary>
public sealed class Box(
    float? width = null,
    float? height = null,
    Vector2? size = null,
    int? category = null,
    Tone? tone = null,
    float grow = 0,
    bool stretch = false)
    : UiSpec(width, height, size, grow, stretch, tone)
{
    protected override UiNode Create(UiNode parent)
    {
        UiNode node = parent.Rect(default);
        if (category is { } index)
            node.CategoryFill(index);
        return node;
    }
}

/// <summary>A meter showing <paramref name="share"/> (<see cref="UiNode.Meter"/>); size it.</summary>
public sealed class Meter(
    float share,
    Vector2? size = null,
    float? width = null,
    float? height = null,
    float grow = 0,
    string? tip = null)
    : UiSpec(width, height, size, grow, tip: tip)
{
    protected override UiNode Create(UiNode parent) => parent.Meter(share);
}

/// <summary>A text field the host types into while it has focus; read and write its <see cref="TextField"/>.</summary>
public sealed class Field(
    string value = "",
    int type = UiType.Label,
    bool numeric = false,
    Vector2? size = null,
    float? width = null,
    float? height = null,
    float grow = 0,
    string? tip = null)
    : UiSpec(width, height, size, grow, tip: tip)
{
    // The field systems give it its fill, focused or not.
    protected override UiNode Create(UiNode parent) =>
        parent.Rect(default, "")
            .Input()
            .With(new TextStyle(type, parent.Roles.Text))
            .Align(TextAlign.Left)
            .With(new TextField(value, numeric));
}

/// <summary>A line joining two built nodes, in a category's colour (<see cref="UiNode.Connector"/>). Add it before what it joins.</summary>
public sealed class Connector(Entity from, Entity to, int category) : UiSpec
{
    protected override UiNode Create(UiNode parent) => parent.Connector(from, to, category);
}

/// <summary>Anything the specs don't cover, built with the fluent calls: <c>new Custom(p =&gt; p.Dropdown(...))</c>.</summary>
public sealed class Custom(
    Func<UiNode, UiNode> build,
    float? width = null,
    float? height = null,
    float grow = 0,
    bool stretch = false,
    string? tip = null)
    : UiSpec(width, height, null, grow, stretch, tip: tip)
{
    protected override UiNode Create(UiNode parent) => build(parent);
}
