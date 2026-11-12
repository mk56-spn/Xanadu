using System;
using System.Collections;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Tree;

/// <summary>
/// A stack: its settings in the constructor, its children in the braces after it,
/// <c>new Row(gap: Space.Sm) { a, b, c }</c>. A sequence (a <c>Select</c>, a
/// helper's results) goes in among them, and a <c>null</c> (a child left out by a
/// condition) is skipped.
/// </summary>
/// <remarks>
/// A stack's braces hold its children, so the settings other specs take as
/// initializer properties (<c>free</c>, <c>hidden</c>, <c>style</c>) are constructor
/// arguments here. It is a collection only for the braces: it implements the non-generic
/// <see cref="IEnumerable"/>, so a stack added to another stack is one child, never
/// mistaken for a sequence of them.
/// </remarks>
public abstract class StackSpec(
    Flex layout,
    float gap,
    float pad,
    StackMainAlignment? main,
    StackCrossAlignment? cross,
    bool clip,
    float? width,
    float? height,
    Vector2? size,
    float grow,
    bool stretch,
    Tone? tone,
    UiCommand? cmd,
    string? tip,
    bool free,
    bool hidden,
    Action<UiNode>? style)
    : UiSpec(width, height, size, grow > 0 ? grow : layout.Grow, stretch || layout.Stretch, tone, cmd, tip), IEnumerable
{
    private readonly UiChildren _children = new();
    private readonly StackCrossAlignment? _cross = cross ?? layout.Cross;

    public void Add(UiSpec? child) => _children.Add(child);

    public void Add(IEnumerable<UiSpec?> children)
    {
        foreach (UiSpec? child in children)
            _children.Add(child);
    }

    IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();

    protected override void Built(UiNode node)
    {
        if (gap > 0)
            node.Gap(gap);
        if (pad > 0)
            node.Pad(pad);
        if (main is { } along)
            node.Main(along);
        if (_cross is { } across)
            node.Cross(across);
        if (clip)
            node.Clip();
        if (free)
            node.IgnoreLayout();
        if (hidden)
            node.Hidden();
        style?.Invoke(node);

        foreach (UiSpec child in _children)
            node.Add(child);
    }
}

/// <summary>Children stacked top to bottom, sized to them unless told otherwise.</summary>
public sealed class Column(
    Flex layout = default,
    float gap = 0,
    float pad = 0,
    StackMainAlignment? main = null,
    StackCrossAlignment? cross = null,
    bool clip = false,
    float? width = null,
    float? height = null,
    Vector2? size = null,
    float grow = 0,
    bool stretch = false,
    Tone? tone = null,
    UiCommand? cmd = null,
    string? tip = null,
    bool free = false,
    bool hidden = false,
    Action<UiNode>? style = null)
    : StackSpec(layout, gap, pad, main, cross, clip, width, height, size, grow, stretch, tone, cmd, tip, free, hidden, style)
{
    protected override UiNode Create(UiNode parent) => parent.Column();
}

/// <summary>Children side by side, sized to them unless told otherwise.</summary>
public sealed class Row(
    Flex layout = default,
    float gap = 0,
    float pad = 0,
    StackMainAlignment? main = null,
    StackCrossAlignment? cross = null,
    bool clip = false,
    float? width = null,
    float? height = null,
    Vector2? size = null,
    float grow = 0,
    bool stretch = false,
    Tone? tone = null,
    UiCommand? cmd = null,
    string? tip = null,
    bool free = false,
    bool hidden = false,
    Action<UiNode>? style = null)
    : StackSpec(layout, gap, pad, main, cross, clip, width, height, size, grow, stretch, tone, cmd, tip, free, hidden, style)
{
    protected override UiNode Create(UiNode parent) => parent.Row();
}

/// <summary>A themed panel: the panel fill, padding and spacing.</summary>
public sealed class Panel(
    Flex layout = default,
    StackCrossAlignment? cross = null,
    float? width = null,
    float? height = null,
    float grow = 0,
    bool stretch = false,
    string? tip = null)
    : StackSpec(layout, 0, 0, null, cross, false, width, height, null, grow, stretch, null, null, tip, false, false, null)
{
    protected override UiNode Create(UiNode parent) => parent.Panel();
}

/// <summary>Takes the room left in its stack, by <paramref name="grow"/>, pushing what follows to the far end.</summary>
public sealed class Spacer(float grow = 1) : UiSpec(grow: grow)
{
    protected override UiNode Create(UiNode parent) => parent.Column();
}
