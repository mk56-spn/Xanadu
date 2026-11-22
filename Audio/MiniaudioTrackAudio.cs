using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Miniaudio;
using Xanadu.Core.Diagnostics;
using Xanadu.Core.Storage;

namespace Xanadu.Audio;

/// <summary>
/// Miniaudio-backed track player with lazy device initialization.
/// <para>
/// The sound's cursor only moves when the device pulls a chunk (18.75 ms on
/// PipeWire at 48 kHz), so read raw it is a staircase. <see cref="PositionSeconds"/>
/// fills the steps in: the audio thread stamps every chunk (<see cref="OnProcess"/>),
/// and the position is the cursor plus the wall time since that stamp. The
/// device does not take its chunks evenly (PipeWire's gaps run 8–23 ms for
/// 18.75 ms chunks), so a stamp is not the chunk's arrival but where a steady
/// line through the arrivals puts it: each is the last plus one chunk's
/// length, nudged a little toward the real arrival.
/// <see cref="RawPositionSeconds"/> is the staircase itself, for measuring.
/// </para>
/// </summary>
public sealed unsafe class MiniaudioTrackAudio : ITrackAudio
{
    /// <summary>What the audio thread writes on every chunk, read back under a sequence lock.</summary>
    private struct ChunkStamp
    {
        public long Sequence;
        public ma_engine* Engine;
        /// <summary>The engine's clock after the chunk, in frames: matched against a fresh read to know the stamp is current.</summary>
        public ulong EngineFrames;
        /// <summary>When the chunk was due on the steady line, <see cref="Stopwatch"/> ticks.</summary>
        public double Ticks;
        /// <summary>The chunk's length, frames.</summary>
        public ulong ChunkFrames;
        public long Chunks;
    }

    private ma_engine* _engine;
    private ChunkStamp* _stamp;
    private uint _sampleRate;

    // What the last PositionSeconds read saw (game thread only).
    private float _lastRaw = -1f;
    private long _lastChunks = -1;
    private float _lastPosition = -1f;

    /// <summary>A filled position this far behind the last one is a seek, not the line's nudge, ms.</summary>
    private const float SeekBackSeconds = 0.03f;
    private ma_sound* _sound;
    private bool _soundInitialized;
    private bool _disposed;

    /// <summary>The sound the decoder plays before this song's real start; every time we expose has it taken off.</summary>
    private double _leadInSeconds;

    public bool Play(string audioPath, float volume) =>
        Load(audioPath, volume) && Resume();

