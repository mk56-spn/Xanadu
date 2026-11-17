using Friflo.Engine.ECS;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Editor;

/// <summary>The canvas the editor draws its playfield on, and the store its items live in.</summary>
public sealed class EditorDrawSurface(IRectCanvas canvas, Handle target, EntityStore entities)
{
    public IRectCanvas Canvas { get; } = canvas;

    public Handle Target { get; } = target;

    public EntityStore Entities { get; } = entities;
}
