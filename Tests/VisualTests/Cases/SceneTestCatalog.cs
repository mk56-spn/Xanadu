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
        new ProfileEditSceneTest(),
        new ProfileTintSceneTest(),
        new MainMenuSceneTest(),
        new SongSelectSceneTest(),
        new EditorWaveformSceneTest(),
        new EditorMetadataSceneTest(),
        new SettingsSceneTest(),
        new OffsetWizardSceneTest(),
        new PlaySceneTest(),
        new PlayCameraSceneTest(),
        new NoteKeyCheckingSceneTest(),
        new RainbowNoteShaderSceneTest(),
        new ShaderTunerSceneTest(),
        new MovementJudgementSceneTest(),
        new TimingIndicatorSceneTest(),
        new SpikeShapeSceneTest(),
        new EditorPlayFromHereSceneTest(),
        new EditorPlacementGhostSceneTest(),
        new ResultsSceneTest(),
        new RestartSceneTest(),
        new EditorDropdownSceneTest(),
        new EditorPreviewSaveSceneTest(),
        new EditorDirectionWheelSceneTest(),
        new EditorSelectionOutlineSceneTest(),
        new EditorPreviewPathSceneTest(),
        new CharacterNotesSceneTest(),
        new SplitlineSceneTest(),
        new EditorGroupsSceneTest(),
        new EditorSelfModulateSceneTest(),
        new EditorOneWayBlocksSceneTest(),
        new EditorBlockStylesSceneTest(),
        new EditorSelectionRotationSceneTest(),
        new EditorFocusSceneTest(),
        new EditorViewSceneTest(),
        new PlayGroupsSceneTest(),
        new PlaySkipSceneTest(),
        new LeaveEditorSceneTest(),
        new EditorExitMenuSceneTest(),
        new PauseSceneTest(),
    };
}
