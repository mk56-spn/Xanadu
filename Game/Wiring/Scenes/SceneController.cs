using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Profiles;
using Xanadu.Model.Replays;
using Xanadu.Model.Scoring;
using SceneState = Xanadu.Model.Ecs.Components.SceneState;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Screens.Bench;
using Xanadu.Screens.Editor;
using Xanadu.Screens.MainMenu;
using Xanadu.Screens.Calibration;
using Xanadu.Screens.Play;
using Xanadu.Screens.Profiles;
using Xanadu.Screens.Results;
using Xanadu.Screens.Settings;
using Xanadu.Screens.Shared;
using Xanadu.Screens.SongTiming;

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

            _focused.Clear();
            SongTimingScene.Build(c.Entities, c.Root, song, c.Visuals);
        }

        private readonly ButtonPress _presses = new();
        private readonly HashSet<int> _focused = new();

        public void HandleActions(SceneSystem system, ref SceneState state)
        {
            SongStore songs = SongStore.Shared;
            SongDocument? song = songs.Find(state.SelectedSongId);
            if (song is null)
                return;

            EntityList views = system.Entities.Query<SongTimingView>().ToEntityList();
            if (views.Count == 0)
                return;

            SongTimingView timing = views[0].GetComponent<SongTimingView>();

            // The list stands for the song's changes; its top is which change the first row shows.
            int first = timing.List.GetComponent<UiList>().First;
            bool changed = false;
            foreach ((Entity entity, SongTimingAdjustKind kind, UiCommand adjust) in UiCommand.All<SongTimingAdjustKind>(system.Entities))
            {
                if (!entity.Tags.Has<Pressed>())
                    continue;

                changed |= kind == SongTimingAdjustKind.Bpm
                    ? SongTempoEdits.SetBase(song, bpm: song.Bpm + adjust.Arg)
                    : SongTempoEdits.SetBase(song, offsetMs: song.OffsetMs + adjust.Arg);
                break;
            }

            foreach ((Entity entity, SongTempoAction action, UiCommand button) in UiCommand.All<SongTempoAction>(system.Entities))
            {
                if (action == SongTempoAction.Save || !_presses.Started(entity))
                    continue;

                switch (action)
                {
                    case SongTempoAction.Add:
                        int added = SongTempoEdits.Add(song);
                        if (added >= 0)
                        {
                            UiLists.SetCount(timing.List, song.TempoChanges.Count, keepPlace: true);
                            UiLists.Reveal(timing.List, added);
                        }
                        changed = true;
                        break;
                    case SongTempoAction.Remove:
                        changed |= SongTempoEdits.Remove(song, first + button.Arg);
                        break;
                }
            }

            // Typed values are read before the view refreshes the fields.
            changed |= CommitFields(system.Entities, song, first);

            bool savePressed = false;
            Entity save = UiCommand.Find(system.Entities, SongTempoAction.Save);
            if (!save.IsNull && save.Tags.Has<Pressed>())
            {
                savePressed = true;
                save.RemoveTag<Pressed>();
            }

            if (changed || savePressed)
                songs.Save(song);

            if (timing.List.GetComponent<UiList>().Count != song.TempoChanges.Count)
                UiLists.SetCount(timing.List, song.TempoChanges.Count, keepPlace: true);
            Show(system.Entities, timing, song);
        }

        /// <summary>Applies each typed field once it loses focus. True when the song changed.</summary>
        private bool CommitFields(EntityStore entities, SongDocument song, int first)
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

                int index = first + field.Slot;
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
            UiList list = timing.List.GetComponent<UiList>();
            foreach (Entity entity in entities.Query<SongTimingField, TextField>().ToEntityList())
            {
                if (entity.Tags.Has<TextFieldFocused>())
                    continue;

                SongTimingField field = entity.GetComponent<SongTimingField>();
                int index = list.First + field.Slot;
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

            // The list hides the rows past the last change; the shown ones say where theirs falls.
            UiLayout.SetHidden(timing.Empty, song.TempoChanges.Count > 0);
            for (int slot = 0; slot < list.Shown; slot++)
                SceneUi.SetText(timing.Rows![slot].Position, SongTempoEdits.Position(map, song.TempoChanges[list.First + slot].TimeMs));
            SceneUi.SetText(timing.Foot, list.Shown < list.Count
                ? $"{list.First + 1}–{list.First + list.Shown} of {list.Count} · wheel to scroll"
                : "");
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
