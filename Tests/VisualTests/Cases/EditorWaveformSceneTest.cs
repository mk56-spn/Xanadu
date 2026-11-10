using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.VisualTests;

public sealed class EditorWaveformSceneTest : SceneTest
{
    public override string Name => "Editor waveform";
    public override string Slug => "editor-waveform";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  VisualAssert.HasText(Context.Entities, "LEVEL EDITOR"));
        AddAssert(
            "editor binding help exists",
            () => EditorPanel.Find(Context.Entities, EditorPanelKind.Bindings) is { IsNull: false } help &&
                  UiLayout.IsHidden(help));

        AddStep("select the test track", () =>
        {
            ref LevelInfo level = ref Context.Entities
                .Query<LevelInfo>()
                .ToEntityList()[0]
                .GetComponent<LevelInfo>();
            level.Song = TestContent.Song("waveform", "Waveform");
        });
        AddUntilStep(
            "wait for waveform",
            () =>
            {
                Entity editor = Context.Entities.Query<LevelInfo>().ToEntityList()[0];
                return editor.GetComponent<WaveformPreview>().Data is { DurationMs: > 0 } &&
                       editor.GetComponent<Playhead>().DurationMs > 0;
            });

        AddStep("place notes, one time stacked three deep", () =>
        {
            foreach (long time in new long[] { 1_000, 2_000, 3_000, 4_000 })
                Context.Entities.CreateEntity(new Note(time, new Vector2(300, 300)));
            for (int i = 0; i < 2; i++)
                Context.Entities.CreateEntity(new Note(3_000, new Vector2(400 + i * 100, 300)));
        });

        AddAssert(
            "waveform has levels",
            () => Context.Entities.Query<WaveformPreview>().ToEntityList()[0]
                .GetComponent<WaveformPreview>().Data!.Steps > 0);
    }
}
