using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Xanadu.Labs.Experiments.EcsRender;

/// <summary>What the GPU gets per shape and per glyph; <c>Instance</c> in the shader.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DrawInstance
{
    public const float KindGlyph = 3;

    public Vector4 Box;     // centre x, y and width, height
    public Vector4 Uv;      // a glyph's atlas rect
    public Vector4 Colour;
    public Vector4 Look;    // turn, kind, edge, unused
}

/// <summary>
/// The frame the render systems build between them: the extract systems add
/// instances as they find them, the sort system orders their keys, the write
/// system sends them and notes the draws. Nothing else passes between them.
/// </summary>
internal sealed class DrawList(Vector2 stage)
{
    public const short NoPage = -1;

    /// <summary>The stage's size in pixels; what lies wholly outside it is culled.</summary>
    public readonly Vector2 Stage = stage;

    public DrawInstance[] Instances = new DrawInstance[1 << 12];

    /// <summary>Each instance's atlas page, or <see cref="NoPage"/> for a shape.</summary>
    public short[] Pages = new short[1 << 12];

    /// <summary>Layer, then z, then the index found at: sorting the keys sorts the frame, and a key's low half says which instance it is.</summary>
    public ulong[] Keys = new ulong[1 << 12];

    public int Count;
    public bool InOrder;
    public int Culled, LaidOut;

    /// <summary>Runs of instances drawn with one call, with the atlas page they need.</summary>
    public readonly List<(uint Start, uint Count, short Page)> Draws = [];

    public void Begin()
    {
        Count = 0;
        Culled = 0;
        InOrder = true;
        Draws.Clear();
    }

    public void Reserve(int more)
    {
        if (Count + more <= Instances.Length)
            return;

        int size = Math.Max(Instances.Length * 2, Count + more);
        Array.Resize(ref Instances, size);
        Array.Resize(ref Pages, size);
        Array.Resize(ref Keys, size);
    }

    public void Add(Depth depth, short page, in DrawInstance instance)
    {
        ulong key = (ulong)(ushort)(depth.Layer + 32768) << 48 | (ulong)(ushort)(depth.Z + 32768) << 32 | (uint)Count;
        if (Count > 0 && key < Keys[Count - 1])
            InOrder = false;
        Keys[Count] = key;
        Pages[Count] = page;
        Instances[Count] = instance;
        Count++;
    }
}
