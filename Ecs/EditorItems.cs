using Friflo.Engine.ECS;
using Godot;
using Xanadu.Rendering;

namespace Xanadu.Ecs;

/// <summary>
/// Creates entities that participate in editor selection and deletion. Every
/// one carries its kind's default <see cref="ItemLayer"/>.
/// </summary>
public static class EditorItems
{
    public static Entity Create<T>(EntityStore entities, T component)
        where T : struct, IComponent =>
        entities.CreateEntity(
            component,
            new SelfModulate(Colors.White),
            new ItemBlend(DefaultBlend<T>()),
            new ItemShaderSource(ItemShaderSource.Own),
            DefaultLayer<T>(),
            Tags.Get<EditorItem>());

    /// <summary>The blend an item of this kind starts with (<see cref="ItemKind.DefaultBlend"/>).</summary>
    public static BlendMode DefaultBlend<T>() where T : struct, IComponent =>
        ItemKinds.For<T>()?.DefaultBlend ?? BlendMode.Mix;

    /// <summary>
    /// Creates an item that already carries its shader. Adding the component
    /// afterwards is a structural change, which editor commands cannot make
    /// while they are being processed.
    /// </summary>
    public static Entity Create<T>(EntityStore entities, T component, int shaderSlot)
        where T : struct, IComponent =>
        Create(entities, component, shaderSlot, "");

    /// <summary>
    /// Creates an item that already carries its map shader slot and, or
    /// instead, a common shader by path.
    /// </summary>
    public static Entity Create<T>(EntityStore entities, T component, int shaderSlot, string commonShaderPath)
        where T : struct, IComponent
    {
        bool hasSlot = LevelShaders.IsSlot(shaderSlot);
        bool hasCommon = !string.IsNullOrEmpty(commonShaderPath);

        if (hasSlot && hasCommon)
            return entities.CreateEntity(component, new ItemShader(shaderSlot), new ItemCommonShader(commonShaderPath), new SelfModulate(Colors.White), new ItemBlend(DefaultBlend<T>()), new ItemShaderSource(ItemShaderSource.Own), DefaultLayer<T>(), Tags.Get<EditorItem>());
        if (hasSlot)
            return entities.CreateEntity(component, new ItemShader(shaderSlot), new SelfModulate(Colors.White), new ItemBlend(DefaultBlend<T>()), new ItemShaderSource(ItemShaderSource.Own), DefaultLayer<T>(), Tags.Get<EditorItem>());
        if (hasCommon)
            return entities.CreateEntity(component, new ItemCommonShader(commonShaderPath), new SelfModulate(Colors.White), new ItemBlend(DefaultBlend<T>()), new ItemShaderSource(ItemShaderSource.Own), DefaultLayer<T>(), Tags.Get<EditorItem>());
        return Create(entities, component);
    }

    /// <summary>
    /// Creates an item a tool has just placed: on its kind's default layer, in
    /// front of everything already there. Loading and undo use
    /// <see cref="Create{T}(EntityStore, T)"/> and set the saved layer instead.
    /// </summary>
    public static Entity Place<T>(EntityStore entities, T component)
        where T : struct, IComponent
    {
        Entity created = Create(entities, component);
        ItemLayer.OnTop(entities, created);

        // A tool-placed item wears the colour picked while nothing was selected.
        foreach (Entity editor in entities.Query<EditorPlacement>().ToEntityList())
        {
            Color modulate = editor.GetComponent<EditorPlacement>().Modulate;
            if (modulate != Colors.White && IsTintable(created))
                SelfModulate.Put(created, modulate);
            break;
        }

        return created;
    }

    /// <summary>The kind's default layer at z 0; placing tools then call <see cref="ItemLayer.OnTop(EntityStore, Entity)"/>.</summary>
    private static ItemLayer DefaultLayer<T>() where T : struct, IComponent => new(ItemLayer.DefaultFor<T>(), 0);

    public static bool IsItem(Entity entity) =>
        !entity.IsNull && entity.Tags.Has<EditorItem>();

    /// <summary>Whether the colour field's self modulate applies to an item.</summary>
    public static bool IsTintable(Entity item) => ItemKinds.Of(item)?.Tintable == true;

    /// <summary>
    /// The most objects a level may hold. Placing, painting and pasting stop
    /// here, so a level stays playable however it is built; loading and undo
    /// do not check it.
    /// </summary>
    public const int MaxItems = 10_000;

    public static int Count(EntityStore entities) =>
        entities.Query().AllTags(Tags.Get<EditorItem>()).Count;

    /// <summary>
    /// Whether adding <paramref name="adding"/> more objects would pass
    /// <see cref="MaxItems"/>. When it would, the editor's status says so.
    /// </summary>
    public static bool WouldOverfill(EntityStore entities, Entity editor, int adding = 1)
    {
        if (Count(entities) + adding <= MaxItems)
            return false;

        if (!editor.IsNull && editor.HasComponent<EditorHud>())
            editor.GetComponent<EditorHud>().StatusText = $"Level is full: {MaxItems:N0} objects at most";
        return true;
    }
}

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
