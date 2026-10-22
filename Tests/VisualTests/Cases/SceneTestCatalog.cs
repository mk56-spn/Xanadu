using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;
using Writer.Editor;
using Writer.Levels;
using Writer.Scenes;
using Writer.Settings;
using Writer.Systems.Playfields;

namespace Writer.Tests.VisualTests;

/// <summary>Structural regression cases for the ECS screens.</summary>
public static class SceneTestCatalog
{
    public static IReadOnlyList<SceneTest> All { get; } = new SceneTest[]
    {
        new ProfilesSceneTest(),
        new MainMenuSceneTest(),
        new SongSelectSceneTest(),
        new EditorWaveformSceneTest(),
        new EditorMetadataSceneTest(),
        new SettingsSceneTest(),
        new OffsetWizardSceneTest(),
        new PlaySceneTest(),
        new MovementJudgementSceneTest(),
        new ResultsSceneTest(),
        new RestartSceneTest(),
        new EditorDropdownSceneTest(),
        new LeaveEditorSceneTest(),
    };
}
