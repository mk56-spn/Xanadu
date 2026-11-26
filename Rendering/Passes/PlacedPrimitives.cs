using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// The frame's CPU-placed recordings: one instance per recording drawn, with its run slots, which the canvas stack's
/// <c>expand</c> pass places into the primitives in draw order and the pre-pass list (one slot per placed primitive,
/// holding its opaque cut or a skip). Fixed slots rather than an atomic count and an indirect draw: the indirect args
/// raced the compute pass (the pre-pass drew nothing on most frames), and a skip costs the vertex stage next to nothing.
/// </summary>
internal sealed class PlacedPrimitives
{
    /// <summary>Depth steps per primitive: room for four million in a frame before they share a depth.</summary>
    public const float DepthStep = 1f / (1 << 22);

    /// <summary>An instance's flag: its plain opaque rects may join the pre-pass.</summary>
    public const uint AllowOpaque = 1;

    /// <summary>One per recording drawn this frame, and each one's run slots.</summary>
    public readonly List<GpuInstance> Instances = [];

    public readonly List<uint> Slots = [];

    /// <summary>Primitives placed this frame, and pre-pass slots kept for them.</summary>
    public int PlacedCount, OpaqueReserved;

    /// <summary>Forgets this frame's instances: the build starts again.</summary>
    public void Clear()
    {
        Instances.Clear();
        Slots.Clear();
        PlacedCount = 0;
        OpaqueReserved = 0;
    }

    /// <summary>
    /// Hands the canvas stack the CPU's instances and slots and sizes what it places into; the resident trees'
    /// instances follow the CPU's in the same list.
    /// </summary>
    public void Send(CanvasMaterial canvasStack, int residentInstances)
    {
        HostLists lists = canvasStack.Lists;
        int instances = Instances.Count + residentInstances;
        lists.Size("instances", instances);
        lists.Set("instances", CollectionsMarshal.AsSpan(Instances));
        lists.Size("slots", Slots.Count);
        lists.Set("slots", CollectionsMarshal.AsSpan(Slots));
        lists.Size("placed", PlacedCount);
        lists.Size("opaque", OpaqueReserved);
        canvasStack.Set("instanceCount", instances);
    }
}
