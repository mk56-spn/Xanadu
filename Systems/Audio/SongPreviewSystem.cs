using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Audio;

/// <summary>
/// Plays the selected level's song on the song-select screen, starting a
/// little way in, and feeds its spectrum to any <see cref="UiBarGraph"/>. The
/// spectrum is analysed ahead on another thread, then read at the playback
/// position, so the bars follow what is actually audible.
/// </summary>
public sealed class SongPreviewSystem : QuerySystem<SongSelectState>
{
    /// <summary>How long a selection must hold still before its song starts.</summary>
    private const float Debounce = 0.3f;

    private const float Volume = 0.5f;

    /// <summary>A new song fades in over this many seconds.</summary>
    private const float FadeInSeconds = 1.2f;

    /// <summary>The old song fades out over this many seconds once the selection moves off it.</summary>
    private const float FadeOutSeconds = 0.25f;
    private const int AnalysisRate = 22050;
    private const int CachedSongs = 4;

    /// <summary>Bars fall at this fraction of full height per second.</summary>
    private const float FallRate = 2.2f;

    private const int EnergyGroups = 4;

    /// <summary>The background takes about a third of a second to hand over to or from the music.</summary>
    private const float ReactiveRate = 3f;

    /// <summary>Broad groups average out quieter than single bands, so lift them.</summary>
    private const float EnergyGain = 1.3f;

    private static readonly Xanadu.Levels.SongStore Songs = new();

    private readonly EntityStore _entities;
    private readonly ITrackAudio _audio;
    private readonly Dictionary<string, SpectrumData> _cache = new();

    private string _wanted = "";
    private string _playing = "";
    private float _stable;
    private double _startSeconds;
    private Task<SpectrumData?>? _analysis;
    private SpectrumData? _spectrum;
    private readonly float[] _bands = new float[SpectrumAnalyzer.DefaultBands];
    private readonly float[] _energy = new float[EnergyGroups];
    private float _reactive;
    private float _fade;

    public SongPreviewSystem(EntityStore entities, ITrackAudio audio)
    {
        _entities = entities;
        _audio = audio;
    }

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;

        string wanted = "";
        Query.ForEachEntity((ref state, _) => wanted = AudioFor(state));

        if (wanted != _wanted)
        {
            _wanted = wanted;
            _stable = 0;
        }
        else
        {
            _stable += delta;
        }

        if (_wanted != _playing && (_wanted.Length == 0 || _stable >= Debounce))
            Switch(_wanted);
        else if (_playing.Length > 0 && !_audio.IsPlaying)
            Restart();

        UpdateFade(delta);
        PollAnalysis();
        UpdateBars(delta);
    }

    /// <summary>Fades the music out as soon as the selection leaves it, and the next song in once it starts.</summary>
    private void UpdateFade(float delta)
    {
        if (_playing.Length == 0)
            return;

        float next = _wanted == _playing
            ? Math.Min(1f, _fade + delta / FadeInSeconds)
            : Math.Max(0f, _fade - delta / FadeOutSeconds);
        if (next == _fade)
            return;

        _fade = next;
        // Squared, so the ramp follows loudness rather than raw amplitude.
        _audio.SetVolume(Volume * _fade * _fade);
    }

    private static string AudioFor(SongSelectState state)
    {
        if (state.Count == 0 || state.Selected < 0 || state.Selected >= state.Catalog.Count)
            return "";

        Xanadu.Levels.LevelEntry entry = state.Catalog[state.Selected];
        if (!entry.IsSongAvailable)
            return "";

        string path = Songs.ResolveAudioPath(entry.Song!);
        return File.Exists(path) ? path : "";
    }

    private void Switch(string path)
    {
        _audio.Stop();
        _playing = path;
        _spectrum = null;
        _analysis = null;
        if (path.Length == 0)
            return;

        if (!_audio.Load(path, Volume))
        {
            _playing = "";
            return;
        }

        // A third of the way in is usually past the intro and into the song.
        float length = _audio.LengthSeconds;
        _startSeconds = length > 20 ? length * 0.33 : 0;
        _audio.Seek((float)_startSeconds);
        _fade = 0f;
        _audio.SetVolume(0f);
        _audio.Resume();

        if (_cache.TryGetValue(path, out SpectrumData? known))
            _spectrum = known;
        else
            _analysis = Task.Run(() => Analyse(path));
    }

    /// <summary>The preview ran out: start it again from the preview point.</summary>
    private void Restart()
    {
        _audio.Seek((float)_startSeconds);
        _audio.Resume();
    }

    private static SpectrumData? Analyse(string path)
    {
        float[]? samples = AudioDecoder.DecodeMono(path, AnalysisRate);
        return samples is null ? null : SpectrumAnalyzer.Analyze(samples, AnalysisRate);
    }

    private void PollAnalysis()
    {
        if (_analysis is not { IsCompleted: true } finished)
            return;

        _analysis = null;
        if (finished.IsFaulted || finished.Result is not { } data || _playing.Length == 0)
            return;

        if (_cache.Count >= CachedSongs)
            _cache.Clear();

        _cache[_playing] = data;
        _spectrum = data;
    }

    private void UpdateBars(float delta)
    {
        // One reading of the music drives the bars and the shader alike.
        bool live = _spectrum is not null && _playing.Length > 0 && _audio.IsPlaying;
        if (live)
            _spectrum!.Sample(_audio.PositionSeconds, _bands);
        else
            Array.Clear(_bands);

        foreach (Entity entity in _entities.Query<UiBarGraph>().ToEntityList())
        {
            ref UiBarGraph graph = ref entity.GetComponent<UiBarGraph>();
            float[] values = graph.Values;

            // Bars jump up with the music and ease back down.
            for (int i = 0; i < values.Length; i++)
            {
                float level = _bands[i * _bands.Length / values.Length];
                values[i] = MathF.Max(level, values[i] - FallRate * delta);
            }
        }

        // Far notes follow the bass, near ones the treble.
        Span<float> groups = stackalloc float[EnergyGroups];
        SpectrumGroups.Average(_bands, groups);
        for (int i = 0; i < EnergyGroups; i++)
            _energy[i] = MathF.Max(groups[i] * EnergyGain, _energy[i] - FallRate * delta);

        _reactive = Math.Clamp(_reactive + (live ? 1 : -1) * ReactiveRate * delta, 0f, 1f);
        var energy = new Godot.Vector4(_energy[0], _energy[1], _energy[2], _energy[3]);
        foreach (Entity entity in _entities.Query<SpectrumEnergy, UiMaterial>().ToEntityList())
        {
            if (entity.GetComponent<UiMaterial>().Material is Godot.ShaderMaterial material)
            {
                material.SetShaderParameter("energy", energy);
                material.SetShaderParameter("reactive", _reactive);
            }
        }
    }
}
