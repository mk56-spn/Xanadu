using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Replays;
using Xanadu.Settings;

namespace Xanadu.Ecs;

/// <summary>Records the player's key presses on a play entity.</summary>
public struct ReplayRecorder : IComponent
{
    public List<ReplayEvent> Events;
    public bool[] Down;

    public static ReplayRecorder Create() => new()
    {
        Events = new List<ReplayEvent>(),
        Down = new bool[KeybindSettings.Actions.Length],
    };
}

/// <summary>Drives a play entity's keys from a recorded replay.</summary>
public struct ReplayPlayer(Replay replay) : IComponent
{
    public Replay Replay = replay;
    public int Cursor;
    public long LastTimeMs = long.MinValue;
    public bool[] Down = new bool[KeybindSettings.Actions.Length];
}
