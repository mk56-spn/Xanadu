using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Editor;

/// <summary>Creates entities that participate in editor selection and deletion.</summary>
public static class EditorItems
{
    public static Entity Create<T>(EntityStore entities, T component)
        where T : struct, IComponent =>
        entities.CreateEntity(component, Tags.Get<EditorItem>());

    /// <summary>Creates an item a tool has just placed.</summary>
    public static Entity Place<T>(EntityStore entities, T component)
        where T : struct, IComponent =>
        Create(entities, component);

    public static bool IsItem(Entity entity) =>
        !entity.IsNull && entity.Tags.Has<EditorItem>();

    /// <summary>
    /// The most objects a level may hold. Placing, painting and pasting stop
    /// here, so a level stays playable however it is built; loading and undo
    /// do not check it.
    /// </summary>
    public const int MaxItems = 10_000;

    public static int Count(EntityStore entities) =>
        entities.Query().AllTags(Tags.Get<EditorItem>()).Count;

    /// <summary>Whether the level holds <see cref="MaxItems"/> already; unlike <see cref="WouldOverfill"/>, says nothing.</summary>
    public static bool IsFull(EntityStore entities) => Count(entities) >= MaxItems;

    /// <summary>
    /// Whether adding <paramref name="adding"/> more objects would pass
    /// <see cref="MaxItems"/>. When it would, the editor's status says so.
    /// </summary>
    public static bool WouldOverfill(EntityStore entities, Entity editor, int adding = 1)
    {
        if (Count(entities) + adding <= MaxItems)
            return false;

        if (!editor.IsNull && editor.HasComponent<EditorHud>())
            editor.GetComponent<EditorHud>().SetStatus($"Level is full: {MaxItems:N0} objects at most");
        return true;
    }
}
