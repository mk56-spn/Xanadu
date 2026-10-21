using System;
using System.Runtime.InteropServices;
using System.Text;
using Godot;
using Miniaudio;

namespace Writer.Systems.Audio;

/// <summary>Miniaudio-backed track player with lazy device initialization.</summary>
public sealed unsafe class MiniaudioTrackAudio : ITrackAudio
{
    private ma_engine* _engine;
    private ma_sound* _sound;
    private bool _soundInitialized;
    private bool _disposed;

    public bool Play(string audioPath, float volume)
    {
        if (!EnsureEngine())
            return false;

        Stop();

        if (_sound == null)
            _sound = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));

        string path = audioPath.StartsWith("res://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(audioPath)
            : audioPath;

        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        fixed (byte* pathPtr = pathBytes)
        {
            ma_result result = ma.sound_init_from_file(
                _engine,
                (sbyte*)pathPtr,
                0,
                null,
                null,
                _sound);

            if (result != ma_result.MA_SUCCESS)
            {
                GD.PushError($"miniaudio failed to load '{path}': {result}");
                return false;
            }
        }

        _soundInitialized = true;
        ma.sound_set_volume(_sound, volume);

        ma_result startResult = ma.sound_start(_sound);
        if (startResult != ma_result.MA_SUCCESS)
        {
            GD.PushError($"miniaudio failed to start '{path}': {startResult}");
            ma.sound_uninit(_sound);
            _soundInitialized = false;
            return false;
        }

        return true;
    }

    public void Stop()
    {
        if (!_soundInitialized)
            return;

        ma.sound_stop(_sound);
        ma.sound_uninit(_sound);
        _soundInitialized = false;
    }

    public bool IsPlaying =>
        _soundInitialized && ma.sound_is_playing(_sound) != 0;

    public float PositionSeconds
    {
        get
        {
            if (!_soundInitialized)
                return 0f;

            float cursor;
            return ma.sound_get_cursor_in_seconds(_sound, &cursor) == ma_result.MA_SUCCESS
                ? cursor
                : 0f;
        }
    }

    public float LengthSeconds
    {
        get
        {
            if (!_soundInitialized)
                return 0f;

            float length;
            return ma.sound_get_length_in_seconds(_sound, &length) == ma_result.MA_SUCCESS
                ? length
                : 0f;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();

        if (_sound != null)
        {
            NativeMemory.Free(_sound);
            _sound = null;
        }

        if (_engine != null)
        {
            ma.engine_uninit(_engine);
            NativeMemory.Free(_engine);
            _engine = null;
        }
    }

    private bool EnsureEngine()
    {
        if (_disposed)
            return false;

        if (_engine != null)
            return true;

        var engine = (ma_engine*)NativeMemory.Alloc((nuint)sizeof(ma_engine));
        ma_result result = ma.engine_init(null, engine);
        if (result != ma_result.MA_SUCCESS)
        {
            GD.PushError($"miniaudio engine init failed: {result}");
            NativeMemory.Free(engine);
            return false;
        }

        _engine = engine;
        return true;
    }
}
