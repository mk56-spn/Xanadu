using Friflo.Engine.ECS;
using Godot;
using Xanadu.Systems.Shaders;

namespace Xanadu.Systems.Editor;

/// <summary>The canvas the editor draws its playfield on, and the store its items live in.</summary>
public sealed class EditorDrawSurface(ShaderRoutingCanvas canvas, Rid target, EntityStore entities)
{
    public ShaderRoutingCanvas Canvas { get; } = canvas;

    public Rid Target { get; } = target;

    public EntityStore Entities { get; } = entities;
}
