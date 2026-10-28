using Friflo.Engine.ECS.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Rendering;

namespace Xanadu.Systems.Groups;

/// <summary>Applies transforms and redraws dirty rectangles, wheels, and text.</summary>
public sealed class PresentationSystemGroup : SystemGroup
{
    public PresentationSystemGroup(IRectCanvas canvas) : base("Presentation")
    {
        Add(new TransformSystem(canvas));
        Add(new RectDrawSystem(canvas));
        Add(new WheelDrawSystem(canvas));
        Add(new GradientDrawSystem(canvas));
        Add(new ShapeDrawSystem(canvas));
        Add(new TextDrawSystem(canvas));
        Add(new ImageDrawSystem(canvas));
        Add(new BarGraphDrawSystem(canvas));
    }
}
