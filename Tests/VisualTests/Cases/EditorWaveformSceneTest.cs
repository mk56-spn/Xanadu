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
            () => Context.Entities.Query<EditorBindingsHelp>().Count == 1 &&
                  !Context.Entities.Query<EditorBindingsHelp>()
                      .ToEntityList()[0]
                      .GetComponent<EditorBindingsHelp>().Visible);

        AddStep("select the test track", () =>
        {
            ref LevelInfo level = ref Context.Entities
                .Query<LevelInfo>()
                .ToEntityList()[0]
                .GetComponent<LevelInfo>();
            level.AudioPath = "res://Audio/test-track.wav";
        });
        AddUntilStep(
            "wait for waveform",
            () =>
            {
                Entity editor = Context.Entities.Query<LevelInfo>().ToEntityList()[0];
                WaveformPreview preview = editor.GetComponent<WaveformPreview>();
                return preview.Left.Length > 0 &&
                       preview.DurationMs > 0 &&
                       editor.GetComponent<Playhead>().DurationMs > 0;
            });

        AddAssert(
            "waveform has peaks",
            () => Context.Entities.Query<WaveformPreview>().ToEntityList()[0]
                .GetComponent<WaveformPreview>().Left.Length > 0);
    }
}
