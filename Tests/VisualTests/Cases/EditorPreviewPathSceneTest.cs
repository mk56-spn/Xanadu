using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using NVector2 = System.Numerics.Vector2;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Leaving an editor preview shows the path the player's hitbox took over the
/// editor's field, in the field's own units.
/// </summary>
public sealed class EditorPreviewPathSceneTest : SceneTest
{
    private int _launches;

    public override string Name => "Editor preview path";
    public override string Slug => "editor-preview-path";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<LevelInfo>().Count > 0);
        AddAssert("a fresh editor has no path", () => Context.Entities.Query<PlayerPath>().Count == 0);
        AddStep("preview a level", StartPreview);
        AddUntilStep("the preview starts", () =>
            Context.Scenes.Current == SceneKind.Play &&
            Context.Entities.Query<PlayerPathRecorder>().Count == 1 &&
            Play().GetComponent<PlaySession>().Started &&
            Play().HasComponent<PhysicsBody>());
        AddUntilStep("the player arcs around the field", Launch);
        AddStep("pause", () => Context.Scenes.PausePlay());
        AddWaitStep("hold pause", 2);
        AddStep("leave the preview", () => Context.Scenes.LeavePause());
        AddUntilStep(
            "back in the editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<LevelInfo>().Count > 0 &&
                  Context.Entities.Query<PlayerPath>().Count == 1);
        AddAssert("the path has samples", () =>
            Context.Entities.Query<PlayerPath>().ToEntityList()[0]
                .GetComponent<PlayerPath>().Samples.Count > 10);
        AddAssert("the recording is gone with the preview", () =>
            Context.Entities.Query<PlayerPathRecorder>().Count == 0);
        AddAssert("the path starts shown", () => PathToggle().GetComponent<Toggle>().Value);
        AddStep("click PATH", ClickPathToggle);
        AddWaitStep("hold PATH", 2);
        AddStep("release PATH", () => Context.Pointer.Release());
        AddUntilStep("the click hides the path", () => !PlayerPathSwitch.IsVisible(Context.Entities));
        AddStep("click PATH again", ClickPathToggle);
        AddWaitStep("hold PATH again", 2);
        AddStep("release PATH again", () => Context.Pointer.Release());
        AddUntilStep("the click shows it again", () => PlayerPathSwitch.IsVisible(Context.Entities));
        AddWaitStep("hold for a look", 5);
    }

    private void StartPreview()
    {
        var song = TestContent.Song("visual-path", "Visual Path");
        LevelDocument document = TestContent.Level("Path Test", song);
        document.Notes.Add(new NoteDocument { TimeMs = 60_000, X = 460, Y = 700 });
        Context.Scenes.StartEditorPreview(new LevelEntry("visual://path", document, song), 0);
    }

    private bool Launch()
    {
        var body = Play().GetComponent<PhysicsBody>().Body;
        if (++_launches % 25 == 1)
            body.SetLinearVelocity(new NVector2(_launches < 100 ? 4f : -4f, -7f));
        return _launches > 150;
    }

    private Entity PathToggle() =>
        Context.Entities.Query<Toggle>().AllTags(Tags.Get<PlayerPathToggle>()).ToEntityList()[0];

    private void ClickPathToggle()
    {
        Entity button = PathToggle();
        Context.Pointer.Click(UiTransform.WorldPosition(button, button.GetComponent<Position>().Value) + new Vector2(10, 10));
    }

    private Entity Play() => Context.Entities.Query<PlaySession>().ToEntityList().First();
}
