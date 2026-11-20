using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Logic.Audio;
using Position = Xanadu.Core.Position;
using SceneState = Xanadu.Model.Ecs.Components.SceneState;

namespace Xanadu.Logic.Bench;

/// <summary>
/// Drives the gameplay bench: plays a library song from the top (the one picked
/// on song select, else the first), loops it, steps through the playable songs
/// on the bench's buttons, and pulses the background on the song's own beats,
/// tempo changes included. A song with an editor map that has curve notes
/// plays that map (BenchSystem.Map.cs). Otherwise, on every other beat a vocal
/// note scrolls past the running box; Space (pink) or J (orange) on its beat
/// sets its height.
/// </summary>
public sealed partial class BenchSystem : QuerySystem<BenchView>
{
    /// <summary>How far the background lifts toward <see cref="Palette.SurfaceAlt"/> on the beat.</summary>
    private const float PulseLift = 0.35f;

    /// <summary>How fast the lift fades after the beat (per second, exponential).</summary>
    private const float PulseDecay = 6f;

    private const float Volume = 0.8f;

    /// <summary>The fixed run speed, pixels a second: the notes scroll past the box at it.</summary>
    private const float RunSpeed = 650f;

    /// <summary>The box's lean into its run.</summary>
    private static readonly float RunTilt = MathF.PI / 180f * 8f;

    // The bench's own keys, fixed for the prototype and apart from the game's
    // keybinds. Two keys hit the vocal notes, and the key picks what the note
    // does: Space is pink (ease to the note's height), J is orange (ease into a climb).
    private const Key PinkKey = Key.Space;
    private const Key OrangeKey = Key.J;

    /// <summary>Switches the vocal ease between tap and hold.</summary>
    private const Key ModeKey = Key.Tab;

    /// <summary>The action keys, in the order of <see cref="BenchView.Quarters"/>.</summary>
    private static readonly Key[] ActionKeys = [Key.S, Key.D, Key.K, Key.L];

    /// <summary>Fall acceleration when no vocal note is holding the box, pixels a second squared.</summary>
    private const float Gravity = 2600f;

    /// <summary>
    /// A hit vocal note eases the box to the note's height from whatever vertical
    /// speed it had: a damped spring, its natural rate per second and its damping
    /// (under 1, so rising into it overshoots a little before it settles).
    /// </summary>
    private const float EaseRate = 7f;
    private const float EaseDamping = 0.7f;

    /// <summary>The climb an orange hit eases into, pixels a second upward.</summary>
    private const float ClimbSpeed = 1300f;

    /// <summary>How quickly the vertical speed eases to the climb (per second, exponential).</summary>
    private const float ClimbEaseRate = 22f;

    /// <summary>On a tap, how long the orange climb lasts before gravity takes the arc, seconds.</summary>
    private const float ClimbSeconds = 0.15f;

    /// <summary>A vocal note is on every this many beats.</summary>
    private const int NoteEvery = 2;

    /// <summary>How far off the beat a Space press still hits, either side, ms.</summary>
    private const double HitWindowMs = 150;

    /// <summary>The lowest and highest a vocal note sits, box centre over the box's standing height.</summary>
    private const float NoteLow = 30f;
    private const float NoteHigh = 380f;

    private readonly EntityStore _entities;
    private readonly ITrackAudio _audio;
    private readonly IKeyboardState? _keyboard;
    private readonly ButtonPress _presses = new();
    private IReadOnlyList<LoadedSong>? _songs;
    private int _index;
    private TempoMap _tempo = TempoMap.Default;
    private bool _playing;
    private long _shownBeat = long.MinValue;
    private float _x = float.NaN;
    private float _y;
    private float _fallSpeed;
    private float _tilt;

    /// <summary>What the last hit vocal note is doing to the box's height, if anything.</summary>
    private Vocal _vocal;

    /// <summary>The height a pink hit is easing to.</summary>
    private float _target;

    /// <summary>How long an orange climb has run, seconds.</summary>
    private float _climbTime;

    /// <summary>The beat of the note that set <see cref="_target"/>.</summary>
    private long _targetBeat;

    /// <summary>Hold: the ease lasts only while Space is held. Tap: it lasts until a note goes by unhit.</summary>
    private bool _hold;
    private bool _wasPinkHeld;
    private bool _wasOrangeHeld;
    private bool _wasModeHeld;
    private double _lastTimeMs;
    private readonly Dictionary<long, Vocal> _hit = new();
    private readonly HashSet<long> _missed = new();

    public BenchSystem(
        EntityStore entities,
        ITrackAudio audio,
        IKeyboardState? keyboard = null)
    {
        _entities = entities;
        _audio = audio;
        _keyboard = keyboard;
    }

