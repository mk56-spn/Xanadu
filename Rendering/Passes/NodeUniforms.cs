using System.Numerics;
using System.Runtime.InteropServices;

namespace Xanadu.Rendering.Passes;

// What a graph's nodes read as uniforms, laid out as their wrappers in Slang declare them.

/// <summary>What a compute node reads at uniform 0: the frame's clock and beat, the elements for <c>over:</c>, and the call's arguments.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PassUniforms
{
    public float Dt, Time;
    public uint Over;
    public float Beat;
    public uint A, B, C, D;
}

/// <summary>A pixel node's quad (uniform 2, <c>XanaduTraceQuad</c> in SlangShaders.Pixels): the quad on the target and the box it covers.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TraceQuad
{
    public Vector2 Corner, XAxis, YAxis;
    public int OriginX, OriginY;
    public uint Width, Height;
    public float Dt, Pad;

    /// <summary>Pixels of the box per texel of what a pixel node runs over: 1 over the box itself.</summary>
    public Vector2 Stretch;
    public Vector2 Pad2;
}

/// <summary>A draw node's quad (vertex uniform 0, <c>XanaduDrawQuad</c> in SlangShaders.Draws): the item's corner and sides on the target.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DrawQuad
{
    public Vector2 Corner, XAxis, YAxis;

    /// <summary>Where z lands: (0, 1) for the item's own draw depth, its slice of the canvas depth onto the frame.</summary>
    public Vector2 Slice;
}

/// <summary>A draw node's fragment placing (uniform 2 of set 3, <c>XanaduDrawBox</c>): the shift onto the target, the box's corner, the slice.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DrawBox
{
    public Vector2 Shift, Origin, Slice, Pad;
}
