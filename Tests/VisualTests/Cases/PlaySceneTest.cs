using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.VisualTests;

public sealed class PlaySceneTest : SceneTest
{
    public override string Name => "Play HUD and field";
    public override string Slug => "play";

    protected override void SetUpSteps()
    {
        AddStep("open synthetic play scene", OpenPlay);

        AddUntilStep(
            "wait for play HUD",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.Play,
                UiLayers.PlayHud) &&
                VisualAssert.HasText(Context.Entities, "Visual Play Test"));
        AddAssert(
            "level title exists",
            () => VisualAssert.HasText(Context.Entities, "Visual Play Test"));
        AddAssert(
            "score HUD exists",
            () => VisualAssert.HasText(Context.Entities, "SCORE 0000000"));
        AddAssert(
            "combo HUD exists",
            () => VisualAssert.HasText(Context.Entities, "COMBO"));
        AddAssert(
            "PAUSE button is animated",
            () => VisualAssert.HasAnimatedText(Context.Entities, "PAUSE"));
        AddAssert(
            "play HUD layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                UiLayers.PlayHud) == 0);
        AddAssert(
            "player exists",
            () => Context.Entities.Query<Player>().Count == 1);
        AddAssert(
            "judgement counter exists",
            () => VisualAssert.HasText(Context.Entities, "JUDGEMENTS") &&
                  VisualAssert.HasText(Context.Entities, "FLAWLESS+"));
        AddAssert(
            "key trail exists",
            () => Context.Entities.Query<KeyTrail>().Count == 1);
        AddAssert(
            "timing error bar exists",
            () => Context.Entities.Query<ErrorBar>().Count == 1);
        AddAssert(
            "HUD layout exists",
            () => Context.Entities.Query<HudLayout>().Count == 1);
        AddAssert(
            "contact counters exist",
            () => Context.Entities.Query<ContactCounterHud>().Count == 1 &&
                  VisualAssert.HasText(Context.Entities, $"0/{ContactCounters.Cap}"));
        AddAssert(
            "key trail fills its drag box",
            KeyTrailMatchesDragBox);
    }

    /// <summary>The key trail's frame covers exactly the box the HUD editor drags.</summary>
    private bool KeyTrailMatchesDragBox()
    {
        Entity playfield = Context.Entities.Query<HudLayout>().ToEntityList()[0];
        HudItemLayout item = playfield.GetComponent<HudLayout>().Items.First(item => item.Id == "key_trail");
        Entity frame = item.Members[0];
        Rect2 local = frame.GetComponent<Rectangle>().Bounds;
        var drawn = new Rect2(frame.GetComponent<Position>().Value + local.Position, local.Size);
        var dragged = new Rect2(item.Bounds.Position + item.Offset, item.Bounds.Size);
        return drawn.Position.DistanceTo(dragged.Position) < 0.5f &&
               drawn.Size.DistanceTo(dragged.Size) < 0.5f;
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-play", "Visual Play");
        LevelDocument document = TestContent.Level("Visual Play Test", song);

        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 260, Y = 620 });
        document.Notes.Add(new NoteDocument { TimeMs = 1_000, X = 620, Y = 520 });
        document.Notes.Add(new NoteDocument { TimeMs = 2_000, X = 460, Y = 700 });

        Context.Scenes.StartPlay(
            new LevelEntry("visual://play", document, song));
    }
}
