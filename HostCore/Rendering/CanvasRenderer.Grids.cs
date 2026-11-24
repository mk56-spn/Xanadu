using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Host.Rendering.Compute;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = Xanadu.Core.Maths.Color;

namespace Xanadu.Host.Rendering;

/// <summary>
/// An effect's [Grid], cut on the GPU (grid_cut.slang): every box a gridded
/// effect draws is a job of one compute operation, and the frame's grids are
/// written into one vertex buffer the effect draws read. The CPU only counts
/// vertices, so a grid resized every frame (the shader tool's preview while its
/// divider is dragged) costs it nothing more than one standing still.
/// </summary>
internal sealed partial class CanvasRenderer
{
    /// <summary>grid_cut.slang's <c>GridJob</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct GridJob
    {
        public Vector4 Corners01, Corners23, Uvs01, Uvs23;
        public Vector4 Colour0, Colour1, Colour2, Colour3;
        public Vector4 Basis, Origin;
        public uint Columns, Rows, FirstVertex, FirstPoint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GridUniforms { public uint Pad0, Pad1, Pad2, Pad3; }

    /// <summary>A canvas vertex in 32-bit words, as grid_cut.slang writes it.</summary>
    private const int VertexWords = 17;

    private ComputeOperation<GridJob>? _gridCut;
    private Buffer? _gridVertices;
    private uint _gridVertexCapacity;
    private int _gridVertexCount;
    private uint _gridMostCells;

    /// <summary>Forgets the grids planned so far: the build starts again.</summary>
    private void ClearGrids()
    {
        _gridCut?.Clear();
        _gridVertexCount = 0;
        _gridMostCells = 0;
    }

    /// <summary>
    /// The range's boxes as grid jobs, their vertices in the frame's grid buffer, a step per run
    /// of one texture; false (nothing added) when the range holds a triangle or a disc, which only
    /// the CPU cuts.
    /// </summary>
    private bool AddGrid(CanvasItem item, in Entry entry, int from, int to, int columns, int rows, StepKind kind,
        CanvasMaterial? material, IReadOnlyDictionary<string, Vector4>? values, float depth, float span)
    {
        for (int i = from; i < to; i++)
        {
            if (item.Primitives[i].Kind is PrimitiveKind.Triangle or PrimitiveKind.Disc)
                return false;
        }

        _gridCut ??= new ComputeOperation<GridJob>(_device, "grid_cut.slang", "cutGrids", inputs: 0, groupsPerJob: 1);
        Transform2D t = entry.Transform;
        var basis = new Vector4(t.X.X, t.X.Y, t.Y.X, t.Y.Y);
        var origin = new Vector4(t.Origin.X, t.Origin.Y, 0, 0);
        uint cells = (uint)(columns * rows);
        _gridMostCells = Math.Max(_gridMostCells, cells);

        int points = 0;
        int start = _gridVertexCount;
        ulong texture = item.Primitives[from].Texture;
        for (int i = from; i < to; i++)
        {
            Primitive p = item.Primitives[i];
            if (p.Texture != texture && _gridVertexCount > start)
            {
                _steps.Add(new Step(kind, (uint)start, (uint)(_gridVertexCount - start),
                    Clip: entry.Clip, Material: material, Values: values, Texture: texture, Depth: depth, Span: span,
                    Transform: entry.Transform, Grid: true));
                start = _gridVertexCount;
            }

            texture = p.Texture;
            BoxCorners k = p.Corners();
            _gridCut.Submit(new GridJob
            {
                Corners01 = new Vector4(k.P0.X, k.P0.Y, k.P1.X, k.P1.Y),
                Corners23 = new Vector4(k.P2.X, k.P2.Y, k.P3.X, k.P3.Y),
                Uvs01 = new Vector4(k.U0.X, k.U0.Y, k.U1.X, k.U1.Y),
                Uvs23 = new Vector4(k.U2.X, k.U2.Y, k.U3.X, k.U3.Y),
                Colour0 = Tinted(k.C0, entry.Color), Colour1 = Tinted(k.C1, entry.Color),
                Colour2 = Tinted(k.C2, entry.Color), Colour3 = Tinted(k.C3, entry.Color),
                Basis = basis,
                Origin = origin,
                Columns = (uint)columns, Rows = (uint)rows,
                FirstVertex = (uint)_gridVertexCount, FirstPoint = (uint)points,
            });
            _gridVertexCount += (int)cells * 6;
            points += (columns + 1) * (rows + 1);
        }

        if (_gridVertexCount > start)
        {
            _steps.Add(new Step(kind, (uint)start, (uint)(_gridVertexCount - start),
                Clip: entry.Clip, Material: material, Values: values, Texture: texture, Depth: depth, Span: span,
                Transform: entry.Transform, Grid: true));
        }

        return true;
    }

    private static Vector4 Tinted(Color corner, Color tint)
    {
        Color c = corner * tint;
        return new Vector4(c.R, c.G, c.B, c.A);
    }

    /// <summary>Cuts the frame's grids into the grid vertex buffer, in one dispatch.</summary>
    private void CutGrids(CommandBuffer commands)
    {
        if (_gridCut is null || _gridCut.Count == 0)
            return;

        // The buffer is read as vertices and written as plain words.
        uint words = (uint)(_gridVertexCount * VertexWords);
        if (_gridVertices is null || words > _gridVertexCapacity)
        {
            _gridVertices?.Dispose();
            _gridVertexCapacity = Math.Max(words, _gridVertexCapacity * 2);
            _gridVertices = Buffer.Create<uint>(_device, "grid vertices",
                BufferUsageFlags.Vertex | BufferUsageFlags.ComputeStorageWrite, _gridVertexCapacity);
        }

        _gridCut.Run(commands, _gridVertices, [], new GridUniforms(),
            (_gridMostCells + ComputeOperation<GridJob>.Threads - 1) / ComputeOperation<GridJob>.Threads);
    }

    private void DisposeGrids()
    {
        _gridCut?.Dispose();
        _gridVertices?.Dispose();
    }
}
