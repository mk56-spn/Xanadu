using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Position = Xanadu.Core.Position;
using Xanadu.Editor;

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
        new UiTreeSceneTest(click: false),
        new UiTreeSceneTest(click: true),
        new SongSelectSceneTest(),
        new EditorWaveformSceneTest(),
        new SettingsSceneTest(),
        new OffsetWizardSceneTest(),
        new PlayCameraSceneTest(),
        new ResultsSceneTest(),
        new RestartSceneTest(),
        new EditorHelpSceneTest(),
        new EditorViewSceneTest(),
        new NoteGlowSceneTest(),
        new PlaySkipSceneTest(),
        new EditorExitMenuSceneTest(),
        new PauseSceneTest(),
    };
}
