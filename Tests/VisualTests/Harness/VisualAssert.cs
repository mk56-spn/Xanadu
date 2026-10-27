using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Tests.VisualTests;

/// <summary>Small ECS assertions shared by visual tests.</summary>
public static class VisualAssert
{
    public static bool HasText(EntityStore store, string value)
    {
        foreach (Entity entity in store.Query<Text>().ToEntityList())
        {
            if (entity.GetComponent<Text>().Value == value)
                return true;
        }

        return false;
    }

    public static bool HasAnimatedText(EntityStore store, string value)
    {
        foreach (Entity entity in store.Query<ButtonVisual, Text>().ToEntityList())
        {
            if (entity.GetComponent<Text>().Value == value)
                return true;
        }

        return false;
    }

    public static Entity LayerAt(EntityStore store, int zIndex)
    {
        foreach (Entity entity in store.Query<Layer>().ToEntityList())
        {
            if (entity.GetComponent<Layer>().ZIndex == zIndex)
                return entity;
        }

        return default;
    }

    public static bool HasLayer(EntityStore store, int zIndex) =>
        !LayerAt(store, zIndex).IsNull;

    public static int CountUninitialised(EntityStore store, int zIndex)
    {
        int count = 0;

        foreach (Entity entity in store.Query<Rectangle>().ToEntityList())
        {
            if (entity.GetComponent<Drawable>().ZIndex == zIndex &&
                entity.Tags.Has<Uninitialised>())
            {
                count++;
            }
        }

        foreach (Entity entity in store.Query<Layer>().ToEntityList())
        {
            if (entity.GetComponent<Layer>().ZIndex == zIndex &&
                entity.Tags.Has<Uninitialised>())
            {
                count++;
            }
        }

        return count;
    }

    public static bool IsSceneReady(
        VisualTestContext context,
        SceneKind scene,
        params int[] layers)
    {
        if (context.Scenes.Current != scene)
            return false;

        foreach (int layer in layers)
        {
            if (!HasLayer(context.Entities, layer) ||
                CountUninitialised(context.Entities, layer) > 0)
            {
                return false;
            }
        }

        return true;
    }
}
