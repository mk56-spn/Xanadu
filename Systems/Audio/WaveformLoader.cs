using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Godot;
using Miniaudio;

namespace Writer.Systems.Audio;

/// <summary>Peak data and duration decoded once per audio file.</summary>
public sealed record WaveformData(float[] Peaks, long DurationMs);

/// <summary>Reads small downsampled peaks from PCM WAV files for the editor preview.</summary>
public static class WaveformLoader
{
    private const int Resolution = 512;
    private static readonly Dictionary<string, WaveformData> Cache = new();

    public static WaveformData? Load(string path, int resolution = Resolution)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string filePath = path.StartsWith("res://") || path.StartsWith("user://")
            ? ProjectSettings.GlobalizePath(path)
            : path;
        if (!File.Exists(filePath))
            return null;

        string key = $"{filePath}:{resolution}";
        if (Cache.TryGetValue(key, out WaveformData? cached))
            return cached;

        WaveformData? data = DecodeWav(filePath, resolution) ??
                             DecodeWithMiniaudio(filePath, resolution);
        if (data is not null)
        {
            if (Cache.Count >= 12)
                Cache.Clear();

            Cache[key] = data;
        }

        return data;
    }

    private static WaveformData? DecodeWav(string path, int resolution)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception)
        {
            return null;
        }

        if (bytes.Length < 44 ||
            Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" ||
            Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
        {
            return null;
        }

        int position = 12;
        int format = 0;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        int dataOffset = -1;
        int dataSize = 0;

        while (position + 8 <= bytes.Length)
        {
            string chunkId = Encoding.ASCII.GetString(bytes, position, 4);
            int chunkSize = BitConverter.ToInt32(bytes, position + 4);
            int body = position + 8;

            if (chunkSize < 0 || body + chunkSize > bytes.Length)
                break;

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                format = BitConverter.ToInt16(bytes, body);
                channels = BitConverter.ToInt16(bytes, body + 2);
                sampleRate = BitConverter.ToInt32(bytes, body + 4);
                bitsPerSample = BitConverter.ToInt16(bytes, body + 14);
            }
            else if (chunkId == "data")
            {
                dataOffset = body;
                dataSize = chunkSize;
                break;
            }

            position = body + chunkSize + (chunkSize & 1);
        }

        if (dataOffset < 0 ||
            channels <= 0 ||
            sampleRate <= 0 ||
            (format != 1 && format != 3) ||
            bitsPerSample is not (8 or 16 or 24 or 32))
        {
            return null;
        }

        int bytesPerSample = bitsPerSample / 8;
        int frameSize = channels * bytesPerSample;
        int frames = dataSize / frameSize;
        if (frames <= 0)
            return null;

        int bins = Math.Clamp(resolution, 1, frames);
        var peaks = new float[bins];

        for (int bin = 0; bin < bins; bin++)
        {
            long start = (long)frames * bin / bins;
            long end = (long)frames * (bin + 1) / bins;
            if (end <= start)
                end = start + 1;

            int step = Math.Max(1, (int)((end - start) / 64));
            double sumSquares = 0;
            int sampleCount = 0;

            for (long frame = start; frame < end; frame += step)
            {
                int offset = dataOffset + (int)frame * frameSize;
                for (int channel = 0; channel < channels; channel++)
                {
                    int sampleOffset = offset + channel * bytesPerSample;
                    float sample = ReadSample(
                        bytes,
                        sampleOffset,
                        format,
                        bitsPerSample);
                    sumSquares += sample * sample;
                    sampleCount++;
                }
            }

            peaks[bin] = Math.Clamp(
                (float)Math.Sqrt(sumSquares / Math.Max(1, sampleCount)) * 1.8f,
                0f,
                1f);
        }

        long durationMs = frames * 1000L / sampleRate;
        return new WaveformData(peaks, durationMs);
    }

    private static unsafe WaveformData? DecodeWithMiniaudio(
        string path,
        int resolution)
    {
        ma_decoder* decoder =
            (ma_decoder*)NativeMemory.Alloc((nuint)sizeof(ma_decoder));
        bool initialized = false;
        try
        {
            ma_decoder_config config = ma.decoder_config_init(
                ma_format.ma_format_f32,
                0,
                0);

            ma_result result;
            IntPtr pathPtr = Marshal.StringToHGlobalAnsi(path);
            try
            {
                result = ma.decoder_init_file(
                    (sbyte*)pathPtr,
                    &config,
                    decoder);
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }

            if (result != ma_result.MA_SUCCESS)
                return null;

            initialized = true;
            ma_format format;
            uint channels;
            uint sampleRate;
            result = ma.decoder_get_data_format(
                decoder,
                &format,
                &channels,
                &sampleRate,
                null,
                0);
            if (result != ma_result.MA_SUCCESS ||
                format != ma_format.ma_format_f32 ||
                channels == 0 ||
                sampleRate == 0)
            {
                return null;
            }

            ulong frameCount;
            result = ma.decoder_get_length_in_pcm_frames(
                decoder,
                &frameCount);
            if (result != ma_result.MA_SUCCESS || frameCount == 0)
                return null;

            int bins = (int)Math.Min((ulong)resolution, frameCount);
            var peaks = new float[bins];
            const int chunkFrames = 4096;
            var buffer = new float[chunkFrames * channels];
            ulong totalRead = 0;

            fixed (float* bufferPtr = buffer)
            {
                while (totalRead < frameCount)
                {
                    ulong framesRead;
                    result = ma.decoder_read_pcm_frames(
                        decoder,
                        bufferPtr,
                        chunkFrames,
                        &framesRead);
                    if (result != ma_result.MA_SUCCESS || framesRead == 0)
                        break;

                    for (ulong frame = 0; frame < framesRead; frame++)
                    {
                        int bin = (int)(
                            (totalRead + frame) * (ulong)bins / frameCount);
                        if (bin >= bins)
                            bin = bins - 1;

                        double sumSquares = 0;
                        for (uint channel = 0; channel < channels; channel++)
                        {
                            int index = (int)(frame * channels + channel);
                            float sample = buffer[index];
                            sumSquares += sample * sample;
                        }

                        peaks[bin] = Math.Clamp(
                            (float)Math.Sqrt(sumSquares / channels) * 1.8f,
                            0f,
                            1f);
                    }

                    totalRead += framesRead;
                }
            }

            if (totalRead == 0)
                return null;

            long durationMs = (long)totalRead * 1000L / sampleRate;
            return new WaveformData(peaks, durationMs);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (initialized)
                ma.decoder_uninit(decoder);

            NativeMemory.Free(decoder);
        }
    }

    private static float ReadSample(
        byte[] bytes,
        int offset,
        int format,
        int bitsPerSample)
    {
        if (format == 3 && bitsPerSample == 32)
            return BitConverter.ToSingle(bytes, offset);

        return bitsPerSample switch
        {
            8 => (bytes[offset] - 128) / 128f,
            16 => BitConverter.ToInt16(bytes, offset) / 32768f,
            24 => Read24(bytes, offset),
            32 => BitConverter.ToInt32(bytes, offset) / 2147483648f,
            _ => 0f,
        };
    }

    private static float Read24(byte[] bytes, int offset)
    {
        int value = bytes[offset] |
                    (bytes[offset + 1] << 8) |
                    (bytes[offset + 2] << 16);
        if ((value & 0x800000) != 0)
            value -= 0x1000000;

        return value / 8388608f;
    }
}
