using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Timeline;

/// <summary>What the keys shared by every timeline can do.</summary>
public enum TimelineCommand
{
    TogglePlay,
    StepBack,
    StepForward,
    SeekBack,
    SeekForward,
    PreviousKey,
    NextKey,
    Start,
    End,
}

/// <summary>What a tool gives the shared timeline behaviour to work on. Times are in milliseconds.</summary>
public interface ITimelineHost
{
    double NowMs { get; }
    double DurationMs { get; }

    /// <summary>One snap step, and where the step grid starts.</summary>
    double StepMs { get; }
    double StepOriginMs { get; }

    /// <summary>Something modal is up, so the timeline leaves the wheel alone.</summary>
    bool Blocked { get; }

    /// <summary>The whole timeline and its ruler on screen; false while it is hidden.</summary>
    bool TryGetArea(out Rect2 panel, out Rect2 ruler);

    /// <summary>The times of everything on the timeline, in any order.</summary>
    IEnumerable<double> MarkerTimesMs();

    void SeekMs(double ms);

    void TogglePlay();

    /// <summary>A jump found nothing that way: below zero is earlier, above is later.</summary>
    void NoMarker(int direction);
}

/// <summary>Marks an entity's <see cref="Xanadu.Ecs.TimelineView"/> as a timeline with this host.</summary>
public struct TimelineHost(ITimelineHost host) : IComponent
{
    public ITimelineHost Host = host;
}

/// <summary>The keys every timeline answers to the same way; a tool's own table holds the rest.</summary>
public static class TimelineShortcuts
{
    /// <summary>How far Shift and an arrow seek.</summary>
    public const double SeekMs = 1_000;

    public static readonly IReadOnlyList<(KeyChord Chord, TimelineCommand Command)> All = new[]
    {
        (new KeyChord(Key.Space), TimelineCommand.TogglePlay),
        (new KeyChord(Key.Left, Ctrl: true), TimelineCommand.StepBack),
        (new KeyChord(Key.Right, Ctrl: true), TimelineCommand.StepForward),
        (new KeyChord(Key.Left, Shift: true), TimelineCommand.SeekBack),
        (new KeyChord(Key.Right, Shift: true), TimelineCommand.SeekForward),
        (new KeyChord(Key.Up), TimelineCommand.PreviousKey),
        (new KeyChord(Key.Down), TimelineCommand.NextKey),
        (new KeyChord(Key.Home), TimelineCommand.Start),
        (new KeyChord(Key.End), TimelineCommand.End),
    };

    public static TimelineCommand? Find(KeyChord chord)
    {
        foreach ((KeyChord bound, TimelineCommand command) in All)
        {
            if (bound == chord)
                return command;
        }

        return null;
    }

    public static void Run(ITimelineHost host, TimelineCommand command)
    {
        switch (command)
        {
            case TimelineCommand.TogglePlay:
                host.TogglePlay();
                break;
            case TimelineCommand.StepBack:
                Step(host, -1);
                break;
            case TimelineCommand.StepForward:
                Step(host, 1);
                break;
            case TimelineCommand.SeekBack:
                SeekTo(host, host.NowMs - SeekMs);
                break;
            case TimelineCommand.SeekForward:
                SeekTo(host, host.NowMs + SeekMs);
                break;
            case TimelineCommand.PreviousKey:
                Jump(host, -1);
                break;
            case TimelineCommand.NextKey:
                Jump(host, 1);
                break;
            case TimelineCommand.Start:
                SeekTo(host, 0);
                break;
            case TimelineCommand.End:
                SeekTo(host, host.DurationMs);
                break;
        }
    }

    private static void SeekTo(ITimelineHost host, double ms) =>
        host.SeekMs(Math.Clamp(ms, 0, Math.Max(0, host.DurationMs)));

    /// <summary>Moves one snap step, landing on the step grid.</summary>
    private static void Step(ITimelineHost host, int direction)
    {
        double index = Math.Round((host.NowMs - host.StepOriginMs) / host.StepMs) + direction;
        SeekTo(host, host.StepOriginMs + index * host.StepMs);
    }

    /// <summary>Goes to the nearest marker before or after the playhead.</summary>
    private static void Jump(ITimelineHost host, int direction)
    {
        double? best = null;
        foreach (double time in host.MarkerTimesMs())
        {
            double offset = (time - host.NowMs) * direction;
            if (offset > MarkerToleranceMs && (best is null || offset < (best.Value - host.NowMs) * direction))
                best = time;
        }

        if (best is { } target)
            SeekTo(host, target);
        else
            host.NoMarker(direction);
    }

    /// <summary>A marker this close counts as under the playhead, so a jump moves past it.</summary>
    private const double MarkerToleranceMs = 0.5;
}
