using System;
using Godot;

namespace Xanadu.Tools.Animator.Files;

/// <summary>
/// The metronome click: a short, decaying sine tick made in code, so the tool
/// ships no audio file. An attack's beat clicks higher than the beats between.
/// </summary>
public sealed class GodotAnimatorClick : IAnimatorClick
{
    private const int MixRate = 44100;
    private const float Seconds = 0.04f;

    private readonly AudioStreamPlayer _accent;
    private readonly AudioStreamPlayer _beat;

    public GodotAnimatorClick(Node owner)
    {
        _accent = Player(owner, Tick(1760f), -6f);
        _beat = Player(owner, Tick(1100f), -12f);
    }

    public void Click(bool accent) => (accent ? _accent : _beat).Play();

    private static AudioStreamPlayer Player(Node owner, AudioStreamWav stream, float volumeDb)
    {
        var player = new AudioStreamPlayer { Stream = stream, VolumeDb = volumeDb };
        owner.AddChild(player);
        return player;
    }

    private static AudioStreamWav Tick(float frequency)
    {
        int samples = (int)(MixRate * Seconds);
        byte[] data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)MixRate;
            float envelope = MathF.Exp(-t * 120f);
            short value = (short)(MathF.Sin(MathF.Tau * frequency * t) * envelope * short.MaxValue * 0.8f);
            data[i * 2] = (byte)(value & 0xff);
            data[i * 2 + 1] = (byte)((value >> 8) & 0xff);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = MixRate,
            Stereo = false,
            Data = data,
        };
    }
}
