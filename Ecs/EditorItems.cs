using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>Creates entities that participate in editor selection and deletion.</summary>
public static class EditorItems
{
    public static Entity Create<T>(EntityStore entities, T component)
        where T : struct, IComponent =>
        entities.CreateEntity(component, Tags.Get<EditorItem>());

    public static bool IsItem(Entity entity) =>
        !entity.IsNull && entity.Tags.Has<EditorItem>();
}

/// <summary>Read/write timing for any editor playfield item.</summary>
public static class EditorItemTiming
{
    public static bool TryGet(Entity entity, out long timeMs)
    {
        if (entity.HasComponent<Note>())
        {
            timeMs = entity.GetComponent<Note>().TimeMs;
            return true;
        }

        if (entity.HasComponent<Mine>())
        {
            timeMs = entity.GetComponent<Mine>().TimeMs;
            return true;
        }

        if (entity.HasComponent<StartPoint>())
        {
            timeMs = entity.GetComponent<StartPoint>().TimeMs;
            return true;
        }

        timeMs = 0;
        return false;
    }

    public static bool Set(Entity entity, long timeMs)
    {
        if (entity.HasComponent<Note>())
        {
            entity.GetComponent<Note>().TimeMs = timeMs;
            return true;
        }

        if (entity.HasComponent<Mine>())
        {
            entity.GetComponent<Mine>().TimeMs = timeMs;
            return true;
        }

        if (entity.HasComponent<StartPoint>())
        {
            entity.GetComponent<StartPoint>().TimeMs = timeMs;
            return true;
        }

        return false;
    }
}

/// <summary>Read/write playfield position for any editor item.</summary>
public static class EditorItemPosition
{
    public static bool TryGet(Entity entity, out Vector2 position)
    {
        if (entity.HasComponent<Note>())
        {
            position = entity.GetComponent<Note>().Position;
            return true;
        }

        if (entity.HasComponent<Mine>())
        {
            position = entity.GetComponent<Mine>().Position;
            return true;
        }

        if (entity.HasComponent<StartPoint>())
        {
            position = entity.GetComponent<StartPoint>().Position;
            return true;
        }

        if (entity.HasComponent<Block>())
        {
            position = entity.GetComponent<Block>().Center;
            return true;
        }

        position = default;
        return false;
    }

    public static bool Set(Entity entity, Vector2 position)
    {
        if (entity.HasComponent<Note>())
        {
            entity.GetComponent<Note>().Position = position;
            return true;
        }

        if (entity.HasComponent<Mine>())
        {
            entity.GetComponent<Mine>().Position = position;
            return true;
        }

        if (entity.HasComponent<StartPoint>())
        {
            entity.GetComponent<StartPoint>().Position = position;
            return true;
        }

        if (entity.HasComponent<Block>())
        {
            ref Block block = ref entity.GetComponent<Block>();
            block.Position = position - block.Size / 2;
            return true;
        }

        return false;
    }
}
