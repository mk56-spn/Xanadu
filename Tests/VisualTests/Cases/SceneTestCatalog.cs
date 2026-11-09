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
        new PlayLayersSceneTest(),
        new PlayBlendsSceneTest(),
        new PlayPassBlurSceneTest(),
        new PlayPassDistortSceneTest(),
        new PlayBufferedGroupSceneTest(),
        new PlayScreenReadSceneTest(),
        new NoteKeyCheckingSceneTest(),
        new RainbowNoteShaderSceneTest(),
        new MovementJudgementSceneTest(),
        new TimingIndicatorSceneTest(),
        new SpikeShapeSceneTest(),
        new EditorPlayFromHereSceneTest(),
        new EditorPlacementGhostSceneTest(),
        new ResultsSceneTest(),
        new RestartSceneTest(),
        new EditorDropdownSceneTest(),
        new EditorPreviewSaveSceneTest(),
        new EditorSelectionOutlineSceneTest(),
        new EditorPreviewPathSceneTest(),
        new NoteDesignsSceneTest(),
        new DashPopSceneTest(),
        new SpikeShatterSceneTest(),
        new HitGhostSceneTest(),
        new CharacterHitboxSceneTest(faceLeft: false),
        new CharacterHitboxSceneTest(faceLeft: true),
        new PlayfieldOutlinesSceneTest(),
        new EditorGroupsSceneTest(),
        new EditorLayersSceneTest(),
        new EditorBlendChoiceSceneTest(),
        new EditorSelfModulateSceneTest(),
        new EditorOneWayBlocksSceneTest(),
        new EditorBlockStylesSceneTest(),
        new EditorBlockSizesSceneTest(),
        new EditorTextSceneTest(),
        new EditorGrassSceneTest(),
        new EditorFlareSceneTest(),
        new EditorToolGroupsSceneTest(),
        new EditorPolygonSceneTest(),
        new EditorScreenReadSceneTest(),
        new EditorSelectionRotationSceneTest(),
        new EditorHelpSceneTest(),
        new EditorFocusSceneTest(),
        new EditorViewSceneTest(),
        new PlayGroupsSceneTest(),
        new PlayGrassSceneTest(),
        new NoteGlowSceneTest(),
        new PlaySkipSceneTest(),
        new LeaveEditorSceneTest(),
        new EditorExitMenuSceneTest(),
        new PauseSceneTest(),
    };
}
