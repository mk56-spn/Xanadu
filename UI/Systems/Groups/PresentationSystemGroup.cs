using Friflo.Engine.ECS.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Rendering;

namespace Xanadu.Ui.Systems.Groups;

/// <summary>Applies transforms and redraws dirty rectangles, wheels, and text, and every connector.</summary>
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
        Add(new ConnectorDrawSystem(canvas));
    }
}
