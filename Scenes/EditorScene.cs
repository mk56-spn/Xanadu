using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Systems.Editor;

namespace Writer.Scenes;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static class EditorScene
{
    private const int FieldSize = 1000;

    public static void Build(EntityStore entities, Vector2 viewportSize)
    {
        float top = EditorLayout.HeaderHeight + EditorLayout.Margin;
        float bottom = viewportSize.Y -
                       EditorLayout.FooterHeight -
                       EditorLayout.TransportHeight -
                       EditorLayout.Gap -
                       EditorLayout.Margin;
        float availableHeight = Mathf.Max(1, bottom - top);
        float availableWidth = Mathf.Max(1, viewportSize.X - EditorLayout.Margin * 2);
        float renderedSize = Mathf.Min(FieldSize, Mathf.Min(availableHeight, availableWidth));
        float scale = renderedSize / FieldSize;
        float originX = (viewportSize.X - renderedSize) / 2;
        float originY = top + (availableHeight - renderedSize) / 2;

        entities.CreateEntity(
            new LevelInfo("Untitled", ""),
            new LevelFile(""),
            new Playfield(FieldSize, FieldSize),
            new EditorPlayhead(
                timeMs: 0,
                bpm: 120,
                offsetMs: 0,
                playing: false),
            new NoteSelection(default),
            new EditorPreview(false, 0, false),
            new EditorMetronome(false),
            new EditorView(new Vector2(originX, originY), scale, viewportSize));

        EditorWarningPopup.Build(entities);
    }
}
