using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Scenes;

/// <summary>Creates the initial editor state and 1000x1000 playfield layout.</summary>
public static class EditorScene
{
    private const int FieldSize = 1000;

    public static void Build(EntityStore entities, Vector2 viewportSize)
    {
        float available = Mathf.Min(viewportSize.Y - 60, viewportSize.X * 0.6f);
        float renderedSize = Mathf.Min(FieldSize, available);
        float scale = renderedSize / FieldSize;
        float originY = Mathf.Max(30, (viewportSize.Y - renderedSize) / 2);

        entities.CreateEntity(
            new LevelInfo("Untitled", ""),
            new Playfield(FieldSize, FieldSize),
            new EditorPlayhead(timeMs: 0, bpm: 120, offsetMs: 0, playing: false),
            new EditorView(new Vector2(30, originY), scale, viewportSize));
    }
}
