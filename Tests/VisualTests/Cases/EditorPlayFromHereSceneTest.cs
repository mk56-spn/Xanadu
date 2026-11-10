using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Playing from the playhead begins there, and leaving comes back to the editor
/// with its view, tool and selection as they were.
/// </summary>
public sealed class EditorPlayFromHereSceneTest : SceneTest
{
    private const long HereMs = 4_000;
    private static readonly Vector2 SpikeAt = new(100, 100);
    private float _scale;

    public override string Name => "Editor play from here";
    public override string Slug => "editor-play-from-here";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep("wait for the editor", () =>
            Context.Scenes.Current == SceneKind.Editor &&
            Context.Entities.Query<EditorLeftMenu>().Count > 0);
        AddStep("add a spike", () =>
            EditorItems.Create(Context.Entities, new Spike(SpikeAt, new Vector2(50, 50), 1)));
        AddStep("set the tool, zoom and selection", () =>
        {
            Entity editor = Editor();
            editor.GetComponent<EditorPlacement>().Mode = EditorPlacementMode.Spikes;
            ref PlayfieldView view = ref editor.GetComponent<PlayfieldView>();
            view.Scale *= 0.8f;
            _scale = view.Scale;
            editor.GetComponent<NoteSelection>().Note =
                Context.Entities.Query<Spike>().ToEntityList()[0];
        });
        AddUntilStep("spike mode hides the left panel", () =>
            !Editor().GetComponent<EditorLeftMenu>().Panel.Enabled);
        AddStep("play from the playhead", () =>
        {
            var song = TestContent.Song("visual-here", "Here");
            LevelDocument document = LevelDocumentMapper.Capture(Context.Entities, Editor());
            Context.Scenes.StartEditorPreview(
                new LevelEntry("visual://here", document, song),
                HereMs,
                fromPlayhead: true);
        });
        AddUntilStep("the preview is playing", () =>
            Context.Scenes.Current == SceneKind.Play &&
            Context.Entities.Query<PlaySession>().Count == 1);
        AddAssert("it begins at the playhead", () =>
            Context.Scenes.CurrentPreviewStart is { TimeMs: HereMs });
        AddUntilStep("it has started", () =>
            Context.Entities.Query<PlaySession>().ToEntityList()[0]
                .GetComponent<PlaySession>().Started);
        AddAssert("the song is at the playhead", () =>
            Context.Entities.Query<Playhead, PlaySession>().ToEntityList()[0]
                .GetComponent<Playhead>().TimeMs >= HereMs);
        AddStep("pause", () => Context.Scenes.PausePlay());
        AddWaitStep("hold pause", 2);
        AddStep("leave", () => Context.Scenes.LeavePause());
        AddUntilStep("back in the editor", () =>
            Context.Scenes.Current == SceneKind.Editor &&
            Context.Entities.Query<EditorLeftMenu>().Count > 0);
        AddAssert("the playhead stayed", () => Editor().GetComponent<Playhead>().TimeMs == HereMs);
        AddAssert("the zoom stayed", () => Editor().GetComponent<PlayfieldView>().Scale == _scale);
        AddAssert("the tool stayed", () =>
            Editor().GetComponent<EditorPlacement>().Mode == EditorPlacementMode.Spikes);
        AddAssert("the selection stayed", () =>
            Editor().GetComponent<NoteSelection>().AllNotes.Count == 1 &&
            Editor().GetComponent<NoteSelection>().AllNotes[0].HasComponent<Spike>());
    }

    private Entity Editor() => Context.Entities
        .Query<PlayfieldMode, EditorLeftMenu>()
        .ToEntityList()
        .First();
}
