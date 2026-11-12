using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// Generic radial option selector, the Xanadu.UI "cheese wheel". The rectangle
/// bounds define the wheel's diameter and every wedge picks one entry from
/// <see cref="Labels"/>. Wedges are blue until selected; the selected wedge is
/// yellow. Interaction and painting live in the matching systems, so the
/// component can be used by any ECS scene that includes the Xanadu.UI pipeline.
/// </summary>
public struct UiWheel : IComponent
{
    public int Selected;
    public int Hover;

    public string[] Labels;
    public Color[]? Colors;

    /// <summary>Selected wedge and hub label colour; defaults to yellow.</summary>
    public Color? SelectedColor;

    /// <summary>Outline colour of the rim and hub; defaults to dark blue.</summary>
    public Color? RimColor;

    /// <summary>Centre hole radius as a fraction of the wheel radius.</summary>
    public float HubRadius;

    /// <summary>Angle of the first wedge's centre; defaults to straight up.</summary>
    public float StartAngle;

    /// <summary>Gap between neighbouring wedges, as its angle at the rim in radians. Its width stays the same towards the hub.</summary>
    public float Gap;
}
