using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Audio;

/// <summary>
/// The feedback sounds. A note hit (its <see cref="NotePop"/> appearing) hits. A control going <see cref="Pressed"/> selects, the
/// pointer moving onto one moves (not in the editor, where it would never stop),
/// both heard through the store's native <c>OnTagsChanged</c>; the song-select
/// wheel landing on another song switches, unless a click just did it.
/// </summary>
public sealed class FeedbackSoundSystem : QuerySystem<SongSelectState>, IDisposable
{
    /// <summary>A song switch this soon after a select is that select's, so it stays quiet.</summary>
    private const double SelectQuietSeconds = 0.1;

    private readonly EntityStore _store;
    private readonly FeedbackSoundPlayer _sounds;
    private readonly ArchetypeQuery<SceneState> _scene;
    private int _selected = -1;
    private double _now;
    private double _lastSelect = double.NegativeInfinity;

    public FeedbackSoundSystem(EntityStore store, FeedbackSoundPlayer sounds)
    {
        _store = store;
        _sounds = sounds;
        _scene = store.Query<SceneState>();
        store.OnTagsChanged += Heard;
        store.OnComponentAdded += Added;
    }

    protected override void OnUpdate()
    {
        _now += Tick.deltaTime;

        int selected = Query.Each(new SelectedSong { Index = -1 }).Index;
        if (selected >= 0 && _selected >= 0 && selected != _selected &&
            _now - _lastSelect > SelectQuietSeconds)
        {
            _sounds.Play(FeedbackSound.SongSwitch);
        }

        _selected = selected;
    }

    private void Heard(TagsChanged changed)
    {
        Entity entity = changed.Entity;
        if (!IsControl(entity))
            return;

        if (changed.AddedTags.Has<Pressed>())
        {
            _sounds.Play(FeedbackSound.Select);
            _lastSelect = _now;
        }
        else if (changed.AddedTags.Has<Hovered>() && !InEditor())
        {
            _sounds.Play(FeedbackSound.Move);
        }
    }

    private void Added(ComponentChanged changed)
    {
        if (changed.Type == typeof(NotePop))
            _sounds.Play(FeedbackSound.Hit);
    }

    private static bool IsControl(Entity entity) =>
        !entity.IsNull &&
        (entity.HasComponent<ButtonVisual>() ||
         entity.HasComponent<Toggle>() ||
         entity.HasComponent<Dropdown>() ||
         entity.HasComponent<UiCommand>());

    private bool InEditor() => _scene.Each(new CurrentScene()).Editor;

    public void Dispose()
    {
        _store.OnTagsChanged -= Heard;
        _store.OnComponentAdded -= Added;
    }

    private struct SelectedSong : IEach<SongSelectState>
    {
        public int Index;

        public void Execute(ref SongSelectState state) =>
            Index = state.Count > 0 ? state.Selected : -1;
    }

    private struct CurrentScene : IEach<SceneState>
    {
        public bool Editor;

        public void Execute(ref SceneState state) =>
            Editor |= state.Current == SceneKind.Editor;
    }
}