    protected override void OnUpdate()
    {
        EntityList views = Query.ToEntityList();

        // Leaving by any route must not leave the song playing.
        if (views.Count == 0)
        {
            if (_playing)
                _audio.Stop();
            _playing = false;
            _songs = null;
            _levels = null;
            _map = null;
            _surfaceAt = null;
            _x = float.NaN;
            return;
        }

        BenchView view = views[0].GetComponent<BenchView>();
        if (_songs is null)
            Open(view);

        double timeMs = _playing ? _audio.PositionSeconds * 1000d : double.NaN;
        if (_map is { } map)
        {
            RunMap(view, map, timeMs, Tick.deltaTime);
        }
        else
        {
            PutSurface(view, away: false);
            HideCurves(view);
            Run(view, timeMs, Tick.deltaTime);
            ShowNotes(view, timeMs);
        }

        foreach ((Entity entity, BenchAction action, _) in UiCommand.All<BenchAction>(_entities))
        {
            if (_presses.Started(entity))
                Step(view, action == BenchAction.Next ? 1 : -1);
        }

        if (!_playing)
            return;

        if (!_audio.IsPlaying)
        {
            _audio.Seek(0);
            _audio.Resume();
        }

        Pulse(view, _audio.PositionSeconds * 1000d);
    }

    /// <summary>What a hit vocal note is doing to the box's height.</summary>
    private enum Vocal
    {
        None,

        /// <summary>Easing to the note's height, from whatever vertical speed the box had.</summary>
        Pink,

        /// <summary>Easing into a climb; when it lets go, gravity turns it into an arc.</summary>
        Orange,
    }

    /// <summary>
    /// Runs the box at <see cref="RunSpeed"/> (the notes scroll past it) and
    /// settles its height. On a vocal note's beat, Space (pink) eases it to that
    /// note's height from whatever vertical speed it had, and J (orange) eases it
    /// into a climb that gravity then bends into an arc. With nothing holding it,
    /// it falls, keeping the speed it left with. Tab switches how long a hit
    /// lasts: tap, pink until a later note goes by unhit and orange for a short
    /// climb; hold, only while the key that hit it is held.
    /// </summary>
    private void Run(BenchView view, double timeMs, float delta)
    {
        if (view.Player.IsNull || !view.Player.TryGetComponent(out Position position))
            return;

        UiStage stage = UiStage.Full;
        float ground = stage.Height * BenchLayout.SurfaceAt - BenchLayout.PlayerSize.Y / 2;
        if (float.IsNaN(_x))
        {
            _x = stage.Width * BenchLayout.PlayerAt;
            _y = ground - BenchLayout.HoverHeight + BenchLayout.PlayerSize.Y / 2;
            _fallSpeed = 0;
            _vocal = Vocal.None;
            _tilt = 0;
            ForgetNotes();
        }

        bool modeHeld = Held(ModeKey);
        if (modeHeld && !_wasModeHeld)
        {
            _hold = !_hold;
            _shownBeat = long.MinValue;
        }
        _wasModeHeld = modeHeld;

        bool pinkHeld = Held(PinkKey);
        bool orangeHeld = Held(OrangeKey);
        if (!double.IsNaN(timeMs))
        {
            if (pinkHeld && !_wasPinkHeld)
                TryHit(timeMs, ground, Vocal.Pink);
            if (orangeHeld && !_wasOrangeHeld)
                TryHit(timeMs, ground, Vocal.Orange);
        }
        _wasPinkHeld = pinkHeld;
        _wasOrangeHeld = orangeHeld;

        if (_hold && ((_vocal == Vocal.Pink && !pinkHeld) || (_vocal == Vocal.Orange && !orangeHeld)))
            _vocal = Vocal.None;

        if (_vocal == Vocal.Orange)
        {
            _climbTime += delta;
            if (!_hold && _climbTime >= ClimbSeconds)
                _vocal = Vocal.None;
        }

        float accel = _vocal switch
        {
            Vocal.Pink => EaseRate * EaseRate * (_target - _y) - 2f * EaseDamping * EaseRate * _fallSpeed,
            Vocal.Orange => (-ClimbSpeed - _fallSpeed) * ClimbEaseRate,
            _ => Gravity,
        };
        _fallSpeed += accel * delta;
        _y += _fallSpeed * delta;
        if (_y > ground)
        {
            _y = ground;
            _fallSpeed = MathF.Min(_fallSpeed, 0f);
        }

        float ceiling = BenchLayout.PlayerSize.Y / 2;
        if (_y < ceiling)
        {
            _y = ceiling;
            _fallSpeed = MathF.Max(_fallSpeed, 0f);
        }

        _tilt += (RunTilt - _tilt) * (1f - MathF.Exp(-9f * delta));
        position.Value = new Vector2(_x, _y);
        ShowActions(view, position.Value);
        view.Player.AddComponent(position);
        view.Player.AddComponent(new UiRotation(_tilt));
        view.Player.AddTag<Moved>();
    }

