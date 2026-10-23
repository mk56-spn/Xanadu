using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Creates entities that participate in editor selection and deletion.</summary>
public static class EditorItems
{
    public static Entity Create<T>(EntityStore entities, T component)
        where T : struct, IComponent =>
        entities.CreateEntity(component, Tags.Get<EditorItem>());
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
