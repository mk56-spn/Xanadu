using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Systems.Timeline;

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

    /// <summary>Runs <paramref name="command"/> on <paramref name="timeline"/>, from the frame its owner last wrote.</summary>
    public static void Run(Entity timeline, TimelineCommand command)
    {
        if (timeline.IsNull || !timeline.HasComponent<TimelineFrame>())
            return;

        TimelineFrame frame = timeline.GetComponent<TimelineFrame>();
        switch (command)
        {
            case TimelineCommand.TogglePlay:
                timeline.EmitSignal(new TimelineTogglePlay());
                break;
            case TimelineCommand.StepBack:
                Step(timeline, frame, -1);
                break;
            case TimelineCommand.StepForward:
                Step(timeline, frame, 1);
                break;
            case TimelineCommand.SeekBack:
                SeekTo(timeline, frame, frame.NowMs - SeekMs);
                break;
            case TimelineCommand.SeekForward:
                SeekTo(timeline, frame, frame.NowMs + SeekMs);
                break;
            case TimelineCommand.PreviousKey:
                Jump(timeline, frame, -1);
                break;
            case TimelineCommand.NextKey:
                Jump(timeline, frame, 1);
                break;
            case TimelineCommand.Start:
                SeekTo(timeline, frame, 0);
                break;
            case TimelineCommand.End:
                SeekTo(timeline, frame, frame.DurationMs);
                break;
        }
    }

    private static void SeekTo(Entity timeline, in TimelineFrame frame, double ms) =>
        timeline.EmitSignal(new TimelineSeek(Math.Clamp(ms, 0, Math.Max(0, frame.DurationMs))));

    /// <summary>Moves one snap step, landing on the step grid.</summary>
    private static void Step(Entity timeline, in TimelineFrame frame, int direction)
    {
        double index = Math.Round((frame.NowMs - frame.StepOriginMs) / frame.StepMs) + direction;
        SeekTo(timeline, frame, frame.StepOriginMs + index * frame.StepMs);
    }

    /// <summary>Goes to the nearest marker before or after the playhead.</summary>
    private static void Jump(Entity timeline, in TimelineFrame frame, int direction)
    {
        double? best = null;
        foreach (double time in frame.Markers)
        {
            double offset = (time - frame.NowMs) * direction;
            if (offset > MarkerToleranceMs && (best is null || offset < (best.Value - frame.NowMs) * direction))
                best = time;
        }

        if (best is { } target)
            SeekTo(timeline, frame, target);
        else
            timeline.EmitSignal(new TimelineNoMarker(direction));
    }

    /// <summary>A marker this close counts as under the playhead, so a jump moves past it.</summary>
    private const double MarkerToleranceMs = 0.5;
}
