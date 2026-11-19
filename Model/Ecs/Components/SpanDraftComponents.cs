using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// A route or hold note being laid, on the editor entity. Both go down the same
/// way: a click puts the head at the snapped playhead, the playhead is moved on,
/// and the next click marks a time there. A route keeps taking clicks as turns
/// until ended; a hold's first one is its tail, which also takes the click's spot.
/// Right click, Enter or a tool change ends either at the playhead; Esc drops it.
/// </summary>
public struct EditorSpanDraft() : IComponent
{
    /// <summary>Which tool is laying it: <see cref="EditorPlacementMode.Curve"/> or <see cref="EditorPlacementMode.Hold"/>.</summary>
    public EditorPlacementMode Mode;

    /// <summary>Where the head goes; only meaningful while <see cref="Active"/>.</summary>
    public Vector2 Head;

    /// <summary>The note's time: the snapped playhead when the head went down.</summary>
    public long StartMs;

    /// <summary>The speed a route runs at, picked when the head went down.</summary>
    public DashStrength Strength;

    /// <summary>A route's turn times so far, ms after the head; the last becomes the route's end.</summary>
    public List<long> TimesMs = new();

    /// <summary>Set by Enter and Esc; the input system acts on them next frame.</summary>
    public bool FinishRequested;
    public bool CancelRequested;

    public bool Active;
}
