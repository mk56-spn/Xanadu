using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Tree;

/// <summary>Text, measured to fit unless sized; <see cref="Fit"/> cuts it to a box layout gives it.</summary>
public sealed class Label(string text = "", int size = UiType.Body) : UiSpec
{
    public TextRole Role { get; init; }
    public TextAlign? Align { get; init; }

    /// <summary>Cut with an ellipsis to its laid-out width (<see cref="UiNode.Fit"/>).</summary>
    public bool Fit { get; init; }

    public bool Bold { get; init; }
    public bool Mono { get; init; }

    protected override UiNode Create(UiNode parent)
    {
        UiNode node = parent.Text(text, size);
        if (Role == TextRole.Dim)
            node.DimText();
        else if (Role == TextRole.Accent)
            node.AccentText();
        if (Align is { } align)
            node.Align(align);
        if (Fit)
            node.Fit();
        if (Bold)
            node.Bold();
        if (Mono)
            node.Mono();
        return node;
    }
}

/// <summary>A button sized to its label; give it a <see cref="UiSpec.Command"/> to do something.</summary>
public sealed class Button(string label, int size = UiType.Label) : UiSpec
{
    protected override UiNode Create(UiNode parent) => parent.Button(label, size);
}

/// <summary>A plain rectangle: a swatch, a stripe, a divider. Fill it with a <see cref="UiSpec.Tone"/>, a <see cref="Category"/> or <see cref="UiSpec.Style"/>.</summary>
public sealed class Box : UiSpec
{
    /// <summary>Filled with this category's colour (<see cref="UiThemeRoles.Categories"/>).</summary>
    public int? Category { get; init; }

    protected override UiNode Create(UiNode parent)
    {
        UiNode node = parent.Rect(default);
        if (Category is { } category)
            node.CategoryFill(category);
        return node;
    }
}

/// <summary>A meter showing <paramref name="share"/> (<see cref="UiNode.Meter"/>); size it.</summary>
public sealed class Meter(float share) : UiSpec
{
    protected override UiNode Create(UiNode parent) => parent.Meter(share);
}

/// <summary>A text field the host types into while it has focus; read and write its <see cref="TextField"/>.</summary>
public sealed class Field(string value = "", int size = UiType.Label) : UiSpec
{
    public bool Numeric { get; init; }

    // The field systems give it its fill, focused or not.
    protected override UiNode Create(UiNode parent) =>
        parent.Rect(default, "")
            .Input()
            .With(new TextStyle(size, parent.Roles.Text))
            .Align(TextAlign.Left)
            .With(new TextField(value, Numeric));
}

/// <summary>A line joining two built nodes, in a category's colour (<see cref="UiNode.Connector"/>). Add it before what it joins.</summary>
public sealed class Connector(Entity from, Entity to, int category) : UiSpec
{
    protected override UiNode Create(UiNode parent) => parent.Connector(from, to, category);
}

/// <summary>Anything the specs don't cover, built with the fluent calls: <c>new Custom(p =&gt; p.Dropdown(...))</c>.</summary>
public sealed class Custom(Func<UiNode, UiNode> build) : UiSpec
{
    protected override UiNode Create(UiNode parent) => build(parent);
}
