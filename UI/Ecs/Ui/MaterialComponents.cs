using Friflo.Engine.ECS;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>A material, such as a shader, applied to a drawable's canvas item.</summary>
public struct UiMaterial(CanvasMaterial material) : IComponent
{
    public CanvasMaterial Material = material;
}
