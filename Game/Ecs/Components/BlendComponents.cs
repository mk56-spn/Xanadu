using Friflo.Engine.ECS;
using Xanadu.Rendering;

namespace Xanadu.Ecs;

/// <summary>
/// The blend mode an item draws with. Every editor item carries one from the
/// moment it is made, so an edit never has to add a component.
/// </summary>
public struct ItemBlend(BlendMode mode) : IComponent
{
    public BlendMode Mode = mode;

    /// <summary>The item's blend, or <see cref="BlendMode.Mix"/>.</summary>
    public static BlendMode Of(Entity entity) =>
        entity.TryGetComponent(out ItemBlend blend) ? blend.Mode : BlendMode.Mix;

    /// <summary>Sets an item's blend, adding the component if it has none.</summary>
    public static void Put(Entity entity, BlendMode mode)
    {
        if (entity.HasComponent<ItemBlend>())
            entity.GetComponent<ItemBlend>().Mode = mode;
        else
            entity.AddComponent(new ItemBlend(mode));
    }
}
