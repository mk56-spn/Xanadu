using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Settings;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Regression: pair, dash, momentum and character-switch notes all draw with
/// their own body, so the four roles read apart at a glance.
/// </summary>
public sealed class CharacterNotesSceneTest : SceneTest
{
    public override string Name => "Character note kinds";
    public override string Slug => "character-notes";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the editor playfield",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<PlayfieldView>().Count > 0);
        AddStep("place the note kinds", () =>
        {
            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(250, 250),
                kind: NoteKind.Pair,
                pairIndex: PairIndex.Two));
            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(500, 250),
                kind: NoteKind.Dash,
                dashDirection: DashDirection.UpRight,
                dashStrength: DashStrength.Double));
            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(750, 250),
                kind: NoteKind.Momentum,
                dashDirection: DashDirection.Left,
                momentum: 600f));
            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(500, 500),
                kind: NoteKind.CharacterSwitch,
                character: CharacterKind.Form));
            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(250, 500),
                kind: NoteKind.Contact,
                contactRequired: 0,
                contactLeft: 2,
                contactRight: 1));
            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(750, 500),
                kind: NoteKind.WeaponSwitch));
            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(300, 300),
                target: KeybindAction.L1,
                kind: NoteKind.Channel,
                channelSide: ChannelSide.Left));
            Context.Entities.CreateEntity(new Note(
                0,
                Vector2.Zero,
                kind: NoteKind.Rail,
                pairIndex: PairIndex.Two,
                channelSide: ChannelSide.Left,
                holdMs: 500));
            Context.Entities.CreateEntity(new Note(
                2_000,
                Vector2.Zero,
                kind: NoteKind.ChannelEnd,
                channelSide: ChannelSide.Left));

            Entity first = Context.Entities.CreateEntity(new Block(
                new Vector2(600, 600),
                new Vector2(90, 50),
                shapeId: 3,
                shapePiece: 0));
            first.AddTag<ConstructionHidden>();
            Entity second = Context.Entities.CreateEntity(new Block(
                new Vector2(710, 600),
                new Vector2(90, 50),
                shapeId: 3,
                shapePiece: 1));
            second.AddTag<ConstructionHidden>();

            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(450, 650),
                target: KeybindAction.L1,
                kind: NoteKind.Construction,
                shapeId: 3));
            Context.Entities.CreateEntity(new Note(
                0,
                new Vector2(550, 700),
                target: KeybindAction.L2,
                kind: NoteKind.ConstructionStep,
                shapeId: 3,
                shapePiece: 0));
            Context.Entities.CreateEntity(new Note(
                1_000,
                new Vector2(650, 700),
                target: KeybindAction.L3,
                kind: NoteKind.ConstructionStep,
                shapeId: 3,
                shapePiece: 1));
        });
        AddUntilStep(
            "all twelve notes exist",
            () => Context.Entities.Query<Note>().Count == 12);
        AddWaitStep("let the notes settle", 4);
    }
}
