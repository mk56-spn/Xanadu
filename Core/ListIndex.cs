using Friflo.Engine.ECS;

namespace Xanadu.Core;

/// <summary>
/// The entity's place in an ordered list it belongs to: a note's slot in the
/// run's chart, a tile's row, a caption's line. What the list is comes from
/// the entity (its tags, its parent), not from this component.
/// </summary>
public struct ListIndex(int value) : IComponent
{
    public int Value = value;

    /// <summary>The entity's place in its list, or -1 when it has none.</summary>
    public static int Of(Entity entity) =>
        !entity.IsNull && entity.TryGetComponent(out ListIndex index) ? index.Value : -1;
}
