using System;
using System.IO;
using System.Text;

namespace Xanadu.Systems.Audio;

/// <summary>
/// How much sound the bundled miniaudio 0.11.22 plays before an MP3's real start.
/// It decodes the file's Xing/Info header frame as silent audio and keeps the
/// encoder's delay, where BASS, ffmpeg and miniaudio 0.11.23 onwards leave both
/// out. Taking this off puts a song's times where every other tool has them. It
/// differs per file, so it has to be read from each one. Delete this once the
/// bindings ship a fixed miniaudio.
/// </summary>
public static class Mp3LeadIn
{
    /// <summary>The decoder's own delay, which the LAME tag's figure leaves out.</summary>
    private const int DecoderDelayFrames = 529;

    /// <summary>How far past the ID3 tag to look for the first audio frame.</summary>
    private const int ScanBytes = 8192;

    private static readonly int[] Mpeg1Rates = { 44100, 48000, 32000 };

    /// <summary>The lead-in of a file in seconds, 0 for anything that has none or cannot be read.</summary>
    public static double Seconds(string path)
    {
        if (!path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return 0d;

        try
        {
            using FileStream file = File.OpenRead(path);
            var head = new byte[10];
            int read = file.Read(head, 0, head.Length);
            file.Position = Id3Length(head, read);
            var bytes = new byte[ScanBytes];
            return Seconds(bytes.AsSpan(0, file.Read(bytes, 0, bytes.Length)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0d;
        }
    }

    /// <summary>The lead-in given the bytes from just after any ID3 tag.</summary>
    public static double Seconds(ReadOnlySpan<byte> bytes)
    {
        for (int i = 0; i + 4 <= bytes.Length; i++)
        {
            if (TryReadFrame(bytes[i..], out int sampleRate, out int frameSamples, out int tagOffset))
            {
                return tagOffset + 8 <= bytes.Length - i
                    ? LeadFrames(bytes[(i + tagOffset)..], frameSamples) / (double)sampleRate
                    : 0d;
            }
        }

        return 0d;
    }

    private static long Id3Length(byte[] head, int read)
    {
        if (read < 10 || head[0] != 'I' || head[1] != 'D' || head[2] != '3')
            return 0;

        long body = (head[6] & 0x7F) << 21 | (head[7] & 0x7F) << 14 | (head[8] & 0x7F) << 7 | head[9] & 0x7F;
        bool footer = (head[5] & 0x10) != 0;
        return 10 + body + (footer ? 10 : 0);
    }

    private static int LeadFrames(ReadOnlySpan<byte> tag, int frameSamples)
    {
        string id = Encoding.ASCII.GetString(tag[..4]);
        if (id is not ("Xing" or "Info"))
            return 0;

        int flags = tag[7];
        int encoder = 8 +
                      ((flags & 0x01) != 0 ? 4 : 0) +
                      ((flags & 0x02) != 0 ? 4 : 0) +
                      ((flags & 0x04) != 0 ? 100 : 0) +
                      ((flags & 0x08) != 0 ? 4 : 0);

        // Encoder name, then 12 bytes of quality and filter fields, then the delay in 12 bits.
        const int delayOffset = 21;
        if (encoder + delayOffset + 2 > tag.Length || tag[encoder] == 0)
            return frameSamples;

        int delay = tag[encoder + delayOffset] << 4 | tag[encoder + delayOffset + 1] >> 4;
        return frameSamples + delay + DecoderDelayFrames;
    }

    private static bool TryReadFrame(
        ReadOnlySpan<byte> bytes,
        out int sampleRate,
        out int frameSamples,
        out int tagOffset)
    {
        sampleRate = frameSamples = tagOffset = 0;
        if (bytes[0] != 0xFF || (bytes[1] & 0xE0) != 0xE0)
            return false;

        int version = bytes[1] >> 3 & 3;
        int layer = bytes[1] >> 1 & 3;
        int bitrate = bytes[2] >> 4;
        int rateIndex = bytes[2] >> 2 & 3;
        if (version == 1 || layer != 1 || bitrate is 0 or 15 || rateIndex == 3)
            return false;

        bool mpeg1 = version == 3;
        bool mono = bytes[3] >> 6 == 3;
        bool hasCrc = (bytes[1] & 1) == 0;
        sampleRate = Mpeg1Rates[rateIndex] / (mpeg1 ? 1 : version == 2 ? 2 : 4);
        frameSamples = mpeg1 ? 1152 : 576;
        int sideInfo = mpeg1 ? mono ? 17 : 32 : mono ? 9 : 17;
        tagOffset = 4 + (hasCrc ? 2 : 0) + sideInfo;
        return true;
    }
}
