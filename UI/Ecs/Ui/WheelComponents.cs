using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>
/// Generic radial option selector, the Writer.UI "cheese wheel". The rectangle
/// bounds define the wheel's diameter and every wedge picks one entry from
/// <see cref="Labels"/>. Wedges are blue until selected; the selected wedge is
/// yellow. Interaction and painting live in the matching systems, so the
/// component can be used by any ECS scene that includes the Writer.UI pipeline.
/// </summary>
public struct UiWheel : IComponent
{
    public int Selected;
    public int Hover;

    public string[] Labels;
    public Color[]? Colors;

    /// <summary>Centre hole radius as a fraction of the wheel radius.</summary>
    public float HubRadius;

    /// <summary>Angle of the first wedge's centre; defaults to straight up.</summary>
    public float StartAngle;

    /// <summary>Angular gap between neighbouring wedges, in radians.</summary>
    public float Gap;
}

/// <summary>Set for one frame when the user selects a different wheel wedge.</summary>
public struct UiWheelChanged(int index) : IComponent
{
    public int Index = index;
}
