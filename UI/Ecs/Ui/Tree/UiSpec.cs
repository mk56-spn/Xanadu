using System;
using System.Collections;
using System.Collections.Generic;
using Xanadu.Ecs;

namespace Xanadu.Ui.Tree;

/// <summary>
/// One node of a UI tree written as a C# object initializer, so the code has the
/// shape of the screen (as osu!lazer's drawables do):
/// <code>
/// body.Add(new Column
/// {
///     Grow = 1,
///     Gap = UiMetrics.Space.Md,
///     Children =
///     {
///         new Label("ATLAS", UiType.Heading) { Role = TextRole.Accent },
///         _back = new Button("BACK") { Tone = Tone.Secondary, Tooltip = "back to the last type" },
///     },
/// });
/// </code>
/// A spec only describes; <see cref="UiTreeBuild.Add(UiNode, UiSpec)"/> builds it into
/// ordinary ECS UI through <see cref="UiNode"/>, and afterwards <see cref="Node"/>
/// is the node it made. Keep a spec in a field (as <c>_back</c> above) to reach its
/// node later. Specs are thrown away with their fields; the entities are the UI.
/// </summary>
public abstract class UiSpec
{
    /// <summary>The node this spec built; null until it is added.</summary>
    public UiNode Node { get; private set; }

    /// <summary>A fixed width; the height stays as it was.</summary>
    public float? Width { get; init; }

    /// <summary>A fixed height; the width stays as it was.</summary>
    public float? Height { get; init; }

    /// <summary>A fixed size, both ways.</summary>
    public Vector2? Size { get; init; }

    /// <summary>A share of the room its stack has left, by weight (<see cref="UiNode.Grow"/>).</summary>
    public float Grow { get; init; }

    /// <summary>Fills its stack across (<see cref="UiNode.Stretch"/>).</summary>
    public bool Stretch { get; init; }

    public float? MinWidth { get; init; }
    public float? MaxWidth { get; init; }

    /// <summary>Out of its parent's flow (<see cref="UiNode.IgnoreLayout"/>).</summary>
    public bool Free { get; init; }

    /// <summary>Built hidden (<see cref="UiNode.Hidden"/>).</summary>
    public bool Hidden { get; init; }

    /// <summary>The theme role it fills with, if any (<see cref="Tree.Tone"/>).</summary>
    public Tone? Tone { get; init; }

    /// <summary>What pressing it does; it takes input and lights up on hover.</summary>
    public UiCommand? Command { get; init; }

    public string? Tooltip { get; init; }

    /// <summary>Anything else, chained on the built node: <c>Style = n =&gt; n.Chamfer(6)</c>.</summary>
    public Action<UiNode>? Style { get; init; }

    /// <summary>Builds the spec under <paramref name="parent"/> and returns its node.</summary>
    internal UiNode Build(UiNode parent)
    {
        UiNode node = Create(parent);
        Node = node;
        Apply(node);
        Built(node);
        return node;
    }

    /// <summary>Makes the node under <paramref name="parent"/>, with what identifies it.</summary>
    protected abstract UiNode Create(UiNode parent);

    /// <summary>After the shared settings: a container builds its children here.</summary>
    protected virtual void Built(UiNode node)
    {
    }

    private void Apply(UiNode node)
    {
        if (Command is { } command)
        {
            node.Input().With(command);
            if (!node.Entity.HasComponent<ButtonVisual>())
                node.With(ButtonVisual.Create(default));
        }

        switch (Tone)
        {
            case Tree.Tone.Primary: node.Primary(); break;
            case Tree.Tone.Secondary: node.Secondary(); break;
            case Tree.Tone.Quiet: node.Quiet(); break;
            case Tree.Tone.Danger: node.Danger(); break;
            case Tree.Tone.Solid: node.Solid(); break;
            case Tree.Tone.Backing: node.Backing(); break;
        }

        if (Size is { } size)
            node.Size(size);
        if (Width is { } width)
            node.Width(width);
        if (Height is { } height)
            node.Height(height);
        if (Grow > 0)
            node.Grow(Grow);
        if (Stretch)
            node.Stretch();
        if (MinWidth is { } min)
            node.MinWidth(min);
        if (MaxWidth is { } max)
            node.MaxWidth(max);
        if (Free)
            node.IgnoreLayout();
        if (Tooltip is not null)
            node.Input().Tooltip(Tooltip);
        if (Hidden)
            node.Hidden();
        Style?.Invoke(node);
    }

    /// <summary>The built node, wherever a node is wanted.</summary>
    public static implicit operator UiNode(UiSpec spec) => spec.Node;

    /// <summary>The built entity, wherever an entity is wanted.</summary>
    public static implicit operator Friflo.Engine.ECS.Entity(UiSpec spec) => spec.Node.Entity;
}

/// <summary>A theme role a node fills with; see <see cref="UiThemeRoles"/>.</summary>
public enum Tone
{
    Primary,
    Secondary,
    Quiet,
    Danger,

    /// <summary>The opaque panel surface.</summary>
    Solid,

    /// <summary>The see-through backing behind controls over a busy view.</summary>
    Backing,
}

/// <summary>A theme role for text.</summary>
public enum TextRole
{
    Normal,
    Dim,
    Accent,
}

/// <summary>
/// A container's children, in order. Being a collection lets an initializer list
/// them as <c>Children = { a, b, c }</c>, take a sequence (a <c>Select</c> or a
/// loop's results) in among them, and skip a <c>null</c> (a child left out by a condition).
/// </summary>
public sealed class UiChildren : IEnumerable<UiSpec>
{
    private readonly List<UiSpec> _specs = new();

    public void Add(UiSpec? spec)
    {
        if (spec is not null)
            _specs.Add(spec);
    }

    public void Add(IEnumerable<UiSpec?> specs)
    {
        foreach (UiSpec? spec in specs)
            Add(spec);
    }

    public IEnumerator<UiSpec> GetEnumerator() => _specs.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Building specs into a node.</summary>
public static class UiTreeBuild
{
    /// <summary>Builds <paramref name="spec"/> (and everything under it) as a child of <paramref name="parent"/>; returns the spec.</summary>
    public static T Add<T>(this UiNode parent, T spec)
        where T : UiSpec
    {
        spec.Build(parent);
        return spec;
    }

    /// <summary>Builds each spec, in order, as a child of <paramref name="parent"/>.</summary>
    public static void Add(this UiNode parent, params UiSpec?[] specs)
    {
        foreach (UiSpec? spec in specs)
            spec?.Build(parent);
    }
}
