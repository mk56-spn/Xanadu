using MoonWorks.Graphics;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// A graph's <c>[Reported]</c> lists and stored values, read back for the host without waiting on the GPU: each frame the
/// ones of every material that ran are copied into the next slot of a ring of download buffers, and the slot copied
/// <see cref="Late"/> frames ago, finished by now, is read into the material's <see cref="CanvasMaterial.Reports"/>.
/// </summary>
internal static class GraphReports
{
    /// <summary>
    /// Slots in the ring: a slot is read back this many frames after it was copied into, past the frames SDL_GPU keeps in
    /// flight, so its copy has finished.
    /// </summary>
    public const int Late = 4;

    /// <summary>The ring for a graph that reports something; null otherwise.</summary>
    public static TransferBuffer[]? Make(GraphicsDevice device, CompiledBuild build)
    {
        uint bytes = 0;
        for (int k = 0; k < build.Bytes.Length; k++)
            if (build.Shape.Buffers[k].Reported)
                bytes += Bytes(build, k);
        if (bytes == 0)
            return null;
        var ring = new TransferBuffer[Late];
        for (int slot = 0; slot < Late; slot++)
            ring[slot] = TransferBuffer.Create<byte>(device, "graph reports", TransferBufferUsage.Download, bytes);
        return ring;
    }

    /// <summary>
    /// Reads the slot copied <see cref="Late"/> frames ago into <paramref name="material"/>'s reports, then copies this
    /// frame's reported buffers into it: after the frame's last node.
    /// </summary>
    public static void Run(CommandBuffer commands, CanvasMaterial material, ref BuildBuffers kept, CompiledBuild build, long frame)
    {
        if (kept.Reports is not { } ring)
            return;
        kept.ReportedAt ??= new long[Late];
        int slot = (int)(frame % Late);
        if (kept.ReportedAt[slot] > 0)
        {
            ReadOnlySpan<byte> held = ring[slot].Map<byte>(cycle: false);
            int at = 0;
            for (int k = 0; k < build.Bytes.Length; k++)
            {
                if (!build.Shape.Buffers[k].Reported)
                    continue;
                int bytes = (int)Bytes(build, k);
                material.Report(build.Shape.Buffers[k].Name, held.Slice(at, bytes));
                at += bytes;
            }

            ring[slot].Unmap();
        }

        CopyPass copy = commands.BeginCopyPass();
        uint offset = 0;
        for (int k = 0; k < build.Bytes.Length; k++)
        {
            if (!build.Shape.Buffers[k].Reported)
                continue;
            uint bytes = Bytes(build, k);
            copy.DownloadFromBuffer(new BufferRegion(kept.Buffers[k], 0, bytes), new TransferBufferLocation(ring[slot].Handle, offset));
            offset += bytes;
        }

        commands.EndCopyPass(copy);
        kept.ReportedAt[slot] = frame;
    }

    private static uint Bytes(CompiledBuild build, int k) => (uint)((build.Bytes[k] + 3) / 4 * 4);
}
