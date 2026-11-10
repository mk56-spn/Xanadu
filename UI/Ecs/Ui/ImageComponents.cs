using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Texture drawn inside an ECS rectangle, by its canvas texture id (<see cref="Xanadu.Systems.Canvas.TextureServer"/>).</summary>
public struct UiImage(Handle texture) : IComponent
{
    public Handle Texture = texture;
}
