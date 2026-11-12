using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>A step button on the timing screen; its command's <see cref="UiCommand.Arg"/> is the step.</summary>
public enum SongTimingAdjustKind
{
    Bpm,
    Offset,
}

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

/// <summary>
/// The timing screen's other buttons. Remove's command <see cref="UiCommand.Arg"/>
/// is its row slot in the BPM changes list.
/// </summary>
public enum SongTempoAction
{
    Add,
    Remove,
    ScrollUp,
    ScrollDown,
    Save,
}
