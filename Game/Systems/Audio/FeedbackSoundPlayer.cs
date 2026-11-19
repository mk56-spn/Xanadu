using System;
using System.Runtime.InteropServices;
using System.Text;
using Miniaudio;
using Xanadu.Core.Diagnostics;
using Xanadu.Core.Storage;

namespace Xanadu.Systems.Audio;

/// <summary>The feedback sounds: the menus' under <c>Assets/Audio/Ui</c>, a note hit's under <c>Assets/Audio/Gameplay</c>.</summary>
public enum FeedbackSound
{
    Move,
    Select,
    Back,
    Error,
    SongSwitch,
    Hit,
}

/// <summary>
/// Plays <see cref="FeedbackSound"/>s on its own miniaudio engine. Each sound is decoded
/// once, on first use, into a few voices taken in turn, so a quick run of the
/// same sound overlaps instead of cutting itself off.
/// </summary>
public sealed unsafe class FeedbackSoundPlayer : IDisposable
{
    private const int Voices = 4;
    private const float Volume = 0.6f;

    private static readonly string[] Files =
    [
        "Assets/Audio/Ui/menu-cursor-move.flac",
        "Assets/Audio/Ui/menu-select.flac",
        "Assets/Audio/Ui/menu-back.flac",
        "Assets/Audio/Ui/menu-error.flac",
        "Assets/Audio/Ui/song-switch.flac",
        "Assets/Audio/Gameplay/hit.flac",
    ];

    private ma_engine* _engine;
    private bool _engineFailed;
    private bool _disposed;
    private readonly ma_sound*[][] _voices = new ma_sound*[Files.Length][];
    private readonly bool[] _failed = new bool[Files.Length];
    private readonly int[] _next = new int[Files.Length];

    public void Play(FeedbackSound sound)
    {
        int index = (int)sound;
        if (!EnsureEngine() || !EnsureLoaded(index))
            return;

        ma_sound* voice = _voices[index][_next[index]];
        _next[index] = (_next[index] + 1) % Voices;
        ma.sound_seek_to_pcm_frame(voice, 0);
        ma.sound_start(voice);
    }

    private bool EnsureLoaded(int index)
    {
        if (_voices[index] is not null)
            return true;
        if (_failed[index])
            return false;

        byte[] path = Encoding.UTF8.GetBytes(GamePaths.Full(Files[index]) + '\0');
        var voices = new ma_sound*[Voices];
        for (int i = 0; i < Voices; i++)
        {
            var voice = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));
            ma_result result;
            fixed (byte* pathPtr = path)
                result = ma.sound_init_from_file(_engine, (sbyte*)pathPtr, (uint)ma_sound_flags.MA_SOUND_FLAG_DECODE, null, null, voice);
            if (result != ma_result.MA_SUCCESS)
            {
                GameLog.Error($"miniaudio failed to load feedback sound '{Files[index]}': {result}");
                NativeMemory.Free(voice);
                for (int j = 0; j < i; j++)
                    Free(voices[j]);
                _failed[index] = true;
                return false;
            }

            ma.sound_set_volume(voice, Volume);
            voices[i] = voice;
        }

        _voices[index] = voices;
        return true;
    }

    private bool EnsureEngine()
    {
        if (_engine != null)
            return true;
        if (_disposed || _engineFailed)
            return false;

        var engine = (ma_engine*)NativeMemory.Alloc((nuint)sizeof(ma_engine));
        ma_result result = ma.engine_init(null, engine);
        if (result != ma_result.MA_SUCCESS)
        {
            GameLog.Error($"miniaudio engine init failed for feedback sounds: {result}");
            NativeMemory.Free(engine);
            _engineFailed = true;
            return false;
        }

        _engine = engine;
        return true;
    }

    private static void Free(ma_sound* voice)
    {
        ma.sound_uninit(voice);
        NativeMemory.Free(voice);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (ma_sound*[]? voices in _voices)
        {
            if (voices is null)
                continue;
            foreach (ma_sound* voice in voices)
                Free(voice);
        }

        if (_engine != null)
        {
            ma.engine_uninit(_engine);
            NativeMemory.Free(_engine);
            _engine = null;
        }
    }
}
