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

    /// <summary>The BPM changes list (<see cref="UiList"/>): it stands for the song's changes and scrolls through them.</summary>
    public Entity List;

    /// <summary>The list's pooled rows, top first; row <c>i</c> shows change <c>First + i</c>.</summary>
    public SongTempoRowUi[] Rows;

    /// <summary>Shown when the song has no BPM changes.</summary>
    public Entity Empty;

    /// <summary>Under the list: which changes show, of how many, when they don't all fit.</summary>
    public Entity Foot;
}

/// <summary>One pooled BPM change row: its moment, where that falls, its tempo, its bar length and remove.</summary>
public readonly record struct SongTempoRowUi(Entity At, Entity Position, Entity Bpm, Entity Bar, Entity Remove);

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

/// <summary>A typed timing field; <see cref="Slot"/> is the pooled row for change fields, which shows change <c>First + Slot</c>.</summary>
public struct SongTimingField(SongTimingFieldKind kind, int slot = 0) : IComponent
{
    public SongTimingFieldKind Kind = kind;
    public int Slot = slot;
}

/// <summary>
/// The timing screen's other buttons. Remove's command <see cref="UiCommand.Arg"/>
/// is its pooled row in the BPM changes list.
/// </summary>
public enum SongTempoAction
{
    Add,
    Remove,
    Save,
}
