using Friflo.Engine.ECS.Systems;
using Writer.Systems.Canvas;
using Writer.Systems.Layout;
using Writer.Systems.Rendering;

namespace Writer.Systems.Groups;

/// <summary>Applies transforms and redraws dirty rectangles and text.</summary>
public sealed class PresentationSystemGroup : SystemGroup
{
    public PresentationSystemGroup(IRectCanvas canvas) : base("Presentation")
    {
        Add(new TransformSystem(canvas));
        Add(new RectDrawSystem(canvas));
        Add(new TextDrawSystem(canvas));
    }
}
