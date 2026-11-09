using System;
using System.IO;
using System.IO.Compression;
using MoonWorks.Graphics;

namespace Xanadu.Lab.MoonWorksParticles;

/// <summary>Reads a texture back from the GPU and writes it as a PNG (so the spike can prove it drew something).</summary>
internal static class Screenshot
{
    public static void Save(GraphicsDevice device, CommandBuffer commands, Texture texture, uint width, uint height, string path)
    {
        using var transfer = TransferBuffer.Create<byte>(device, TransferBufferUsage.Download, width * height * 4);
        CopyPass copy = commands.BeginCopyPass();
        copy.DownloadFromTexture(new TextureRegion(texture), new TextureTransferInfo(transfer));
        commands.EndCopyPass(copy);

        Fence fence = device.SubmitAndAcquireFence(commands);
        device.WaitForFence(fence);
        device.ReleaseFence(fence);

        byte[] pixels = transfer.Map<byte>(false).ToArray();
        transfer.Unmap();
        File.WriteAllBytes(path, Png(pixels, (int)width, (int)height));
        Console.WriteLine($"Saved {path}");
    }

    private static byte[] Png(byte[] rgba, int width, int height)
    {
        var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
            for (int y = 0; y < height; y++)
            {
                z.WriteByte(0);
                z.Write(rgba, y * width * 4, width * 4);
            }

        var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        WriteBig(header, 0, (uint)width);
        WriteBig(header, 4, (uint)height);
        header[8] = 8; header[9] = 6; // 8-bit RGBA
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", raw.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        var buffer = new byte[4];
        WriteBig(buffer, 0, (uint)data.Length);
        png.Write(buffer);
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        png.Write(typeBytes);
        png.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFF, typeBytes), data) ^ 0xFFFFFFFF;
        WriteBig(buffer, 0, crc);
        png.Write(buffer);
    }

    private static uint Crc(uint crc, byte[] data)
    {
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return crc;
    }

    private static void WriteBig(byte[] target, int at, uint value)
    {
        target[at] = (byte)(value >> 24); target[at + 1] = (byte)(value >> 16);
        target[at + 2] = (byte)(value >> 8); target[at + 3] = (byte)value;
    }
}
