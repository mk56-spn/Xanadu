using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Settings;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The keynote, momentum and dash shapes: momentum notes carry an arrow with a
/// chevron per extra strength step, and a dash note is joined to its landing.
/// </summary>
public sealed class NoteDesignsSceneTest : SceneTest
{
    public override string Name => "Note designs";
    public override string Slug => "note-designs";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the editor playfield",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<PlayfieldView>().Count > 0);
        AddStep("place the notes", () =>
        {
            PlayfieldView view = default;
            foreach (Entity entity in Context.Entities.Query<PlayfieldView>().Entities)
                view = entity.GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => view.ScreenToPlayfield(new Vector2(x, y));

            Context.Entities.CreateEntity(new Note(0, At(500, 150), target: KeybindAction.M));
            Context.Entities.CreateEntity(new Note(0, At(650, 150), target: KeybindAction.L1));

            Context.Entities.CreateEntity(new Note(
                0, At(500, 370), kind: NoteKind.Momentum,
                dashDirection: DashDirection.UpLeft, momentum: 1200f));
            Context.Entities.CreateEntity(new Note(
                0, At(700, 370), kind: NoteKind.Momentum,
                dashDirection: DashDirection.Right, momentum: 900f));
            Context.Entities.CreateEntity(new Note(
                0, At(900, 370), kind: NoteKind.Momentum,
                dashDirection: DashDirection.Down, momentum: 600f));

            Context.Entities.CreateEntity(new Note(
                0, At(500, 600), kind: NoteKind.Dash,
                dashDirection: DashDirection.Right, dashStrength: DashStrength.Single));
            Context.Entities.CreateEntity(new Note(
                0, At(900, 750), kind: NoteKind.Dash,
                dashDirection: DashDirection.UpRight, dashStrength: DashStrength.Triple));
        });
        AddUntilStep("all seven notes exist", () => Context.Entities.Query<Note>().Count == 7);
        AddWaitStep("let the notes settle", 4);
    }
}
