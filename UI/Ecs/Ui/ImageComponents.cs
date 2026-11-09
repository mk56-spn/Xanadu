using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>Texture drawn inside an ECS rectangle, by its canvas texture id (<see cref="Xanadu.Systems.Canvas.TextureServer"/>).</summary>
public struct UiImage(Rid texture) : IComponent
{
    public Rid Texture = texture;
}
