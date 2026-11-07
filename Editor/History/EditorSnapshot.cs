using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>
/// The level's editable state as undo keeps it: every placed item as an
/// <see cref="ItemCopy"/>, in <see cref="ItemKinds.All"/> order and sorted
/// within each kind, plus the group and layer tables.
/// </summary>
public readonly record struct EditorSnapshot(
    List<ItemCopy> Items,
    GroupDefinition[]? Groups = null,
    LevelLayers? Layers = null);
