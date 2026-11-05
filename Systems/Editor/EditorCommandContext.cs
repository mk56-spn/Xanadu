using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>
/// What every editor command handler works with: the store, the undo history,
/// and the edits that must wait until the command query has finished.
/// </summary>
public sealed class EditorCommandContext(EntityStore entities, EditorHistory history)
{
    public EntityStore Entities { get; } = entities;

    public EditorHistory History { get; } = history;

    /// <summary>Edits that change existing entities, which cannot happen while the query runs.</summary>
    public List<Action> AfterQuery { get; } = new();
}
