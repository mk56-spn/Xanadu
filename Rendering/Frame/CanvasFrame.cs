using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Kept;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using Entity = Friflo.Engine.ECS.Entity;
using MwColor = MoonWorks.Graphics.Color;
using Rect = MoonWorks.Graphics.Rect;

namespace Xanadu.Rendering.Frame;

/// <summary>One item's draws in a job: its place, and what it inherits.</summary>
/// <remarks>A resident segment (<see cref="Piece"/>) has no item: its items are placed on the GPU.</remarks>
internal readonly record struct Entry(int Z, int Sequence, CanvasItem Item, Transform2D Transform, Color Color, Rect Clip,
    ResidentPiece? Piece = null);

internal enum StepKind
{
    /// <summary>A run of the canvas stack's placed primitives (<see cref="Passes.CanvasStack"/>): its <c>ordered</c> draw over <see cref="Step.Count"/> copies from <see cref="Step.Start"/>.</summary>
    Canvas,
    Effect,
    Particles,

    /// <summary>Copies of a kept mesh (<see cref="CopySteps"/>): <see cref="Step.Start"/> is the first in <see cref="CanvasFrame.Copies"/>, <see cref="Step.Count"/> how many.</summary>
    Copies,

    /// <summary>An item's triangles drawn once per particle of its field (<see cref="CanvasItem.ParticleMesh"/>), through their effect.</summary>
    MeshParticles,
}

/// <summary>
/// One draw, in order: a run of primitives (with up to <see cref="Passes.CanvasStack.Slots"/>
/// textures, from <see cref="FirstTexture"/> in the texture list), an effect's
/// vertices, or a particle field. A kept effect range's step carries the entity keeping it (<see cref="Kept"/>): the
/// item's range from frame to frame, where its graph holds the images that outlive a frame.
/// </summary>
internal readonly record struct Step(
    StepKind Kind, uint Start, uint Count, int FirstTexture = 0,
    Rect Clip = default, CanvasMaterial? Material = null, IReadOnlyDictionary<string, Vector4>? Values = null, ulong Texture = 0,
    ParticleField? Particles = null, Transform2D Transform = default, Color Color = default, float Depth = 0, float Span = 0,
    KeptVertices? Mesh = null, bool Grid = false, Entity Kept = default);

/// <summary>One root drawn into one target.</summary>
internal sealed class Job(CanvasItem root, Texture target, MwColor clear)
{
    /// <summary>For a material's view (<see cref="MaterialViews"/>), that material: its own items aren't drawn into it.</summary>
    public CanvasMaterial? Viewer { get; init; }

    /// <summary>For a view, its own number, so what an item keeps for being drawn here is apart from the tree's; 0 otherwise.</summary>
    public int View { get; init; }

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
    public float Beat, Unit, Pad1, Pad2;

    public static FrameUniforms Now(float width, float height, float time) => new()
    {
        Width = width, Height = height, Time = time, Beat = ShaderClock.Beat, Unit = 1,
    };

    /// <summary>The frame for one run's draws: its unit, from the run's values (<see cref="CanvasMaterial.Unit"/>), 1 without one.</summary>
    public readonly FrameUniforms For(IReadOnlyDictionary<string, Vector4>? values) =>
        this with { Unit = values is not null && values.TryGetValue(CanvasMaterial.Unit, out Vector4 unit) ? unit.X : 1 };
}

/// <summary>
/// What one frame of the canvas is made of, shared by its passes: the jobs collected, the
/// steps built from them in draw order with their run textures, and the vertices effects cut
/// on the CPU. Cleared and filled again every frame.
/// </summary>
internal sealed class CanvasFrame
{
    public readonly List<Job> Jobs = [];
    public readonly List<Step> Steps = [];

    /// <summary>Each primitive run's texture slots, <see cref="Passes.CanvasStack.Slots"/> a run, from its step's <see cref="Step.FirstTexture"/>.</summary>
    public readonly List<ulong> StepTextures = [];

    public readonly List<CanvasVertex> Vertices = [];

    /// <summary>Every copy drawn this frame, placed on its target, and their values (each at its <see cref="GpuCopy.ValuesAt"/>).</summary>
    public readonly List<GpuCopy> Copies = [];
    public readonly List<byte> CopyValues = [];

    /// <summary>Counts frames built, for what is kept between them.</summary>
    public long Number;

    /// <summary>The frame's time step and clock, for stacks and particles.</summary>
    public float Dt, Time;
}
