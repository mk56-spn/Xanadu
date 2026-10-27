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
        new EditorDirectionWheelSceneTest(),
        new EditorGroupsSceneTest(),
        new EditorFocusSceneTest(),
        new PlayGroupsSceneTest(),
        new LeaveEditorSceneTest(),
    };
}
