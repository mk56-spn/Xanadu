using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// A colour swatch that can be picked up: press it and drag, and its fill
/// colour goes with the pointer to whatever <see cref="UiColorDrop"/> it is
/// let go over. A press that does not move is left to whatever else the
/// swatch does.
/// </summary>
public struct UiColorSwatch : ITag;

/// <summary>
/// Somewhere a dragged colour can be dropped. The drag system leaves the
/// colour here; the owner takes it with <see cref="Take"/> and applies it as
/// if it had been typed or picked.
/// </summary>
public struct UiColorDrop : IComponent
{
    public bool Pending;
    public Color Color;

    /// <summary>Hands over a colour dropped on <paramref name="entity"/>, once.</summary>
    public static bool Take(Entity entity, out Color color)
    {
        color = default;
        if (entity.IsNull || !entity.HasComponent<UiColorDrop>())
            return false;

        ref UiColorDrop drop = ref entity.GetComponent<UiColorDrop>();
        if (!drop.Pending)
            return false;

        drop.Pending = false;
        color = drop.Color;
        return true;
    }
}
