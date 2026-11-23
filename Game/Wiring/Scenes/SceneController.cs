using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Replays;
using Xanadu.Model.Scoring;
using Xanadu.Screens.Bench;
using Xanadu.Screens.Calibration;
using Xanadu.Screens.Editor;
using Xanadu.Screens.MainMenu;
using Xanadu.Screens.Play;
using Xanadu.Screens.Profiles;
using Xanadu.Screens.Results;
using Xanadu.Screens.Settings;
using Xanadu.Screens.Shared;
using Xanadu.Screens.SongTiming;
using Xanadu.Storage.Levels;
using Xanadu.Storage.Profiles;
using Xanadu.Storage.Settings;
using Xanadu.Ui.Ecs.Ui;
using SceneState = Xanadu.Model.Ecs.Components.SceneState;

namespace Xanadu.Wiring.Scenes;

/// <summary>Dependencies and payload for building a scene.</summary>
public sealed record SceneBuildContext(
    EntityStore Entities,
    Entity Root,
    Entity StateEntity,
    Vector2 Screen,
    IVisualItemFactory Visuals,
    KeybindSettings Keybinds,
    ProfileStore Profiles,
    DisplaySettings Display,
    Func<List<LevelEntry>> LoadLevels,
    LevelEntry? Entry = null,
    PlayResults? Results = null,
    long? StartMs = null,
    Replay? Replay = null,
    bool RecordReplay = false,
    bool CanWatchReplay = false,
    bool Preview = false);

/// <summary>A scene-specific builder registered with the scene manager.</summary>
public interface ISceneController
{
    SceneKind Kind { get; }
    SceneBackgroundKind Background => SceneBackgroundKind.None;
    void Build(SceneBuildContext context);
    bool TryForward(SceneSystem system) => false;
    SceneKind BackTarget => SceneKind.None;
    bool PausesOnBack => false;
    bool ConfirmsOnBack => false;
    void HandleActions(SceneSystem system, ref SceneState state) { }

    /// <summary>The scene is being switched away from (or rebuilt), by any route.</summary>
    void Leave() { }
}

/// <summary>Scene builders keyed by scene kind.</summary>
public sealed class SceneControllerRegistry
{
    private readonly Dictionary<SceneKind, ISceneController> _controllers = [];

    public void Register(ISceneController controller) =>
        _controllers[controller.Kind] = controller;

    public bool Build(SceneKind kind, SceneBuildContext context)
    {
        if (!_controllers.TryGetValue(kind, out ISceneController? controller))
            return false;

        controller.Build(context);
        return true;
    }

    public ISceneController? Get(SceneKind kind) =>
        _controllers.TryGetValue(kind, out ISceneController? controller)
            ? controller
            : null;

    public static SceneControllerRegistry CreateDefault(SceneSystem system)
    {
        var registry = new SceneControllerRegistry();
        registry.Register(new SongTimingController());
        registry.Register(new MainMenuHost());
        registry.Register(new ProfilesHost());
        registry.Register(new ProfileEditHost());
        registry.Register(new SongSelectHost(system));
        registry.Register(new EditorController());
        registry.Register(new PlayController());
        registry.Register(new ResultsHost(system));
        registry.Register(new SettingsHost());
        registry.Register(new BenchController());
        return registry;
    }

    private sealed class SongTimingController : ISceneController
    {
        public SceneKind Kind => SceneKind.SongTiming;
        public SceneKind BackTarget => SceneKind.SongSelect;

        public void Build(SceneBuildContext c)
        {
            SongStore songs = SongStore.Shared;
            SongDocument? song = songs.Find(
                c.StateEntity.GetComponent<SceneState>().SelectedSongId);
            if (song is null)
            {
                c.StateEntity.GetComponent<SceneState>().Pending = SceneKind.SongSelect;
                return;
            }

            _actions.Reset();
            SongTimingScene.Build(c.Entities, c.Root, song, c.Visuals);
        }

        private readonly SongTimingActions _actions = new();

        public void HandleActions(SceneSystem system, ref SceneState state) =>
            _actions.Handle(system.Entities, state.SelectedSongId);
    }

    private sealed class EditorController : ISceneController
    {
        public SceneKind Kind => SceneKind.Editor;
        public void Build(SceneBuildContext c) => EditorScene.Build(c.Entities, c.Screen, c.Root, c.Entry);
        public SceneKind BackTarget => SceneKind.SongSelect;
        public bool ConfirmsOnBack => true;
    }

    private sealed class PlayController : ISceneController
    {
        public SceneKind Kind => SceneKind.Play;
        public void Build(SceneBuildContext c) => PlayScene.Build(
            c.Entities,
            c.Root,
            c.Entry!,
            c.Keybinds,
            c.Visuals,
            c.StartMs,
            c.Replay,
            c.RecordReplay,
            c.Preview);
        public bool TryForward(SceneSystem system) => system.RequestPlaySkip();
        public SceneKind BackTarget => SceneKind.SongSelect;
        public bool PausesOnBack => true;
    }

    private sealed class BenchController : ISceneController
    {
        public SceneKind Kind => SceneKind.Bench;
        public void Build(SceneBuildContext c) => BenchScene.Build(c.Entities, c.Root, c.Visuals);
        public SceneKind BackTarget => SceneKind.Main;
    }
}
