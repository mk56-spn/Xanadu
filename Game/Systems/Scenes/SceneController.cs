using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Replays;
using Xanadu.Scenes;
using SceneState = Xanadu.Ecs.SceneState;
using Xanadu.Settings;
using Xanadu.Ui.Ecs.Ui;

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
    SceneBackgroundKind Background => SceneBackgroundKind.None;
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
        registry.Register(new ProfileEditController());
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

            _scroll = 0;
            _focused.Clear();
            SongTimingScene.Build(c.Entities, c.Root, song, c.Visuals);
        }

        private readonly ButtonPress _presses = new();
        private readonly HashSet<int> _focused = new();
        private int _scroll;

        public void HandleActions(SceneSystem system, ref SceneState state)
        {
            SongStore songs = SongStore.Shared;
            SongDocument? song = songs.Find(state.SelectedSongId);
            if (song is null)
                return;

            bool changed = false;
            foreach (Entity entity in system.Entities
                         .Query<SongTimingAdjust>()
                         .WithDisabled()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                SongTimingAdjust adjust = entity.GetComponent<SongTimingAdjust>();
                changed |= adjust.Kind == SongTimingAdjustKind.Bpm
                    ? SongTempoEdits.SetBase(song, bpm: song.Bpm + adjust.Delta)
                    : SongTempoEdits.SetBase(song, offsetMs: song.OffsetMs + adjust.Delta);
                break;
            }

            foreach (Entity entity in system.Entities.Query<SongTempoButton>().ToEntityList())
            {
                if (!_presses.Started(entity))
                    continue;

                SongTempoButton button = entity.GetComponent<SongTempoButton>();
                switch (button.Action)
                {
                    case SongTempoAction.Add:
                        int added = SongTempoEdits.Add(song);
                        if (added >= 0)
                            _scroll = Math.Max(0, added - SongTimingScene.ChangeRows + 1);
                        changed = true;
                        break;
                    case SongTempoAction.Remove:
                        changed |= SongTempoEdits.Remove(song, _scroll + button.Slot);
                        break;
                    case SongTempoAction.ScrollUp:
                        _scroll--;
                        break;
                    case SongTempoAction.ScrollDown:
                        _scroll++;
                        break;
                }
            }

            // Typed values are read before the view refreshes the fields.
            changed |= CommitFields(system.Entities, song);

            bool savePressed = false;
            foreach (Entity entity in system.Entities
                         .Query<SongTimingSave>()
                         .WithDisabled()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                savePressed = true;
                entity.RemoveTag<Pressed>();
                break;
            }

            if (changed || savePressed)
                songs.Save(song);

            _scroll = Math.Clamp(_scroll, 0, Math.Max(0, song.TempoChanges.Count - SongTimingScene.ChangeRows));
            Entity view = system.Entities.Query<SongTimingView>()
                .ToEntityList().Count > 0
                ? system.Entities.Query<SongTimingView>().ToEntityList()[0]
                : default;
            if (!view.IsNull)
                Show(system.Entities, view.GetComponent<SongTimingView>(), song);
        }

        /// <summary>Applies each typed field once it loses focus. True when the song changed.</summary>
        private bool CommitFields(EntityStore entities, SongDocument song)
        {
            bool changed = false;
            foreach (Entity entity in entities.Query<SongTimingField, TextField>().ToEntityList())
            {
                bool focused = entity.Tags.Has<TextFieldFocused>();
                bool was = _focused.Contains(entity.Id);
                if (focused)
                    _focused.Add(entity.Id);
                else
                    _focused.Remove(entity.Id);
                if (!was || focused)
                    continue;

                SongTimingField field = entity.GetComponent<SongTimingField>();
                string text = entity.GetComponent<TextField>().Value;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    continue;

                int index = _scroll + field.Slot;
                changed |= field.Kind switch
                {
                    SongTimingFieldKind.BaseBpm => SongTempoEdits.SetBase(song, bpm: value),
                    SongTimingFieldKind.BaseBar => SongTempoEdits.SetBase(song, beatsPerBar: (int)Math.Round(value)),
                    SongTimingFieldKind.Offset => SongTempoEdits.SetBase(song, offsetMs: (long)Math.Round(value)),
                    SongTimingFieldKind.ChangeAt => SongTempoEdits.Set(song, index, timeMs: value),
                    SongTimingFieldKind.ChangeBpm => SongTempoEdits.Set(song, index, bpm: value),
                    SongTimingFieldKind.ChangeBar => SongTempoEdits.Set(song, index, beatsPerBar: (int)Math.Round(value)),
                    _ => false,
                };
            }

            return changed;
        }

        private void Show(EntityStore entities, SongTimingView timing, SongDocument song)
        {
            SceneUi.SetText(timing.Bpm, $"BPM {TempoText.Bpm(song.Bpm)}");
            SceneUi.SetText(timing.Offset, $"OFFSET {song.OffsetMs} MS");

            TempoMap map = song.Tempo();
            foreach (Entity entity in entities.Query<SongTimingField, TextField>().ToEntityList())
            {
                if (entity.Tags.Has<TextFieldFocused>())
                    continue;

                SongTimingField field = entity.GetComponent<SongTimingField>();
                int index = _scroll + field.Slot;
                TempoChangeDocument? change = index < song.TempoChanges.Count ? song.TempoChanges[index] : null;
                string value = field.Kind switch
                {
                    SongTimingFieldKind.BaseBpm => TempoText.Bpm(song.Bpm),
                    SongTimingFieldKind.BaseBar => song.BeatsPerBar.ToString(CultureInfo.InvariantCulture),
                    SongTimingFieldKind.Offset => song.OffsetMs.ToString(CultureInfo.InvariantCulture),
                    SongTimingFieldKind.ChangeAt => change is null ? "" : change.TimeMs.ToString("0.#", CultureInfo.InvariantCulture),
                    SongTimingFieldKind.ChangeBpm => change is null ? "" : TempoText.Bpm(change.Bpm),
                    SongTimingFieldKind.ChangeBar => change is null ? "" : change.BeatsPerBar.ToString(CultureInfo.InvariantCulture),
                    _ => "",
                };
                ref TextField text = ref entity.GetComponent<TextField>();
                if (text.Value != value)
                {
                    text.Value = value;
                    entity.AddTag<Dirty>();
                }
            }

            UiLayout.SetHidden(timing.Empty, song.TempoChanges.Count > 0);
            for (int slot = 0; slot < (timing.Rows?.Length ?? 0); slot++)
            {
                SongTempoRowUi row = timing.Rows![slot];
                int index = _scroll + slot;
                bool used = index < song.TempoChanges.Count;
                UiLayout.SetHidden(row.Row, !used);
                if (used)
                    SceneUi.SetText(row.Position, SongTempoEdits.Position(map, song.TempoChanges[index].TimeMs));
            }
        }
    }

    private sealed class MainMenuController : ISceneController
    {
        public SceneKind Kind => SceneKind.Main;
        public SceneBackgroundKind Background => SceneBackgroundKind.Menu;
        public void Build(SceneBuildContext c) => MainMenuScene.Build(c.Entities, c.Root, c.Visuals);
        public bool TryForward(SceneSystem system)
        {
            system.Request(SceneKind.SongSelect);
            return true;
        }
    }

    private sealed class ProfilesController : ISceneController
    {
        public SceneKind Kind => SceneKind.Profiles;
        public SceneBackgroundKind Background => SceneBackgroundKind.Menu;
        public void Build(SceneBuildContext c) => ProfilesScene.Build(c.Entities, c.Root, c.Profiles, c.Visuals);
        public void HandleActions(SceneSystem system, ref SceneState state)
        {
            foreach (Entity entity in system.Entities
                         .Query<SceneAction>()
                         .WithDisabled()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                SceneAction action = entity.GetComponent<SceneAction>();
                switch (action.Kind)
                {
                    case SceneActionKind.SelectProfile:
                        if (system.Profiles.Select(action.ProfileId))
                            state.Pending = SceneKind.Main;
                        return;
                    case SceneActionKind.CreateProfile:
                        system.Profiles.Create();
                        state.Pending = SceneKind.ProfileEdit;
                        return;
                    case SceneActionKind.EditProfile:
                        if (system.Profiles.Select(action.ProfileId))
                            state.Pending = SceneKind.ProfileEdit;
                        return;
                }
            }
        }
    }

    private sealed class ProfileEditController : ISceneController
    {
        public SceneKind Kind => SceneKind.ProfileEdit;
        public SceneBackgroundKind Background => SceneBackgroundKind.Menu;
        public SceneKind BackTarget => SceneKind.Profiles;

        public void Build(SceneBuildContext c)
        {
            if (c.Profiles.ActiveProfile is { } profile)
                ProfileEditScene.Build(c.Entities, c.Root, profile, c.Visuals);
            else
                c.StateEntity.GetComponent<SceneState>().Pending = SceneKind.Profiles;
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
            c.Entry!,
            c.Keybinds,
            c.Visuals,
            c.Start,
            c.Replay,
            c.RecordReplay);
        public bool TryForward(SceneSystem system) => system.RequestPlaySkip();
        public SceneKind BackTarget => SceneKind.SongSelect;
        public bool PausesOnBack => true;
    }

    private sealed class ResultsController : ISceneController
    {
        public SceneKind Kind => SceneKind.Results;
        public void Build(SceneBuildContext c) => ResultsScene.Build(c.Entities, c.Root, c.Results ?? default, c.Visuals, c.CanWatchReplay);
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
            c.Keybinds,
            c.Display,
            c.Visuals);

        public SceneKind BackTarget => SceneKind.Main;

        public void HandleActions(SceneSystem system, ref SceneState state)
        {
            foreach (Entity entity in system.Entities
                         .Query<KeybindButton>()
                         .WithDisabled()
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
        public void Build(SceneBuildContext c) => OffsetWizardScene.Build(c.Entities, c.Root, c.Visuals);
        public SceneKind BackTarget => SceneKind.Main;
    }
}
