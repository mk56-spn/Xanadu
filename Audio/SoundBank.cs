using System;
using System.Runtime.InteropServices;
using System.Text;
using Miniaudio;
using Xanadu.Core.Diagnostics;
using Xanadu.Core.Storage;

namespace Xanadu.Audio;

/// <summary>
/// Plays a fixed set of short sounds on its own miniaudio engine, by their index
/// in the file list it is made with (game paths). Each sound is decoded
/// once, on first use or on <see cref="Prepare"/>, into a few voices taken in turn, so a quick run of the
/// same sound overlaps instead of cutting itself off.
/// </summary>
public sealed unsafe class SoundBank : IDisposable
{
    private const int Voices = 4;
    private const float Volume = 0.6f;

    private readonly string[] _files;

    private ma_engine* _engine;
    private bool _engineFailed;
    private bool _disposed;
    private readonly ma_sound*[][] _voices;
    private readonly bool[] _failed;
    private readonly int[] _next;

    public SoundBank(string[] files)
    {
        _files = files;
        _voices = new ma_sound*[files.Length][];
        _failed = new bool[files.Length];
        _next = new int[files.Length];
    }

    /// <summary>Plays the sound at <paramref name="index"/> in the file list.</summary>
    public void Play(int index)
    {
        if ((uint)index >= (uint)_files.Length || !EnsureEngine() || !EnsureLoaded(index))
            return;

        ma_sound* voice = _voices[index][_next[index]];
        _next[index] = (_next[index] + 1) % Voices;
        ma.sound_seek_to_pcm_frame(voice, 0);
        ma.sound_start(voice);
    }

    /// <summary>
    /// Starts the engine and decodes every sound now, so the first one played
    /// (a run's first hit) doesn't stall the frame it lands on.
    /// </summary>
    public void Prepare()
    {
        if (!EnsureEngine())
            return;
        for (int i = 0; i < _files.Length; i++)
            EnsureLoaded(i);
    }

    private bool EnsureLoaded(int index)
    {
        if (_voices[index] is not null)
            return true;
        if (_failed[index])
            return false;

        byte[] path = Encoding.UTF8.GetBytes(GamePaths.Full(_files[index]) + '\0');
        var voices = new ma_sound*[Voices];
        for (int i = 0; i < Voices; i++)
        {
            var voice = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));
            ma_result result;
            fixed (byte* pathPtr = path)
                result = ma.sound_init_from_file(_engine, (sbyte*)pathPtr, (uint)ma_sound_flags.MA_SOUND_FLAG_DECODE, null, null, voice);
            if (result != ma_result.MA_SUCCESS)
            {
                GameLog.Error($"miniaudio failed to load sound '{_files[index]}': {result}");
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
            GameLog.Error($"miniaudio engine init failed for sounds: {result}");
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