    /// <summary>
    /// Hits the open vocal note nearest the press, if one is in the window: pink
    /// eases to its height, orange eases into a climb.
    /// </summary>
    private void TryHit(double timeMs, float ground, Vocal vocal)
    {
        double beat = _tempo.BeatAt(timeMs);
        long first = (long)Math.Floor(beat / NoteEvery) * NoteEvery;
        long best = long.MinValue;
        double bestError = HitWindowMs;
        for (long candidate = first; candidate <= first + NoteEvery; candidate += NoteEvery)
        {
            if (candidate < 0 || _hit.ContainsKey(candidate) || _missed.Contains(candidate))
                continue;

            double error = Math.Abs(_tempo.TimeAt(candidate) - timeMs);
            if (error <= bestError)
            {
                bestError = error;
                best = candidate;
            }
        }

        if (best == long.MinValue)
            return;

        _hit[best] = vocal;
        _vocal = vocal;
        _target = NoteY(best, ground);
        _climbTime = 0;
        _targetBeat = best;
    }

    /// <summary>
    /// Places the vocal notes on the beats in view, coloured by whether they were
    /// hit or missed. A note that goes by unhit ends a tap's hold.
    /// </summary>
    private void ShowNotes(BenchView view, double timeMs)
    {
        if (view.Notes is null)
            return;

        UiStage stage = UiStage.Full;
        float ground = stage.Height * BenchLayout.SurfaceAt - BenchLayout.PlayerSize.Y / 2;
        int used = 0;
        if (!double.IsNaN(timeMs))
        {
            // The song looped or changed: start the notes over.
            if (timeMs < _lastTimeMs - 500)
                ForgetNotes();
            _lastTimeMs = timeMs;

            double msPerPixel = 1000d / RunSpeed;
            double from = timeMs - (_x + BenchLayout.NoteRadius) * msPerPixel;
            double to = timeMs + (stage.Width - _x + BenchLayout.NoteRadius) * msPerPixel;
            long beat = Math.Max(0, (long)Math.Ceiling(_tempo.BeatAt(from) / NoteEvery) * NoteEvery);
            for (; used < view.Notes.Length; beat += NoteEvery)
            {
                double noteMs = _tempo.TimeAt(beat);
                if (noteMs > to)
                    break;

                if (!_hit.ContainsKey(beat) && !_missed.Contains(beat) && noteMs < timeMs - HitWindowMs)
                {
                    _missed.Add(beat);
                    if (!_hold && _vocal == Vocal.Pink && beat > _targetBeat)
                        _vocal = Vocal.None;
                }

                Color fill = _hit.TryGetValue(beat, out Vocal hitWith) ? (hitWith == Vocal.Orange ? Palette.Warning : Palette.Pink)
                    : _missed.Contains(beat) ? Palette.Muted
                    : Palette.TextMuted;
                float x = _x + (float)((noteMs - timeMs) / msPerPixel);
                PlaceNote(view.Notes[used++], new Vector2(x, NoteY(beat, ground)), fill);
            }
        }

        for (; used < view.Notes.Length; used++)
            PlaceNote(view.Notes[used], new Vector2(-1000, -1000), Palette.TextMuted);
    }

    private static void PlaceNote(Entity note, Vector2 at, Color fill)
    {
        if (note.IsNull || !note.TryGetComponent(out Position position))
            return;

        position.Value = at;
        note.AddComponent(position);
        note.AddTag<Moved>();

        ref UiShape shape = ref note.GetComponent<UiShape>();
        if (shape.Fill != fill)
        {
            shape.Fill = fill;
            note.AddTag<Dirty>();
        }
    }

    /// <summary>The box-centre height a beat's vocal note sets: a fixed scatter between <see cref="NoteLow"/> and <see cref="NoteHigh"/>.</summary>
    private static float NoteY(long beat, float ground)
    {
        uint hash = (uint)beat * 2654435761u;
        hash ^= hash >> 15;
        float t = (hash & 0xFFFF) / 65535f;
        return ground - (NoteLow + (NoteHigh - NoteLow) * t);
    }

    private void ForgetNotes()
    {
        _hit.Clear();
        _missed.Clear();
        _targetBeat = long.MinValue;
        _lastTimeMs = 0;
    }

