using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Creates entities that participate in editor selection and deletion.</summary>
public static class EditorItems
{
    public static Entity Create<T>(EntityStore entities, T component)
        where T : struct, IComponent =>
        entities.CreateEntity(component, Tags.Get<EditorItem>());
}
