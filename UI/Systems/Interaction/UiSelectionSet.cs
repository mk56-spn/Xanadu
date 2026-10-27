using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Systems.Ui;

/// <summary>Generic ordered entity selection used by UI interaction code.</summary>
public sealed class UiSelectionSet
{
    private readonly List<Entity> _items = new();

    public Entity Primary => _items.Count > 0 ? _items[0] : default;

    public IReadOnlyList<Entity> All => _items;

    public bool Contains(Entity entity) =>
        !entity.IsNull && _items.Exists(item => item.Id == entity.Id);

    public void Set(Entity entity)
    {
        _items.Clear();
        if (!entity.IsNull)
            _items.Add(entity);
    }

    public void SetMany(IEnumerable<Entity> entities)
    {
        _items.Clear();
        foreach (Entity entity in entities)
        {
            if (!entity.IsNull && !Contains(entity))
                _items.Add(entity);
        }
    }

    public void Toggle(Entity entity)
    {
        if (entity.IsNull)
            return;

        int index = _items.FindIndex(item => item.Id == entity.Id);
        if (index >= 0)
            _items.RemoveAt(index);
        else
            _items.Add(entity);
    }

    public void Clear() => _items.Clear();
}
