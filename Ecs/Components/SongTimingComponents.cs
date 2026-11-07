using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

public enum SongTimingAdjustKind
{
    Bpm,
    Offset,
}

public struct SongTimingAdjust(SongTimingAdjustKind kind, int delta) : IComponent
{
    public SongTimingAdjustKind Kind = kind;
    public int Delta = delta;
}

public struct SongTimingSave : IComponent;

public struct SongTimingView(Entity title, Entity bpm, Entity offset) : IComponent
{
    public Entity Title = title;
    public Entity Bpm = bpm;
    public Entity Offset = offset;

    /// <summary>The BPM changes list's row slots, filled from the song by the controller.</summary>
    public SongTempoRowUi[] Rows;

    /// <summary>Shown when the song has no BPM changes.</summary>
    public Entity Empty;
}

/// <summary>One BPM change row: its moment, where that falls, its tempo, its bar length and remove.</summary>
public readonly record struct SongTempoRowUi(Entity Row, Entity At, Entity Position, Entity Bpm, Entity Bar, Entity Remove);

/// <summary>What a typed field on the timing screen sets.</summary>
public enum SongTimingFieldKind
{
    BaseBpm,
    BaseBar,
    Offset,
    ChangeAt,
    ChangeBpm,
    ChangeBar,
}

/// <summary>A typed timing field; <see cref="Slot"/> is the change row slot for change fields.</summary>
public struct SongTimingField(SongTimingFieldKind kind, int slot = 0) : IComponent
{
    public SongTimingFieldKind Kind = kind;
    public int Slot = slot;
}

public enum SongTempoAction
{
    Add,
    Remove,
    ScrollUp,
    ScrollDown,
}

/// <summary>A button of the BPM changes list; <see cref="Slot"/> is the row slot for Remove.</summary>
public struct SongTempoButton(SongTempoAction action, int slot = 0) : IComponent
{
    public SongTempoAction Action = action;
    public int Slot = slot;
}