    public bool Load(string audioPath, float volume)
    {
        if (!EnsureEngine())
            return false;

        Stop();

        if (_sound == null)
            _sound = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));

        string path = GamePaths.Full(audioPath);

        byte[] pathBytes = Encoding.UTF8.GetBytes(path + '\0');
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
                GameLog.Error($"miniaudio failed to load '{path}': {result}");
                return false;
            }
        }

        _soundInitialized = true;
        _leadInSeconds = Mp3LeadIn.Seconds(path);
        ma.sound_seek_to_second(_sound, (float)_leadInSeconds);
        ma.sound_set_volume(_sound, volume);
        return true;
    }

    public bool Resume()
    {
        if (!_soundInitialized)
            return false;

        ma_result result = ma.sound_start(_sound);
        if (result == ma_result.MA_SUCCESS)
            return true;

        GameLog.Error($"miniaudio failed to resume: {result}");
        return false;
    }

    public void Pause()
    {
        if (_soundInitialized)
            ma.sound_stop(_sound);
    }

    public void Seek(float seconds)
    {
        if (_soundInitialized)
            ma.sound_seek_to_second(_sound, MathF.Max(0, seconds) + (float)_leadInSeconds);
    }

    public void SetVolume(float volume)
    {
        if (_soundInitialized)
            ma.sound_set_volume(_sound, Math.Clamp(volume, 0f, 1f));
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

    /// <summary>The cursor as it is: it moves only when the device takes a chunk.</summary>
    public float RawPositionSeconds
    {
        get
        {
            if (!_soundInitialized)
                return 0f;

            float cursor;
            return ma.sound_get_cursor_in_seconds(_sound, &cursor) == ma_result.MA_SUCCESS
                ? MathF.Max(0f, cursor - (float)_leadInSeconds)
                : 0f;
        }
    }

    /// <summary>
    /// The cursor plus the wall time since the device last took a chunk, capped
    /// at one chunk; the raw cursor while stopped, or when no stamp matches it.
    /// </summary>
    public float PositionSeconds
    {
        get
        {
            if (!_soundInitialized)
                return 0f;

            float raw = RawPositionSeconds;
            if (_stamp == null || _sampleRate == 0 || ma.sound_is_playing(_sound) == 0)
            {
                _lastPosition = -1f;
                return raw;
            }

            // The cursor moves partway through mixing a chunk, a few µs before the chunk is
            // stamped: caught in between, wait for the stamp, or the position runs a chunk ahead.
            if (_lastChunks >= 0 && raw != _lastRaw && Volatile.Read(ref _stamp->Chunks) == _lastChunks)
            {
                long until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2000;
                while (Volatile.Read(ref _stamp->Chunks) == _lastChunks && Stopwatch.GetTimestamp() < until)
                    Thread.SpinWait(20);
                raw = RawPositionSeconds;
            }

            float position = Filled(raw);
            _lastRaw = raw;
            _lastChunks = Volatile.Read(ref _stamp->Chunks);
            // The line's nudges are tiny steps back; judging wants a clock that only goes on.
            if (_lastPosition >= 0 && position < _lastPosition && position > _lastPosition - SeekBackSeconds)
                position = _lastPosition;
            _lastPosition = position;
            return position;
        }
    }

    /// <summary>The cursor plus the wall time since its chunk's stamp on the steady line.</summary>
    /// <summary>The cursor plus the wall time since its chunk's stamp on the steady line.</summary>
    private float Filled(float raw)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            long before = Volatile.Read(ref _stamp->Sequence);
            if ((before & 1) != 0)
                continue;
            ulong engineFrames = _stamp->EngineFrames;
            double ticks = _stamp->Ticks;
            ulong chunk = _stamp->ChunkFrames;
            if (Volatile.Read(ref _stamp->Sequence) != before)
                continue;

            // A chunk mixed since the stamp was read: the cursor and stamp disagree, so read again.
            if (ma.engine_get_time_in_pcm_frames(_engine) != engineFrames)
            {
                raw = RawPositionSeconds;
                continue;
            }

            // Behind the line's stamp when the chunk came early, so a little before the cursor is fine;
            // more than two chunks on means the device stalled, and the clock waits for it.
            double since = (Stopwatch.GetTimestamp() - ticks) / Stopwatch.Frequency;
            double length = chunk / (double)_sampleRate;
            return MathF.Max(0f, raw + (float)Math.Clamp(since, -length, 2 * length));
        }

        return raw;
    }

    /// <summary>How many frames the device took last chunk (0 before the first), at <see cref="SampleRate"/>.</summary>
    public ulong ChunkFrames => _stamp == null ? 0 : _stamp->ChunkFrames;

    /// <summary>Chunks the device has taken since the engine started.</summary>
    public long Chunks => _stamp == null ? 0 : Volatile.Read(ref _stamp->Chunks);

    /// <summary>The engine's output rate, Hz (0 before the engine starts).</summary>
    public uint SampleRate => _sampleRate;

    /// <summary>How far a stamp moves toward a chunk's real arrival: small, so uneven arrivals average out.</summary>
    private const double StampPull = 0.05;

    /// <summary>The audio thread, after each chunk is mixed: stamps when, on the steady line, and how far the engine got.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnProcess(void* user, float* frames, ulong frameCount)
    {
        var stamp = (ChunkStamp*)user;
        if (stamp->Engine == null)
            return;

        double arrived = Stopwatch.GetTimestamp();
        ulong engineFrames = ma.engine_get_time_in_pcm_frames(stamp->Engine);
        uint rate = stamp->Engine->sampleRate;
        double length = rate == 0 ? 0 : frameCount * (double)Stopwatch.Frequency / rate;
        double due = stamp->Ticks + length;
        // The first chunk, or after a stall or a stop: start the line on this arrival.
        double ticks = stamp->Chunks == 0 || Math.Abs(arrived - due) > 4 * length
            ? arrived
            : due + StampPull * (arrived - due);
        Volatile.Write(ref stamp->Sequence, stamp->Sequence + 1);
        stamp->EngineFrames = engineFrames;
        stamp->Ticks = ticks;
        stamp->ChunkFrames = frameCount;
        stamp->Chunks++;
        Volatile.Write(ref stamp->Sequence, stamp->Sequence + 1);
    }

    public float LengthSeconds
    {
        get
        {
            if (!_soundInitialized)
                return 0f;

            float length;
            return ma.sound_get_length_in_seconds(_sound, &length) == ma_result.MA_SUCCESS
                ? MathF.Max(0f, length - (float)_leadInSeconds)
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

        if (_stamp != null)
        {
            NativeMemory.Free(_stamp);
            _stamp = null;
        }
    }

    private bool EnsureEngine()
    {
        if (_disposed)
            return false;

        if (_engine != null)
            return true;

        var engine = (ma_engine*)NativeMemory.Alloc((nuint)sizeof(ma_engine));
        var stamp = (ChunkStamp*)NativeMemory.AllocZeroed((nuint)sizeof(ChunkStamp));
        ma_engine_config config = ma.engine_config_init();
        config.onProcess = &OnProcess;
        config.pProcessUserData = stamp;
        ma_result result = ma.engine_init(&config, engine);
        if (result != ma_result.MA_SUCCESS)
        {
            GameLog.Error($"miniaudio engine init failed: {result}");
            NativeMemory.Free(engine);
            NativeMemory.Free(stamp);
            return false;
        }

        _engine = engine;
        _stamp = stamp;
        _sampleRate = ma.engine_get_sample_rate(engine);
        // Set last: the audio thread stamps nothing until the engine is whole.
        Volatile.Write(ref *(nint*)&stamp->Engine, (nint)engine);
        return true;
    }
}
