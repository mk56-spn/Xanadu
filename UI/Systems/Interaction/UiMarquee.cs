using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>How a press on empty space ended.</summary>
public enum MarqueeEnd
{
    /// <summary>No marquee press was in progress.</summary>
    None,

    /// <summary>Released without dragging far enough to make a box: a plain click.</summary>
    Click,

    /// <summary>Released after dragging a box.</summary>
    Box,
}

/// <summary>
/// Rubber-band selection logic shared by every view that box-selects, whatever
/// it selects (<typeparamref name="T"/>): the drag from the press to the
/// pointer, the dead zone before a press becomes a box, and whether the box
/// adds to what was selected or replaces it. The view finds what the box
/// covers; <see cref="Selection"/> turns that into the new selection.
/// </summary>
public sealed class UiMarquee<T>(float threshold = 4f)
{
    private readonly List<T> _basis = new();

    /// <summary>Pressed on empty space; dragging past the threshold turns it into a box.</summary>
    public bool Armed { get; private set; }

    /// <summary>A box is being dragged.</summary>
    public bool Active { get; private set; }

    /// <summary>The box adds to the selection that existed when it began.</summary>
    public bool Additive { get; private set; }

    /// <summary>Where the press happened. Kept after <see cref="End"/> so a click can act there.</summary>
    public Vector2 Start { get; private set; }

    public Vector2 Current { get; private set; }

    /// <summary>The box between the press and the pointer, whichever way it was dragged.</summary>
    public Rect2 Bounds => UiMarqueeBox.Between(Start, Current);

    /// <summary>A press on empty space that may become a box; a click if it never does.</summary>
    public void Arm(Vector2 start)
    {
        Cancel();
        Start = start;
        Current = start;
        Armed = true;
    }

    /// <summary>
    /// Starts a box at once. Additive boxes keep <paramref name="existing"/>
    /// selected alongside whatever they cover.
    /// </summary>
    public void Begin(Vector2 start, bool additive, IEnumerable<T> existing)
    {
        Cancel();
        Start = start;
        Current = start;
        Active = true;
        Additive = additive;
        if (additive)
            _basis.AddRange(existing);
    }

    /// <summary>
    /// Follows the pointer. True while a box is being dragged;
    /// <paramref name="started"/> is true on the call that turned an armed
    /// press into one, which is when a plain (non-additive) box clears the
    /// old selection.
    /// </summary>
    public bool Update(Vector2 pointer, out bool started)
    {
        started = false;
        if (!Armed && !Active)
            return false;

        Current = pointer;
        if (Armed && Start.DistanceTo(pointer) > threshold)
        {
            Armed = false;
            Active = true;
            Additive = false;
            started = true;
        }

        return Active;
    }

    /// <summary>Finishes the press. <see cref="Start"/> stays readable for a click.</summary>
    public MarqueeEnd End()
    {
        MarqueeEnd end = Active ? MarqueeEnd.Box : Armed ? MarqueeEnd.Click : MarqueeEnd.None;
        Armed = false;
        Active = false;
        Additive = false;
        _basis.Clear();
        return end;
    }

    /// <summary>Drops any press or box without it counting as a click.</summary>
    public void Cancel() => End();

    /// <summary>The selection a box covering <paramref name="hits"/> makes: the hits, after the existing ones when additive.</summary>
    public List<T> Selection(IEnumerable<T> hits)
    {
        var selection = new List<T>(Additive ? _basis : Array.Empty<T>());
        foreach (T hit in hits)
        {
            if (!selection.Contains(hit))
                selection.Add(hit);
        }

        return selection;
    }

    /// <summary>Copies the box into the component a draw system reads.</summary>
    public void Publish(ref UiMarqueeBox box)
    {
        box.Start = Start;
        box.Current = Current;
        box.Active = Active;
    }
}
