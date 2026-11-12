using System;
using System.Collections;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Tree;

/// <summary>
/// One node of a UI tree written as C# constructors and collection initializers,
/// so the code has the shape of the screen (as osu!lazer's drawables do):
/// <code>
/// body.Add(new Column(Fill, gap: Space.Md)
/// {
///     new Label("ATLAS", UiType.Heading, ink: Ink.Accent),
///     (_back = new Button("BACK", tone: Tone.Secondary, tip: "back to the last type")),
/// });
/// </code>
/// What a node is and how it is laid out go in its constructor, as named
/// arguments; the few rare settings (<see cref="Free"/>, <see cref="Hidden"/>,
/// <see cref="MinWidth"/>, <see cref="MaxWidth"/>, <see cref="Style"/>) are
/// initializer properties. A spec only describes:
/// <see cref="UiTreeBuild.Add{T}(UiNode, T)"/> builds it into ordinary ECS UI
/// through <see cref="UiNode"/>, and afterwards <see cref="Node"/> is the node it
/// made. Keep a spec in a field (as <c>_back</c> above) to reach its node later.
/// </summary>
public abstract class UiSpec(
    float? width = null,
    float? height = null,
    Vector2? size = null,
    float grow = 0,
    bool stretch = false,
    Tone? tone = null,
    UiCommand? cmd = null,
    string? tip = null)
{
    /// <summary>The node this spec built; null until it is added.</summary>
    public UiNode Node { get; private set; }

    public float? MinWidth { get; init; }
    public float? MaxWidth { get; init; }

    /// <summary>Out of its parent's flow (<see cref="UiNode.IgnoreLayout"/>).</summary>
    public bool Free { get; init; }

    /// <summary>Built hidden (<see cref="UiNode.Hidden"/>).</summary>
    public bool Hidden { get; init; }

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

    /// <summary>After the shared settings: a stack builds its children here.</summary>
    protected virtual void Built(UiNode node)
    {
    }

    private void Apply(UiNode node)
    {
        if (cmd is { } command)
        {
            node.Input().With(command);
            if (!node.Entity.HasComponent<ButtonVisual>())
                node.With(ButtonVisual.Create(default));
        }

        switch (tone)
        {
            case Tree.Tone.Primary: node.Primary(); break;
            case Tree.Tone.Secondary: node.Secondary(); break;
            case Tree.Tone.Quiet: node.Quiet(); break;
            case Tree.Tone.Danger: node.Danger(); break;
            case Tree.Tone.Solid: node.Solid(); break;
            case Tree.Tone.Backing: node.Backing(); break;
        }

        if (size is { } fixedSize)
            node.Size(fixedSize);
        if (width is { } fixedWidth)
            node.Width(fixedWidth);
        if (height is { } fixedHeight)
            node.Height(fixedHeight);
        if (grow > 0)
            node.Grow(grow);
        if (stretch)
            node.Stretch();
        if (MinWidth is { } min)
            node.MinWidth(min);
        if (MaxWidth is { } max)
            node.MaxWidth(max);
        if (Free)
            node.IgnoreLayout();
        if (tip is not null)
            node.Input().Tooltip(tip);
        if (Hidden)
            node.Hidden();
        Style?.Invoke(node);
    }

    /// <summary>The built node, wherever a node is wanted.</summary>
    public static implicit operator UiNode(UiSpec spec) => spec.Node;

    /// <summary>The built entity, wherever an entity is wanted.</summary>
    public static implicit operator Entity(UiSpec spec) => spec.Node.Entity;
}

/// <summary>
/// A shape a screen repeats, given once and passed to a stack's constructor:
/// <c>static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch);</c>.
/// The stack's own arguments win over it.
/// </summary>
public readonly record struct Flex(float Grow = 0, StackCrossAlignment? Cross = null, bool Stretch = false);

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
public enum Ink
{
    Normal,
    Dim,
    Accent,
}

/// <summary>Short names for how a stack lines its children up across it: <c>cross: Cross.Stretch</c>.</summary>
public static class Cross
{
    public const StackCrossAlignment Start = StackCrossAlignment.Start;
    public const StackCrossAlignment Center = StackCrossAlignment.Center;
    public const StackCrossAlignment End = StackCrossAlignment.End;
    public const StackCrossAlignment Stretch = StackCrossAlignment.Stretch;
}

/// <summary>Short names for where a stack puts its children along it: <c>main: Main.Center</c>.</summary>
public static class Main
{
    public const StackMainAlignment Start = StackMainAlignment.Start;
    public const StackMainAlignment Center = StackMainAlignment.Center;
    public const StackMainAlignment End = StackMainAlignment.End;
}

/// <summary>Short names for how text sits in its box: <c>align: Align.Right</c>.</summary>
public static class Align
{
    public const TextAlign Left = TextAlign.Left;
    public const TextAlign Center = TextAlign.Center;
    public const TextAlign Right = TextAlign.Right;
}

/// <summary>Helpers a tree reads better with; <c>using static Xanadu.Ui.Tree.UiSpecs;</c>.</summary>
public static class UiSpecs
{
    /// <summary>A button's command: <c>cmd: Cmd(AtlasCommand.View, 1)</c>.</summary>
    public static UiCommand Cmd<T>(T id, int arg = 0)
        where T : struct, Enum =>
        UiCommand.Of(id, arg);
}

/// <summary>A stack's children, kept in order; see <see cref="StackSpec"/>.</summary>
internal sealed class UiChildren : IEnumerable<UiSpec>
{
    private readonly List<UiSpec> _specs = new();

    public void Add(UiSpec? spec)
    {
        if (spec is not null)
            _specs.Add(spec);
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
