using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xanadu.Core.Maths;

namespace Xanadu.Labs.Experiments.Particles;

/// <summary>Four uints, as the shader's <c>uint4</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct UInt4(uint x, uint y, uint z, uint w)
{
    public uint X = x, Y = y, Z = z, W = w;
}

/// <summary>An emitter as <c>particle_lab.slang</c>'s <c>Emitter</c>: ten 16-byte rows.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct EmitterGpu
{
    public UInt4 Kind, Emit;
    public Vector4 Place, Aim, Motion, Life, Size, Look, Born, Dies;
}

/// <summary>A module as the shader's <c>Module</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ModuleGpu
{
    public UInt4 Head;
    public Vector4 A, B;
}

[InlineArray(ParticleScene.EmitterCount)]
internal struct EmitterRow
{
    private EmitterGpu _first;
}

[InlineArray(ParticleScene.MostModules)]
internal struct ModuleRow
{
    private ModuleGpu _first;
}

/// <summary>
/// The shader's <c>Sim</c>: everything a frame needs, pushed as one uniform
/// block to every pass and draw. Its rows are all 16 bytes, so the C# and
/// std140 layouts agree without padding.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SimGpu
{
    public UInt4 Frame, Counts;
    public Vector4 Time, Mouse, Field, Look, Tint;
    public EmitterRow Emitters;
    public ModuleRow Modules;

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
    private static Vector4 Rgba(Color c) => new(c.R, c.G, c.B, c.A);

    public static EmitterGpu Pack(EmitterSettings e, uint spawns, uint first) => new()
    {
        Kind = new UInt4((uint)e.Shape, e.Child is >= 0 and < ParticleScene.EmitterCount ? (uint)e.Child + 1 : 0, e.Children, (uint)e.Look),
        Emit = new UInt4(spawns, first, e.On ? 1u : 0u, 0),
        Place = new Vector4(e.Position.X, e.Position.Y, e.Size.X, e.Size.Y),
        Aim = new Vector4(Radians(e.Turn), Radians(e.Direction), Radians(e.Spread), e.Outward),
        Motion = new Vector4(e.Speed.X, MathF.Max(e.Speed.X, e.Speed.Y), e.Inherit, e.Deposit),
        Life = new Vector4(MathF.Max(0.01f, e.Life.X), MathF.Max(e.Life.X, e.Life.Y), e.Trail, e.Stretch),
        Size = new Vector4(e.SizeRange.X, e.SizeRange.Y, e.Jitter, e.TrailWidth),
        Look = new Vector4(e.HueJitter, e.Tangent, e.FadeIn, 0),
        Born = Rgba(e.Born),
        Dies = Rgba(e.Dies),
    };

    public static ModuleGpu Pack(ModuleSettings m) => new()
    {
        Head = new UInt4((uint)m.Kind, m.On ? m.Mask : 0, (uint)m.Mode, 0),
        A = new Vector4(m.Values[0], m.Values[1], m.Values[2], m.Values[3]),
        B = new Vector4(m.Values[4], m.Values[5], m.Values[6], m.Values[7]),
    };

    public static Vector4 Colour(Color c) => Rgba(c);
}

/// <summary>What the GPU counted last frame, read back a frame or two late.</summary>
internal readonly record struct ParticleCounts(int Free, int Alive, int Trails, int Events, int Spawned, int Died);
