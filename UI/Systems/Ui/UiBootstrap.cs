using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Canvas;
using Writer.Systems.Groups;

namespace Writer.Systems.Ui;

/// <summary>UI-owned assembly points for the generic ECS render pipeline.</summary>
public static class UiBootstrap
{
    public static void AttachInitialization(
        SystemGroup root,
        Rid parentCanvas) =>
        root.Add(new InitializationSystemGroup(parentCanvas));

    public static void AttachPresentation(
        SystemGroup root,
        IRectCanvas canvas) =>
        root.Add(new PresentationSystemGroup(canvas));
}
