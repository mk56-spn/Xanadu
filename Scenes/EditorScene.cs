using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;
using Writer.Levels;
using Writer.Settings;
using Writer.Editor;
using Writer.Systems.Editor;
using Writer.Strings;
using Writer.Systems.Playfields;

namespace Writer.Scenes;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static partial class EditorScene
{
    private const int FieldSize = 1000;

    public static void Build(
        EntityStore entities,
        Vector2 viewportSize,
        Entity? parent = null,
        LevelEntry? entry = null)
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

        Entity editor = entities.CreateEntity(
            new LevelInfo("Untitled", ""),
            new LevelFile(""),
            new Playfield(FieldSize, FieldSize),
            new Playhead(
                timeMs: 0,
                bpm: 120,
                offsetMs: 0,
                playing: false),
            new NoteSelection(default),
            new EditorPreview(false, 0, false),
            new EditorMetronome(false),
            new EditorTimeline(32f),
            new PlayfieldMode(PlayfieldKind.Editor),
            new PlayfieldView(new Vector2(originX, originY), scale, viewportSize));

        editor.AddComponent(new EditorSelectionBox(
            Vector2.Zero,
            Vector2.Zero,
            false));
        editor.AddComponent(new WaveformPreview(
            "",
            System.Array.Empty<float>(),
            System.Array.Empty<float>(),
            0));
        editor.AddComponent(new EditorNoteTarget(KeybindAction.M));
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Notes));
        editor.AddComponent(new EditorDashDirectionState(DashDirection.UpRight));
        editor.AddComponent(new EditorUiRequest());

        if (parent is { } root)
            root.AddChild(editor);

        Entity popup = EditorWarningPopup.Build(entities);
        Entity popupLayer = popup.Parent;

        if (parent is { } sceneRoot && !popupLayer.IsNull)
            sceneRoot.AddChild(popupLayer);

        if (parent is { } menuRoot)
        {
            BuildEditorUi(entities, editor, menuRoot, viewportSize);

            BuildEditorChrome(entities, menuRoot, editor, viewportSize);
            BuildSidebar(entities, menuRoot, editor, viewportSize);
            BuildToolbar(entities, menuRoot, editor, viewportSize);
            }

        if (entry is not null)
        {
                LevelDocumentMapper.Apply(entities, editor, entry.Document);
                if (editor.HasComponent<LevelFile>())
                editor.GetComponent<LevelFile>().Path = entry.Path;
        }
    }






























}
