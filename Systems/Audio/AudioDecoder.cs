using System;
using System.Runtime.InteropServices;
using Miniaudio;

namespace Xanadu.Systems.Audio;

/// <summary>Decodes a whole audio file to mono floats at a chosen sample rate.</summary>
public static class AudioDecoder
{
    /// <summary>Returns the samples, or null when the file cannot be decoded.</summary>
    public static unsafe float[]? DecodeMono(string path, int sampleRate)
    {
        ma_decoder* decoder = (ma_decoder*)NativeMemory.Alloc((nuint)sizeof(ma_decoder));
        bool initialized = false;
        try
        {
            ma_decoder_config config = ma.decoder_config_init(
                ma_format.ma_format_f32,
                1,
                (uint)sampleRate);

            IntPtr pathPtr = Marshal.StringToHGlobalAnsi(path);
            ma_result result;
            try
            {
                result = ma.decoder_init_file((sbyte*)pathPtr, &config, decoder);
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }

            if (result != ma_result.MA_SUCCESS)
                return null;

            initialized = true;
            ulong frameCount;
            if (ma.decoder_get_length_in_pcm_frames(decoder, &frameCount) != ma_result.MA_SUCCESS ||
                frameCount == 0)
            {
                return null;
            }

            float[] samples = new float[frameCount];
            ulong total = 0;
            fixed (float* destination = samples)
            {
                while (total < frameCount)
                {
                    ulong read;
                    result = ma.decoder_read_pcm_frames(
                        decoder,
                        destination + total,
                        Math.Min(65536UL, frameCount - total),
                        &read);
                    if (result != ma_result.MA_SUCCESS || read == 0)
                        break;

                    total += read;
                }
            }

            return total == 0 ? null : samples[..(int)total];
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
}
