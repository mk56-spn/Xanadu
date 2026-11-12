using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Miniaudio;
using Xanadu.Storage;

namespace Xanadu.Tools.Animator.Files;

/// <summary>
/// The metronome click: a short, decaying sine tick made in code, so the tool
/// ships no audio file. An attack's beat clicks higher and louder than the beats
/// between. The ticks are written once to <c>user://animator</c> as WAVs and
/// played through miniaudio, as the game plays its songs.
/// </summary>
public sealed unsafe class MiniaudioAnimatorClick : IAnimatorClick, IDisposable
{
    private const int MixRate = 44100;
    private const float Seconds = 0.04f;

    private ma_engine* _engine;
    private byte[]? _accent, _beat;
    private bool _failed;

    public void Click(bool accent)
    {
        if (!Ready())
            return;

        fixed (byte* path = accent ? _accent : _beat)
            ma.engine_play_sound(_engine, (sbyte*)path, null);
    }

    /// <summary>Starts the audio device and writes the ticks on the first click; a failure leaves the tool silent.</summary>
    private bool Ready()
    {
        if (_engine != null)
            return true;
        if (_failed)
            return false;

        try
        {
            string folder = GamePaths.Full("user://animator");
            Directory.CreateDirectory(folder);
            _accent = WriteTick(Path.Combine(folder, "click_accent.wav"), 1760f, 0.5f);
            _beat = WriteTick(Path.Combine(folder, "click_beat.wav"), 1100f, 0.25f);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Metronome click is off: {exception.Message}");
            _failed = true;
            return false;
        }

        var engine = (ma_engine*)NativeMemory.Alloc((nuint)sizeof(ma_engine));
        ma_result result = ma.engine_init(null, engine);
        if (result != ma_result.MA_SUCCESS)
        {
            Console.Error.WriteLine($"Metronome click is off: miniaudio engine init failed ({result})");
            NativeMemory.Free(engine);
            _failed = true;
            return false;
        }

        _engine = engine;
        return true;
    }

    /// <summary>Writes a 16-bit mono tick and returns its path as null-terminated UTF-8 for miniaudio.</summary>
    private static byte[] WriteTick(string path, float frequency, float gain)
    {
        int samples = (int)(MixRate * Seconds);
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + samples * 2);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(MixRate);
            writer.Write(MixRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(samples * 2);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)MixRate;
                float envelope = MathF.Exp(-t * 120f);
                writer.Write((short)(MathF.Sin(MathF.Tau * frequency * t) * envelope * short.MaxValue * 0.8f * gain));
            }
        }

        return Encoding.UTF8.GetBytes(path + "\0");
    }

    public void Dispose()
    {
        if (_engine == null)
            return;

        ma.engine_uninit(_engine);
        NativeMemory.Free(_engine);
        _engine = null;
    }
}
