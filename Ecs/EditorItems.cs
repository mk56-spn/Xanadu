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
            new ItemBlend(BlendMode.Mix),
            new ItemShaderSource(ItemShaderSource.Own),
            DefaultLayer<T>(),
            Tags.Get<EditorItem>());

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
            return entities.CreateEntity(component, new ItemShader(shaderSlot), new ItemCommonShader(commonShaderPath), new SelfModulate(Colors.White), new ItemBlend(BlendMode.Mix), new ItemShaderSource(ItemShaderSource.Own), DefaultLayer<T>(), Tags.Get<EditorItem>());
        if (hasSlot)
            return entities.CreateEntity(component, new ItemShader(shaderSlot), new SelfModulate(Colors.White), new ItemBlend(BlendMode.Mix), new ItemShaderSource(ItemShaderSource.Own), DefaultLayer<T>(), Tags.Get<EditorItem>());
        if (hasCommon)
            return entities.CreateEntity(component, new ItemCommonShader(commonShaderPath), new SelfModulate(Colors.White), new ItemBlend(BlendMode.Mix), new ItemShaderSource(ItemShaderSource.Own), DefaultLayer<T>(), Tags.Get<EditorItem>());
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
    public static bool IsTintable(Entity item) =>
        !item.IsNull &&
        (item.HasComponent<Note>() || item.HasComponent<Mine>() ||
         item.HasComponent<Block>() || item.HasComponent<Spike>() ||
         item.HasComponent<LevelText>() || item.HasComponent<GrassPatch>() ||
         item.HasComponent<LevelPolygon>());

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

        if (entity.HasComponent<Spike>())
        {
            position = entity.GetComponent<Spike>().Center;
            return true;
        }

        if (entity.HasComponent<LevelText>())
        {
            position = entity.GetComponent<LevelText>().Position;
            return true;
        }

        if (entity.HasComponent<GrassPatch>())
        {
            position = entity.GetComponent<GrassPatch>().Position;
            return true;
        }

        if (entity.HasComponent<LevelPolygon>())
        {
            position = entity.GetComponent<LevelPolygon>().Position;
            return true;
        }

        if (entity.HasComponent<ScreenReadRegion>())
        {
            position = entity.GetComponent<ScreenReadRegion>().Bounds.GetCenter();
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

        if (entity.HasComponent<Spike>())
        {
            ref Spike spike = ref entity.GetComponent<Spike>();
            spike.Position = position - spike.Size / 2;
            return true;
        }

        if (entity.HasComponent<LevelText>())
        {
            entity.GetComponent<LevelText>().Position = position;
            return true;
        }

        if (entity.HasComponent<GrassPatch>())
        {
            entity.GetComponent<GrassPatch>().Position = position;
            return true;
        }

        if (entity.HasComponent<LevelPolygon>())
        {
            entity.GetComponent<LevelPolygon>().Position = position;
            return true;
        }

        if (entity.HasComponent<ScreenReadRegion>())
        {
            ref ScreenReadRegion region = ref entity.GetComponent<ScreenReadRegion>();
            region.Position = position - region.Size / 2;
            return true;
        }

        return false;
    }
}
