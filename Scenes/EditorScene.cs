using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Strings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Scenes;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static partial class EditorScene
{
    private const int FieldSize = LevelDocument.DefaultFieldSize;

    public static void Build(
        EntityStore entities,
        Vector2 viewportSize,
        Entity? parent = null,
        LevelEntry? entry = null)
    {
        var field = new Playfield(FieldSize, FieldSize);
        PlayfieldView view = EditorViewport.Open(viewportSize, field);

        Entity editor = entities.CreateEntity(
            new LevelInfo("Untitled", ""),
            new LevelFile(""),
            field,
            new Playhead(
                timeMs: 0,
                bpm: 120,
                offsetMs: 0,
                playing: false),
            new NoteSelection(default),
            new EditorMetronome(false),
            new TimelineView(32f),
            new PlayfieldMode(PlayfieldKind.Editor),
            view);

        editor.AddComponent(new EditorViewNavigation(new Vector2(field.Width, field.Height)));
        editor.AddComponent(new UiMarqueeBox());
        editor.AddComponent(new WaveformPreview(""));
        editor.AddComponent(new EditorPlacement());
        editor.AddComponent(new EditorPolygonDraft());
        editor.AddComponent(new EditorPlacementGhost());
        editor.AddComponent(new EditorUiRequest());
        editor.AddComponent(LevelGroups.Create());
        editor.AddComponent(EditorGroupState.Closed);
        editor.AddComponent(EditorAnalysis.Create());
        editor.AddComponent(LevelShaders.Create());

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
            BuildToolbar(entities, menuRoot, editor, viewportSize);
            BuildDialogs(entities, menuRoot, editor, viewportSize);
        }

        if (entry is not null)
        {
            LevelDocumentMapper.Apply(entities, editor, entry.Document);
            if (editor.HasComponent<LevelFile>())
                editor.GetComponent<LevelFile>().Path = entry.Path;
        }
    }






























}
