using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Editor.Kinds;

/// <summary>
/// Every kind of placed item. Adding a kind is adding it here: everything that
/// treats items alike — selection, moving, resizing, turning, picking, undo,
/// copy and paste, saving — loops over <see cref="All"/>.
/// </summary>
public static class ItemKinds
{
    public static readonly NoteItemKind Notes = new();

    /// <summary>
    /// Every kind, in the order items are made when a level loads or an undo
    /// lands; later items draw over earlier ones on the same layer and z.
    /// </summary>
    public static readonly ItemKind[] All =
    {
        Notes,
    };

    /// <summary>
    /// The order a click looks for an item in.
    /// </summary>
    public static readonly ItemKind[] PickOrder =
    {
        Notes,
    };

    private static readonly Dictionary<Type, ItemKind> ByComponent = new()
    {
        [typeof(Note)] = Notes,
    };

    /// <summary>The kind an entity is, or null when it is not a placed item.</summary>
    public static ItemKind? Of(Entity entity)
    {
        if (entity.IsNull)
            return null;

        foreach (ItemKind kind in All)
        {
            if (kind.Has(entity))
                return kind;
        }

        return null;
    }

    /// <summary>The kind whose component is <typeparamref name="T"/>, or null.</summary>
    public static ItemKind? For<T>() where T : struct, IComponent =>
        ByComponent.TryGetValue(typeof(T), out ItemKind? kind) ? kind : null;
}
