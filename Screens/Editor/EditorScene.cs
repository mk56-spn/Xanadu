using Friflo.Engine.ECS;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Levels;
using Xanadu.Model.Editor;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor;

namespace Xanadu.Screens.Editor;

/// <summary>Creates the initial editor state and a playfield centered between the header and footer.</summary>
public static class EditorScene
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
            new LevelInfo("Untitled"),
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
        editor.AddComponent(new EditorPlacementGhost());
        editor.AddComponent(new EditorUiRequest());

        if (parent is { } root)
            root.AddChild(editor);

        Entity popup = EditorWarningPopup.Build(entities);
        Entity popupLayer = popup.Parent;

        if (parent is { } sceneRoot && !popupLayer.IsNull)
            sceneRoot.AddChild(popupLayer);

        if (parent is { } menuRoot)
        {
            EditorSceneMetadata.BuildEditorUi(entities, editor, menuRoot, viewportSize);

            EditorSceneChrome.BuildEditorChrome(entities, menuRoot, editor, viewportSize);
            EditorSceneChrome.BuildToolbar(entities, menuRoot, editor, viewportSize);
            EditorSceneDialogs.BuildDialogs(entities, menuRoot, editor, viewportSize);
        }

        if (entry is not null)
        {
            LevelDocumentMapper.Apply(entities, editor, entry.Document, entry.Song);
            if (editor.HasComponent<LevelFile>())
                editor.GetComponent<LevelFile>().Path = entry.Path;
        }
    }






























}
