using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Replays;
using Xanadu.Scenes;
using SceneState = Xanadu.Ecs.SceneState;
using Xanadu.Settings;

namespace Xanadu.Systems.Scenes;

/// <summary>Dependencies and payload for building a scene.</summary>
public sealed record SceneBuildContext(
    EntityStore Entities,
    Entity Root,
    Entity StateEntity,
    Vector2 Screen,
    IVisualRidFactory Visuals,
    KeybindSettings Keybinds,
    ProfileStore Profiles,
    DisplaySettings Display,
    Func<List<LevelEntry>> LoadLevels,
    LevelEntry? Entry = null,
    PlayResults? Results = null,
    StartPoint? Start = null,
    Replay? Replay = null,
    bool RecordReplay = false,
    bool CanWatchReplay = false);

/// <summary>A scene-specific builder registered with the scene manager.</summary>
public interface ISceneController
{
    SceneKind Kind { get; }
    void Build(SceneBuildContext context);
    bool TryForward(SceneSystem system) => false;
    SceneKind BackTarget => SceneKind.None;
    bool PausesOnBack => false;
    bool ConfirmsOnBack => false;
    void HandleActions(SceneSystem system, ref SceneState state) { }
}

/// <summary>Scene builders keyed by scene kind.</summary>
public sealed class SceneControllerRegistry
{
    private readonly Dictionary<SceneKind, ISceneController> _controllers = new();

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
        registry.Register(new MainMenuController());
        registry.Register(new ProfilesController());
        registry.Register(new SongSelectController(system));
        registry.Register(new EditorController());
        registry.Register(new PlayController());
        registry.Register(new ResultsController());
        registry.Register(new SettingsController());
        registry.Register(new OffsetWizardController());
        return registry;
    }

    private sealed class SongTimingController : ISceneController
    {
        public SceneKind Kind => SceneKind.SongTiming;
        public SceneKind BackTarget => SceneKind.Editor;

        public void Build(SceneBuildContext c)
        {
            var songs = new SongStore();
            SongDocument? song = songs.Find(
                c.StateEntity.GetComponent<SceneState>().SelectedSongId);
            if (song is null)
            {
                c.StateEntity.GetComponent<SceneState>().Pending = SceneKind.Editor;
                return;
            }

            SongTimingScene.Build(c.Entities, c.Root, c.Screen, song, c.Visuals);
        }

        public void HandleActions(SceneSystem system, ref SceneState state)
        {
            var songs = new SongStore();
            SongDocument? song = songs.Find(state.SelectedSongId);
            if (song is null)
                return;

            bool changed = false;
            foreach (Entity entity in system.Entities
                         .Query<SongTimingAdjust>()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                SongTimingAdjust adjust = entity.GetComponent<SongTimingAdjust>();
                if (adjust.Kind == SongTimingAdjustKind.Bpm)
                    song.Bpm = Math.Clamp(song.Bpm + adjust.Delta, 1, 1000);
                else
                    song.OffsetMs = Math.Clamp(song.OffsetMs + adjust.Delta, -600_000, 600_000);
                changed = true;
                break;
            }

            bool savePressed = false;
            foreach (Entity entity in system.Entities
                         .Query<SongTimingSave>()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                savePressed = true;
                entity.RemoveTag<Pressed>();
                break;
            }

            if (changed || savePressed)
                songs.Save(song);

            Entity view = system.Entities.Query<SongTimingView>()
                .ToEntityList().Count > 0
                ? system.Entities.Query<SongTimingView>().ToEntityList()[0]
                : default;
            if (!view.IsNull)
            {
                SongTimingView timing = view.GetComponent<SongTimingView>();
                SceneUi.SetText(timing.Bpm, $"BPM {song.Bpm}");
                SceneUi.SetText(timing.Offset, $"OFFSET {song.OffsetMs} MS");
            }
        }
    }

    private sealed class MainMenuController : ISceneController
    {
        public SceneKind Kind => SceneKind.Main;
        public void Build(SceneBuildContext c) => MainMenuScene.Build(c.Entities, c.Root, c.Screen, c.Visuals);
        public bool TryForward(SceneSystem system)
        {
            system.Request(SceneKind.SongSelect);
            return true;
        }
    }

    private sealed class ProfilesController : ISceneController
    {
        public SceneKind Kind => SceneKind.Profiles;
        public void Build(SceneBuildContext c) => ProfilesScene.Build(c.Entities, c.Root, c.Screen, c.Profiles, c.Visuals);
        public void HandleActions(SceneSystem system, ref SceneState state)
        {
            foreach (Entity entity in system.Entities
                         .Query<ProfileSelect>()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                if (system.Profiles.Select(entity.GetComponent<ProfileSelect>().ProfileId))
                    state.Pending = SceneKind.Main;
                return;
            }

            foreach (Entity entity in system.Entities
                         .Query<ProfileCreate>()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                system.Profiles.Create();
                state.Pending = SceneKind.Main;
                return;
            }
        }
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
            c.Screen,
            c.Entry!,
            c.Keybinds,
            c.Visuals,
            c.Start,
            c.Replay,
            c.RecordReplay);
        public SceneKind BackTarget => SceneKind.SongSelect;
        public bool PausesOnBack => true;
    }

    private sealed class ResultsController : ISceneController
    {
        public SceneKind Kind => SceneKind.Results;
        public void Build(SceneBuildContext c) => ResultsScene.Build(c.Entities, c.Root, c.Screen, c.Results ?? default, c.Visuals, c.CanWatchReplay);
        public bool TryForward(SceneSystem system)
        {
            system.Request(SceneKind.SongSelect);
            return true;
        }
        public SceneKind BackTarget => SceneKind.SongSelect;
    }

    private sealed class SettingsController : ISceneController
    {
        public SceneKind Kind => SceneKind.Settings;
        public void Build(SceneBuildContext c) => SettingsScene.Build(
            c.Entities,
            c.Root,
            c.Screen,
            c.Keybinds,
            c.Display,
            c.Visuals);

        public SceneKind BackTarget => SceneKind.Main;

        public void HandleActions(SceneSystem system, ref SceneState state)
        {
            foreach (Entity entity in system.Entities
                         .Query<KeybindButton>()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                system.Keybinds.BeginCapture(entity.GetComponent<KeybindButton>().Action);
                return;
            }
        }
    }

    private sealed class OffsetWizardController : ISceneController
    {
        public SceneKind Kind => SceneKind.OffsetWizard;
        public void Build(SceneBuildContext c) => OffsetWizardScene.Build(c.Entities, c.Root, c.Screen, c.Visuals);
        public SceneKind BackTarget => SceneKind.Main;
    }
}
