using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Kept;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using MwColor = MoonWorks.Graphics.Color;
using Rect = MoonWorks.Graphics.Rect;

namespace Xanadu.Rendering.Frame;

/// <summary>One item's draws in a job: its place, and what it inherits.</summary>
/// <remarks>A resident segment (<see cref="Piece"/>) has no item: its items are placed on the GPU.</remarks>
internal readonly record struct Entry(int Z, int Sequence, CanvasItem Item, Transform2D Transform, Color Color, Rect Clip,
    ResidentPiece? Piece = null);

internal enum StepKind
{
    Primitives,
    Effect,
    Particles,

    /// <summary>An item's triangles drawn once per particle of its field (<see cref="CanvasItem.ParticleMesh"/>), through their effect.</summary>
    MeshParticles,
}

/// <summary>
/// One draw, in order: a run of primitives (with up to <see cref="PrimitivePipelines.Slots"/>
/// textures, from <see cref="FirstTexture"/> in the texture list), an effect's
/// vertices, or a particle field.
/// </summary>
internal readonly record struct Step(
    StepKind Kind, uint Start, uint Count, int FirstTexture = 0,
    Rect Clip = default, CanvasMaterial? Material = null, IReadOnlyDictionary<string, Vector4>? Values = null, ulong Texture = 0,
    ParticleField? Particles = null, Transform2D Transform = default, Color Color = default, float Depth = 0, float Span = 0,
    KeptVertices? Mesh = null, bool Grid = false);

/// <summary>One root drawn into one target.</summary>
internal sealed class Job(CanvasItem root, Texture target, MwColor clear)
{
    public readonly CanvasItem Root = root;
    public readonly Texture Target = target;
    public readonly MwColor Clear = clear;
    public readonly List<Entry> Entries = [];
    public int FirstStep, StepCount;
    public uint OpaqueStart, OpaqueReserved;
}

/// <summary>xanadu.stage's <c>Frame</c>: fragment uniform buffer 0. Only the engine's clocks; anything else is a global.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FrameUniforms
{
    public float Width, Height, Time, ItemPremultiplied;
    public float Beat, Pad0, Pad1, Pad2;

    public static FrameUniforms Now(float width, float height, float time) => new()
    {
        Width = width, Height = height, Time = time, Beat = ShaderClock.Beat,
    };
}

/// <summary>A stack's compute clock (uniform buffer 0 of its UPDATE and STEP passes, SlangShaders.WrapUpdate).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct UpdateUniforms { public float Dt, Time, Beat, Pad; }

/// <summary>
/// What one frame of the canvas is made of, shared by its passes: the jobs collected, the
/// steps built from them in draw order with their run textures, and the vertices effects cut
/// on the CPU. Cleared and filled again every frame.
/// </summary>
internal sealed class CanvasFrame
{
    public readonly List<Job> Jobs = [];
    public readonly List<Step> Steps = [];

    /// <summary>Each primitive run's texture slots, <see cref="PrimitivePipelines.Slots"/> a run, from its step's <see cref="Step.FirstTexture"/>.</summary>
    public readonly List<ulong> StepTextures = [];

    public readonly List<CanvasVertex> Vertices = [];

    /// <summary>Counts frames built, for what is kept between them.</summary>
    public long Number;

    /// <summary>The frame's time step and clock, for stacks and particles.</summary>
    public float Dt, Time;
}
