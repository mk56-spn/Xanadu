using Friflo.Engine.ECS;
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

            Context.Entities.CreateEntity(new Note(0, At(500, 150), target: KeybindAction.Action1));
            Context.Entities.CreateEntity(new Note(0, At(650, 150), target: KeybindAction.Action2));
            Context.Entities.CreateEntity(new Note(0, At(800, 150), target: KeybindAction.Action3));
            Context.Entities.CreateEntity(new Note(0, At(950, 150), kind: NoteKind.Neutral));

            Context.Entities.CreateEntity(new Note(0, At(500, 370), kind: NoteKind.Momentum, momentum: 600f));
            Context.Entities.CreateEntity(new Note(0, At(700, 370), kind: NoteKind.Momentum, momentum: 1200f));
            Context.Entities.CreateEntity(new Note(0, At(900, 370), kind: NoteKind.Momentum, momentum: 900f, diagonal: true));

            Context.Entities.CreateEntity(new Note(0, At(500, 600), kind: NoteKind.Dash, dashStrength: DashStrength.Single));
            Context.Entities.CreateEntity(new Note(0, At(700, 600), kind: NoteKind.Dash, dashStrength: DashStrength.Triple));
            Context.Entities.CreateEntity(new Note(0, At(900, 600), kind: NoteKind.Dash, dashStrength: DashStrength.Double, diagonal: true));

            Context.Entities.CreateEntity(new Note(0, At(700, 800), kind: NoteKind.Contact, costs: new BarCosts(2, 0, 4)));
        });
        AddUntilStep("all eleven notes exist", () => Context.Entities.Query<Note>().Count == 11);
        AddWaitStep("let the notes settle", 4);
    }
}
