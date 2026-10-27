using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>Texture drawn inside an ECS rectangle.</summary>
public struct UiImage(Texture2D texture) : IComponent
{
    public Texture2D Texture = texture;
}
