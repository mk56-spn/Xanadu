using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// The route note the route tool is laying, on the editor entity: the head's
/// place and time from the first click, then a turn at the snapped playhead
/// for each click after, until it is finished into a route note.
/// </summary>
public struct EditorRouteDraft() : IComponent
{
    /// <summary>Where the head goes; only meaningful while <see cref="Active"/>.</summary>
    public Vector2 Head;

    /// <summary>The note's time: the snapped playhead when the head went down.</summary>
    public long StartMs;

    /// <summary>The speed the route runs at, picked when the head went down.</summary>
    public DashStrength Strength;

    /// <summary>Turn times so far, ms after the head; the last becomes the route's end.</summary>
    public List<long> TimesMs = new();

    /// <summary>Set by Enter and Esc; the input system acts on them next frame.</summary>
    public bool FinishRequested;
    public bool CancelRequested;

    public bool Active;
}
