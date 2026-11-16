using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>Texture drawn inside an ECS rectangle, by its canvas texture id (<see cref="TextureServer"/>).</summary>
public struct UiImage(Handle texture) : IComponent
{
    public Handle Texture = texture;
}
