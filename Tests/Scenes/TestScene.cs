using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Scenes;

/// <summary>Creates throwaway layers and rectangles for exercising the ECS systems.</summary>
public static class TestScene
{
    public static void Build(EntityStore entities)
    {
        UiBuilder.Begin(entities)
            .Layer(0)
                .Rect(new Vector2(140, 80), new Vector2(200, 80), Colors.CornflowerBlue, "Layer 0", true)
                .Rect(new Vector2(340, 80), new Vector2(120, 80), Colors.CornflowerBlue, "Layer 0")
            .Layer(5)
                .Rect(new Vector2(120, 190), new Vector2(160, 60), Colors.Orange, "Layer 5", true)
                .Rect(new Vector2(340, 210), new Vector2(200, 100), Colors.Orange, "Layer 5")
            .Layer(10)
                .Rect(new Vector2(240, 360), new Vector2(320, 120), Colors.MediumSeaGreen, "Layer 10", true)
                .Rect(new Vector2(490, 350), new Vector2(100, 100), Colors.MediumSeaGreen, "Layer 10")
                .Container(
                    new Vector2(850, 200),
                    new Vector2(360, 180),
                    StackDirection.Horizontal,
                    spacing: 12,
                    clip: true)
                    .Rect(Vector2.Zero, new Vector2(90, 70), Colors.SkyBlue, "A")
                    .Rect(Vector2.Zero, new Vector2(90, 70), Colors.Gold, "B", takesInput: true)
                    .Rect(Vector2.Zero, new Vector2(90, 70), Colors.LightGreen, "C")
                .End();
    }
}
