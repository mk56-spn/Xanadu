using Friflo.Engine.ECS;
using Xanadu.Logic.Editor.Kinds;

namespace Xanadu.Logic.Editor;

/// <summary>Read/write timing for any editor playfield item; only timed kinds have one.</summary>
public static class EditorItemTiming
{
    public static bool TryGet(Entity entity, out long timeMs)
    {
        long? time = ItemKinds.Of(entity)?.Time(entity);
        timeMs = time ?? 0;
        return time is not null;
    }

    public static bool Set(Entity entity, long timeMs) => ItemKinds.Of(entity)?.SetTime(entity, timeMs) == true;
}

/// <summary>Read/write playfield position for any editor item: its kind's <see cref="ItemKind.Position"/>.</summary>
public static class EditorItemPosition
{
    public static bool TryGet(Entity entity, out Vector2 position)
    {
        ItemKind? kind = ItemKinds.Of(entity);
        position = kind?.Position(entity) ?? default;
        return kind is not null;
    }

    public static bool Set(Entity entity, Vector2 position)
    {
        ItemKind? kind = ItemKinds.Of(entity);
        kind?.SetPosition(entity, position);
        return kind is not null;
    }
}
