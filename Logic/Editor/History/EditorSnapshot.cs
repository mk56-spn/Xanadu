using System.Collections.Generic;
using Xanadu.Logic.Editor.Kinds;
using Xanadu.Model.Levels;

namespace Xanadu.Logic.Editor.History;

/// <summary>
///     The level's editable state as undo keeps it: every placed item as an
///     <see cref="ItemCopy" />, in <see cref="ItemKinds.All" /> order and sorted
///     within each kind, and the level's groups as copies, in their order.
/// </summary>
public readonly record struct EditorSnapshot(List<ItemCopy> Items, List<GroupDocument>? Groups = null);
