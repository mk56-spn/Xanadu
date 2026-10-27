using System;
using System.IO;
using System.Text;

namespace Xanadu.Levels;

/// <summary>Reads basic title/artist metadata from MP3 ID3 tags.</summary>
public static class AudioTagReader
{
    public static bool TryRead(
        string path,
        out string title,
        out string artist)
    {
        title = "";
        artist = "";
        if (!File.Exists(path) ||
            !Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            if (ReadId3v2(stream, ref title, ref artist))
                return !string.IsNullOrWhiteSpace(title) ||
                       !string.IsNullOrWhiteSpace(artist);

            stream.Seek(0, SeekOrigin.Begin);
            return ReadId3v1(stream, ref title, ref artist);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool ReadId3v2(
        FileStream stream,
        ref string title,
        ref string artist)
    {
        byte[] header = new byte[10];
        if (stream.Read(header, 0, 10) != 10 ||
            header[0] != 'I' || header[1] != 'D' || header[2] != '3')
        {
            return false;
        }

        int version = header[3];
        bool v24 = version == 4;
        int size = (header[6] & 0x7F) << 21 |
                   (header[7] & 0x7F) << 14 |
                   (header[8] & 0x7F) << 7 |
                   (header[9] & 0x7F);
        size = Math.Min(size, 16 * 1024 * 1024);
        byte[] data = new byte[size];
        int read = stream.Read(data, 0, size);
        int position = 0;

        while (position + 6 <= read)
        {
            string id;
            int frameSize;
            int headerSize;
            if (version == 2)
            {
                id = Encoding.ASCII.GetString(data, position, 3);
                frameSize = data[position + 3] << 16 |
                            data[position + 4] << 8 |
                            data[position + 5];
                headerSize = 6;
            }
            else
            {
                if (position + 10 > read)
                    break;
                id = Encoding.ASCII.GetString(data, position, 4);
                frameSize = v24
                    ? (data[position + 4] & 0x7F) << 21 |
                      (data[position + 5] & 0x7F) << 14 |
                      (data[position + 6] & 0x7F) << 7 |
                      (data[position + 7] & 0x7F)
                    : data[position + 4] << 24 |
                      data[position + 5] << 16 |
                      data[position + 6] << 8 |
                      data[position + 7];
                headerSize = 10;
            }

            if (frameSize <= 0 ||
                position + headerSize + frameSize > read)
            {
                break;
            }

            if (id is "TIT2" or "TT2")
                title = DecodeText(data, position + headerSize, frameSize);
            else if (id is "TPE1" or "TP1")
                artist = DecodeText(data, position + headerSize, frameSize);

            position += headerSize + frameSize;
        }

        return true;
    }

    private static string DecodeText(byte[] data, int offset, int length)
    {
        if (length <= 1)
            return "";

        byte encoding = data[offset];
        int start = offset + 1;
        int count = length - 1;
        try
        {
            string value = encoding switch
            {
                0 => Encoding.Latin1.GetString(data, start, count),
                1 => Encoding.Unicode.GetString(data, start, count),
                2 => Encoding.BigEndianUnicode.GetString(data, start, count),
                3 => Encoding.UTF8.GetString(data, start, count),
                _ => Encoding.Latin1.GetString(data, start, count),
            };
            return value.TrimEnd('\0', '\uFEFF').Trim();
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static bool ReadId3v1(
        FileStream stream,
        ref string title,
        ref string artist)
    {
        if (stream.Length < 128)
            return false;

        stream.Seek(-128, SeekOrigin.End);
        byte[] tag = new byte[128];
        if (stream.Read(tag, 0, 128) != 128 ||
            tag[0] != 'T' || tag[1] != 'A' || tag[2] != 'G')
        {
            return false;
        }

        title = Encoding.Latin1.GetString(tag, 3, 30)
            .TrimEnd('\0', ' ').Trim();
        artist = Encoding.Latin1.GetString(tag, 33, 30)
            .TrimEnd('\0', ' ').Trim();
        return !string.IsNullOrWhiteSpace(title) ||
               !string.IsNullOrWhiteSpace(artist);
    }
}
