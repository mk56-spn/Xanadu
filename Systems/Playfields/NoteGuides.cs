using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Guide lines that lead the eye from note to note in hit order. Notes that
/// share a timestamp are hit together, so they meet at a hub: a small dot at
/// their centre with a spoke to each note, and the guide runs hub to hub.
/// </summary>
public static class NoteGuides
{
    /// <summary>Notes this close in time count as simultaneous.</summary>
    public const long SimultaneousMs = 1;

    /// <summary>Radius of the hub dot, as a fraction of the note radius.</summary>
    public const float HubRadiusScale = 0.18f;

    /// <summary>One visible note: its time, screen centre and fade alpha.</summary>
    public readonly record struct Point(long TimeMs, Vector2 Center, float Alpha);

    /// <summary>A line to draw, already trimmed to stop at the note rims.</summary>
    public readonly record struct Segment(Vector2 From, Vector2 To, float Alpha);

    /// <summary>The meeting dot of a simultaneous group.</summary>
    public readonly record struct Hub(Vector2 Center, float Alpha);

    public sealed class Layout
    {
        public List<Segment> Guides { get; } = new();
        public List<Segment> Spokes { get; } = new();
        public List<Hub> Hubs { get; } = new();
    }

    public static Layout Build(IEnumerable<Point> notes, float noteRadius)
    {
        var sorted = new List<Point>(notes);
        sorted.Sort(static (a, b) => a.TimeMs.CompareTo(b.TimeMs));

        var layout = new Layout();
        float hubRadius = noteRadius * HubRadiusScale;
        Group? previous = null;
        int start = 0;
        while (start < sorted.Count)
        {
            int end = start + 1;
            while (end < sorted.Count && sorted[end].TimeMs - sorted[start].TimeMs <= SimultaneousMs)
                end++;

            Group group = Gather(sorted, start, end, noteRadius, hubRadius, layout);
            if (previous is { } from)
                Connect(from, group, layout);
            previous = group;
            start = end;
        }

        return layout;
    }

    /// <summary>A hit moment: where the guide attaches and how far from it the line stops.</summary>
    private readonly record struct Group(Vector2 Anchor, float Clearance, float Alpha);

    private static Group Gather(
        List<Point> sorted,
        int start,
        int end,
        float noteRadius,
        float hubRadius,
        Layout layout)
    {
        if (end - start == 1)
            return new Group(sorted[start].Center, noteRadius, sorted[start].Alpha);

        Vector2 sum = Vector2.Zero;
        float alpha = 0f;
        for (int i = start; i < end; i++)
        {
            sum += sorted[i].Center;
            alpha = Math.Max(alpha, sorted[i].Alpha);
        }

        Vector2 hub = sum / (end - start);
        layout.Hubs.Add(new Hub(hub, alpha));
        for (int i = start; i < end; i++)
        {
            if (Trim(hub, sorted[i].Center, hubRadius, noteRadius) is { } spoke)
                layout.Spokes.Add(spoke with { Alpha = sorted[i].Alpha });
        }

        return new Group(hub, hubRadius, alpha);
    }

    private static void Connect(Group from, Group to, Layout layout)
    {
        if (Trim(from.Anchor, to.Anchor, from.Clearance, to.Clearance) is { } guide)
            layout.Guides.Add(guide with { Alpha = Math.Min(from.Alpha, to.Alpha) });
    }

    /// <summary>The line between two points minus the clearances at each end, or null when nothing is left.</summary>
    private static Segment? Trim(Vector2 from, Vector2 to, float fromClearance, float toClearance)
    {
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length <= fromClearance + toClearance)
            return null;

        Vector2 direction = delta / length;
        return new Segment(from + direction * fromClearance, to - direction * toClearance, 1f);
    }
}
