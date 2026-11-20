using System.Collections.Generic;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Kinds;

namespace Xanadu.Logic.Editor.History;

/// <summary>
///     The level's editable state as undo keeps it: every placed item as an
///     <see cref="ItemCopy" />, in <see cref="ItemKinds.All" /> order and sorted
///     within each kind.
/// </summary>
public readonly record struct EditorSnapshot(List<ItemCopy> Items);
