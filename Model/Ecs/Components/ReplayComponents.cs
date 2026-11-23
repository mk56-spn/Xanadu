using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Replays;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Ecs.Components;

/// <summary>Records the player's key presses on a play entity.</summary>
public struct ReplayRecorder : IComponent
{
    public List<ReplayEvent> Events;
    public bool[] Down;

    /// <summary>The song time the entrance was skipped at, or -1.</summary>
    public long SkipFromMs;

    public List<ReplayOffset> Offsets;

    public static ReplayRecorder Create() => new()
    {
        Events = [],
        Down = new bool[KeybindActions.All.Length],
        SkipFromMs = -1,
        Offsets = [],
    };
}

/// <summary>Drives a play entity's keys from a recorded replay.</summary>
public struct ReplayPlayer(Replay replay) : IComponent
{
    public Replay Replay = replay;
    public int Cursor;
    public int OffsetCursor;
    public long LastTimeMs = long.MinValue;
    public bool[] Down = new bool[KeybindActions.All.Length];
    /// <summary>The song time each action last changed, ms: the keyboard reports it so presses judge on the recorded time.</summary>
    public long[] ChangedMs = new long[KeybindActions.All.Length];
}
