using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Xanadu.Systems.Audio;

namespace Xanadu.Tests.Audio;

[TestFixture]
public sealed class Mp3LeadInTests
{
    /// <summary>The first frame of a 44.1 kHz stereo MP3, with its tag where the format puts it.</summary>
    private static byte[] Frame(string tag, int lameDelay = -1, byte flags = 0x0F)
    {
        var bytes = new byte[1024];
        bytes[0] = 0xFF;
        bytes[1] = 0xFB; // MPEG 1, layer 3, no CRC
        bytes[2] = 0x90; // 128 kbps, 44.1 kHz
        bytes[3] = 0x00; // stereo
        int at = 4 + 32;
        Encoding.ASCII.GetBytes(tag).CopyTo(bytes, at);
        bytes[at + 7] = flags;
        int encoder = at + 8 + 4 + 4 + 100 + 4;
        if (lameDelay >= 0)
        {
            Encoding.ASCII.GetBytes("LAME3.100").CopyTo(bytes, encoder);
            bytes[encoder + 21] = (byte)(lameDelay >> 4);
            bytes[encoder + 22] = (byte)((lameDelay & 0xF) << 4);
        }

        return bytes;
    }

    [TestCase("Info")]
    [TestCase("Xing")]
    public void AHeaderFramePlusTheEncoderDelayIsTheLeadIn(string tag) =>
        Assert.That(
            Mp3LeadIn.Seconds(Frame(tag, lameDelay: 576)),
            Is.EqualTo((1152 + 576 + 529) / 44100d).Within(1e-9));

    [Test]
    public void AHeaderWithoutAnEncoderTagIsOnlyItsFrame() =>
        Assert.That(Mp3LeadIn.Seconds(Frame("Info")), Is.EqualTo(1152 / 44100d).Within(1e-9));

    [Test]
    public void AFileWithNoHeaderFrameHasNoLeadIn() =>
        Assert.That(Mp3LeadIn.Seconds(Frame("\0\0\0\0")), Is.Zero);

    [Test]
    public void AnId3TagIsSkippedToFindTheFrame()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp3");
        try
        {
            var id3 = new byte[10 + 300];
            Encoding.ASCII.GetBytes("ID3").CopyTo(id3, 0);
            id3[8] = 2; // 2 << 7 = 256
            id3[9] = 44;
            using (FileStream file = File.Create(path))
            {
                file.Write(id3);
                file.Write(Frame("Info", lameDelay: 576));
            }

            Assert.That(Mp3LeadIn.Seconds(path), Is.EqualTo(2257 / 44100d).Within(1e-9));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AnythingElseHasNoLeadIn()
    {
        Assert.That(Mp3LeadIn.Seconds("/no/such/song.mp3"), Is.Zero);
        Assert.That(Mp3LeadIn.Seconds("/no/such/song.wav"), Is.Zero);
        Assert.That(Mp3LeadIn.Seconds(new byte[64]), Is.Zero);
    }
}
