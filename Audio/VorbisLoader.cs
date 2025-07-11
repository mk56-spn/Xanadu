// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Godot;
using NVorbis;
using FileAccess = Godot.FileAccess;

namespace XanaduProject.Audio
{
    public static class VorbisLoader
    {
        public static void DecodeEntireFileIntoMemory(string path, out float[] song, out int sampleRate)
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (f == null)
            {
                GD.PrintErr($"Failed to open OGG file: {path}");
                song = [];
                sampleRate = 0;
                return;
            }

            using var stream = new MemoryStream(f.GetBuffer((long)f.GetLength()));
            using var vorbis = new VorbisReader(stream);

            sampleRate = vorbis.SampleRate;

            // Decode directly into the final array.
            // This avoids intermediate lists and reduces GC pressure.
            float[] songTemp = new float[vorbis.TotalSamples * vorbis.Channels];

            int totalSamplesRead = 0;
            int samplesReadThisCall;
            // Use a span for a safe, efficient view into the array segment we're writing to.
            var writeSpan = songTemp.AsSpan();

            while ((samplesReadThisCall = vorbis.ReadSamples(writeSpan.Slice(totalSamplesRead))) > 0)
                totalSamplesRead += samplesReadThisCall;

            // In the rare case of a metadata mismatch, trim the array to the actual size.
            if (totalSamplesRead < songTemp.Length) Array.Resize(ref songTemp, totalSamplesRead);


            song = songTemp;
        }
    }
}
