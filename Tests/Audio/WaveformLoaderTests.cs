using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Xanadu.Systems.Audio;

namespace Xanadu.Tests.Audio;

[TestFixture]
public sealed class WaveformLoaderTests
{
    [Test]
    public void DecodesPcmWavIntoNormalizedPeaks()
    {
        string path = WriteWav(short.MinValue, short.MaxValue);
        try
        {
            WaveformData? data = WaveformLoader.Load(path, 4);

            Assert.That(data, Is.Not.Null);
            Assert.That(data!.Left, Has.Length.EqualTo(2));
            Assert.That(data.DurationMs, Is.EqualTo(2));
            Assert.That(data.Left, Has.Some.GreaterThan(0.5f));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteWav(params short[] samples)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "writer-waveform-" + Guid.NewGuid().ToString("N") + ".wav");

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples.Length * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(1_000);
        writer.Write(2_000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples.Length * 2);
        foreach (short sample in samples)
            writer.Write(sample);

        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }
}