    private bool Held(Key key) => _keyboard is not null && _keyboard.IsPressed(key);

    /// <summary>The quarters follow the player (upright, not leaning) and light while their key is held.</summary>
    private void ShowActions(BenchView view, Vector2 center)
    {
        if (view.Quarters is null)
            return;

        for (int i = 0; i < view.Quarters.Length && i < ActionKeys.Length; i++)
        {
            Entity quarter = view.Quarters[i];
            if (quarter.IsNull || !quarter.TryGetComponent(out Position position))
                continue;

            position.Value = center;
            quarter.AddComponent(position);
            quarter.AddTag<Moved>();

            ref UiShape shape = ref quarter.GetComponent<UiShape>();
            Color fill = Held(ActionKeys[i]) ? Palette.Primary : Palette.PrimaryMuted;
            if (shape.Fill != fill)
            {
                shape.Fill = fill;
                quarter.AddTag<Dirty>();
            }
        }
    }

    /// <summary>Gathers the playable songs and starts on the one song select last had.</summary>
    private void Open(BenchView view)
    {
        _songs = SongStore.Shared.Playable();
        string selected = "";
        foreach (Entity entity in _entities.Query<SceneState>().ToEntityList())
            selected = entity.GetComponent<SceneState>().SelectedSongId;

        _index = 0;
        for (int i = 0; i < _songs.Count; i++)
        {
            if (_songs[i].Id == selected)
                _index = i;
        }

        Play(view);
    }

    private void Step(BenchView view, int direction)
    {
        if (_songs is not { Count: > 0 } songs)
            return;

        _index = ((_index + direction) % songs.Count + songs.Count) % songs.Count;
        Play(view);
    }

    private void Play(BenchView view)
    {
        _audio.Stop();
        _playing = false;
        _shownBeat = long.MinValue;
        _vocal = Vocal.None;
        ForgetNotes();
        if (_songs is not { Count: > 0 } songs)
        {
            UiText.Set(view.Title, "No playable songs in the library.");
            UiText.Set(view.Info, "");
            UiText.SetFill(view.Background, Palette.Stage);
            return;
        }

        LoadedSong song = songs[_index];
        _tempo = song.Tempo();
        _map = MapFor(song.Id);
        _x = float.NaN;
        ResetMapRun();
        UiText.Set(view.Title, _map is null
            ? $"{song.Title}  ·  {song.Artist}"
            : $"{song.Title}  ·  {song.Artist}  ·  map: {_map.Name}");
        _playing = _audio.Play(song.AudioFile, Volume);
        if (!_playing)
            UiText.Set(view.Info, "Couldn't play this song's audio.");
    }

    /// <summary>A soft lift on each beat of the song that fades out before the next.</summary>
    private void Pulse(BenchView view, double timeMs)
    {
        double beat = _tempo.BeatAt(timeMs);
        double whole = Math.Floor(beat);
        float sinceBeat = (float)((timeMs - _tempo.TimeAt(whole)) / 1000d);
        float lift = PulseLift * MathF.Exp(-PulseDecay * sinceBeat);
        UiText.SetFill(view.Background, Palette.Stage.Lerp(Palette.SurfaceAlt, lift));

        long shown = (long)whole;
        if (shown == _shownBeat)
            return;

        _shownBeat = shown;
        TempoPoint section = _tempo.At(timeMs);
        int beatsPerBar = Math.Max(1, section.BeatsPerBar);
        long bar = (long)Math.Floor(whole / beatsPerBar);
        long inBar = shown - bar * beatsPerBar;
        UiText.Set(
            view.Info,
            _map is { } map
                ? $"BPM {TempoText.Bpm(section.Bpm)}   ·   bar {bar + 1}  beat {inBar + 1}/{beatsPerBar}   ·   {_index + 1} of {_songs!.Count}   ·   {map.Curves.Count} curves: the body follows them"
                : $"BPM {TempoText.Bpm(section.Bpm)}   ·   bar {bar + 1}  beat {inBar + 1}/{beatsPerBar}   ·   {_index + 1} of {_songs!.Count}   ·   SPACE pink · J orange: {(_hold ? "HOLD" : "TAP")} (Tab)");
    }

    /// <summary>Puts every curve line away, for the beat run.</summary>
    private void HideCurves(BenchView view)
    {
        if (view.Curves is not { Length: > 0 } pool)
            return;
        if (_curveShown.Length != pool.Length)
        {
            _curveShown = new int[pool.Length];
            Array.Fill(_curveShown, -2);
        }

        for (int i = 0; i < pool.Length; i++)
            HideCurve(pool[i], i);
    }
}
