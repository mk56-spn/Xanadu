using Friflo.Engine.ECS;

namespace Writer.Ecs;

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

public struct EditorSongTimingAction : IComponent;

public struct SongTimingView(Entity title, Entity bpm, Entity offset) : IComponent
{
    public Entity Title = title;
    public Entity Bpm = bpm;
    public Entity Offset = offset;
}
