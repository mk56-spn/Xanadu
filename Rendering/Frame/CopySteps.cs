using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Rendering.Passes;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Rendering.Frame;

/// <summary>One copy as the copied stages read it: SlangCopies' <c>XanaduCopy</c>, 80 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GpuCopy
{
    public Vector2 Origin, XAxis, YAxis;
    public float Unit, UvShift;
    public Vector4 Modulate, Flash;
    public uint ValuesAt, Pad0, Pad1, Pad2;
}

/// <summary>
/// Building a copy draw's step (<see cref="CopyDraw"/>): every copy placed on the target and its values packed as the
/// effect's values block, into the frame's lists (<see cref="CanvasFrame.Copies"/>, <see cref="CanvasFrame.CopyValues"/>),
/// and one step drawing them all over the mesh kept in its own space.
/// </summary>
internal sealed class CopySteps(CanvasFrame frame, CanvasPipelines pipelines, CopiesPipelines copies, CopiedMeshes meshes)
{
    /// <summary>Adds the step at the draw-order depth after <paramref name="sequence"/>; returns the depth steps it takes (none when it draws nothing).</summary>
    public uint Add(CanvasItem item, in Entry entry, CopyDraw draw, uint sequence)
    {
        CanvasMaterial? material = item.Material ?? (draw.Run > 0 ? item.Runs[draw.Run - 1].Material : null);
        if (material is null)
            return 0;
        CanvasEffect effect = pipelines.Effect(material);
        if (effect == pipelines.Plain || copies.Of(effect, material.Shader) is not { } copied)
            return 0;
        KeptVertices mesh = meshes.Of(draw.Mesh, frame.Number);
        if (mesh.Vertices.Count == 0)
            return 0;

        // A deform sets its own depths, so the copies take a slice to spread them over, as one effect does.
        uint taken = copied.Deforms ? StepBuilder.DeformSlice : 1;
        float depth = 1f - (sequence + 1) * PlacedPrimitives.DepthStep;
        float span = copied.Deforms ? (taken - 1) * PlacedPrimitives.DepthStep : 0;

        int first = frame.Copies.Count;
        int size = copied.Compiled.ValuesSize;
        Transform2D placed = entry.Transform;
        foreach (CanvasCopy copy in draw.Copies)
        {
            int at = frame.CopyValues.Count;
            if (size > 0)
            {
                CollectionsMarshal.SetCount(frame.CopyValues, at + size);
                ReadOnlySpan<Vector4> own = copy.Values >= 0 && copy.Values < draw.Values.Length ? draw.Values.AsSpan(copy.Values) : [];
                ShaderParams.Pack(CollectionsMarshal.AsSpan(frame.CopyValues).Slice(at, size), effect.Compiled, material, null, own);
            }

            Transform2D place = placed * copy.Place;
            Color tint = copy.Modulate * entry.Color;
            frame.Copies.Add(new GpuCopy
            {
                Origin = place.Origin, XAxis = place.X, YAxis = place.Y,
                Unit = copy.Unit, UvShift = copy.UvShift,
                Modulate = new Vector4(tint.R, tint.G, tint.B, tint.A),
                Flash = new Vector4(copy.Flash.R, copy.Flash.G, copy.Flash.B, copy.FlashAmount),
                ValuesAt = (uint)at,
            });
        }

        frame.Steps.Add(new Step(StepKind.Copies, (uint)first, (uint)draw.Copies.Length, Clip: entry.Clip, Material: material,
            Depth: depth, Span: span, Transform: entry.Transform, Color: entry.Color, Mesh: mesh));
        return taken;
    }
}
