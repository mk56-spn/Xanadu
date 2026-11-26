using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace Xanadu.Slang;

/// <summary>
/// One handle on the preview: what it changes (<see cref="Kind"/>, the constructor parameter or
/// placement method <see cref="Parameter"/>, the <see cref="Axis"/> of a size: 0 x, 1 y, 2 both),
/// where it is (<see cref="At"/>, from the preview's centre) and where that is in the shape's own space.
/// </summary>
public readonly record struct ChainHandle(HandleKind Kind, string Parameter, int Axis, Vector2 At, Vector2 Local);

/// <summary>A chain's shape as the handles see it: its placement, half its size in its own space, and its handles.</summary>
public sealed record ChainHandles(ShapeChain Chain, Similarity Placement, Vector2 Extent, bool HasExtent, List<ChainHandle> Handles)
{
    public Vector2 Origin => Placement.Offset;
}

/// <summary>
/// The handles a chain gets, from the library's tags (<see cref="SlangHandleTags"/>): the
/// constructor's tagged parameters, and on every shape a move square at its centre, a turn dot at
/// the end of an arm over it and a scale square out on its diagonal. A handle bound to a value
/// that is worked out (not a literal or a tuned value) shows nothing, as nothing could be written;
/// one bound to a parameter or a placement step that isn't written yet is, and dragging it writes it.
/// </summary>
public static class SlangHandles
{
    /// <summary>How far past the shape's top the turn dot sits, and past its corner the scale square, in pixels.</summary>
    private const float ArmReach = 28, ScaleReach = 20;

    /// <summary>The handles of a chain the CPU can place; null otherwise.</summary>
    public static ChainHandles? Of(ShapeChain chain, SlangHandleTags tags, Func<string, int, IReadOnlyList<string>> parameterNames, Vector2 canvas)
    {
        if (ShapePlacement.Of(chain, canvas) is not { } placement)
            return null;
        IReadOnlyList<HandleTag> own = tags.Of(chain.Shape);
        (Vector2 extent, bool hasExtent) = Extent(chain, own, parameterNames);
        var handles = new List<ChainHandle>();

        foreach (HandleTag tag in own)
        {
            int index = IndexOf(chain.Shape, tag.Parameter, parameterNames);
            ChainArgument? argument = index >= 0 && index < chain.Arguments.Count ? chain.Arguments[index] : null;
            // Not written yet: only the next one, which an edit can add at the end.
            bool missing = argument is null && index == chain.Arguments.Count;
            if (argument is null && !missing)
                continue;
            if (!missing && !argument!.Writable)
                continue;
            float[] value = argument?.Value ?? [0f, 0f];
            void Add(int axis, Vector2 local) => handles.Add(new ChainHandle(tag.Kind, tag.Parameter, axis, placement.Apply(local), local));
            switch (tag.Kind)
            {
                case HandleKind.Radius:
                    Add(0, new Vector2(value[0], 0));
                    break;
                case HandleKind.Size or HandleKind.Half when value.Length == 2:
                    var half = new Vector2(value[0], value[1]) * (tag.Kind == HandleKind.Size ? 0.5f : 1f);
                    Add(0, new Vector2(half.X, 0));
                    Add(1, new Vector2(0, half.Y));
                    Add(2, half);
                    break;
                case HandleKind.Corner when hasExtent:
                    Add(0, new Vector2(extent.X - value[0], -extent.Y));
                    break;
                case HandleKind.Point when value.Length == 2:
                    Add(0, new Vector2(value[0], value[1]));
                    break;
            }
        }

        Placed(chain, tags, HandleKind.Move, placement.Offset, Vector2.Zero, handles);
        Vector2 up = Similarity.Turn(new Vector2(0, -1), placement.Angle);
        Placed(chain, tags, HandleKind.Turn, placement.Apply(new Vector2(0, -extent.Y)) + up * ArmReach, Vector2.Zero, handles);
        Vector2 corner = placement.Apply(extent);
        Vector2 diagonal = Vector2.Normalize(placement.Linear(extent) is { } d && d.LengthSquared() > 1e-6f ? d : Vector2.One);
        Placed(chain, tags, HandleKind.Scale, corner + diagonal * ScaleReach, Vector2.Zero, handles);
        return new ChainHandles(chain, placement, extent, hasExtent, handles);
    }

    /// <summary>A placement handle, writable when its first-run step is a literal or a tuned value, or isn't written yet.</summary>
    private static void Placed(ShapeChain chain, SlangHandleTags tags, HandleKind kind, Vector2 at, Vector2 local, List<ChainHandle> handles)
    {
        if (tags.MethodFor(kind) is not { } method)
            return;
        bool writable = ShapePlacement.FirstRunStep(chain, kind) is not { } step || step.Arguments is [{ Writable: true }];
        if (writable)
            handles.Add(new ChainHandle(kind, method, 0, at, local));
    }

    /// <summary>Half the shape's size in its own space, from its radius, size or points; a small default when it has none.</summary>
    private static (Vector2, bool) Extent(ShapeChain chain, IReadOnlyList<HandleTag> own, Func<string, int, IReadOnlyList<string>> parameterNames)
    {
        Vector2 points = Vector2.Zero;
        foreach (HandleTag tag in own)
        {
            int index = IndexOf(chain.Shape, tag.Parameter, parameterNames);
            if (index < 0 || index >= chain.Arguments.Count || chain.Arguments[index].Value is not { } value)
                continue;
            switch (tag.Kind)
            {
                case HandleKind.Radius:
                    return (new Vector2(MathF.Abs(value[0])), true);
                case HandleKind.Size when value.Length == 2:
                    return (Vector2.Abs(new Vector2(value[0], value[1])) * 0.5f, true);
                case HandleKind.Half when value.Length == 2:
                    return (Vector2.Abs(new Vector2(value[0], value[1])), true);
                case HandleKind.Point when value.Length == 2:
                    points = Vector2.Max(points, Vector2.Abs(new Vector2(value[0], value[1])));
                    break;
            }
        }

        return points != Vector2.Zero ? (points, false) : (new Vector2(24), false);
    }

    /// <summary>Which argument of <paramref name="shape"/>'s constructor <paramref name="parameter"/> is; -1 when none is.</summary>
    public static int IndexOf(string shape, string parameter, Func<string, int, IReadOnlyList<string>> parameterNames)
    {
        for (int index = 0; index < 12; index++)
        {
            IReadOnlyList<string> names = parameterNames(shape, index);
            if (names.Count == 0)
                return -1;
            if (names.Contains(parameter))
                return index;
        }

        return -1;
    }

    /// <summary>A value as the code writes it: whole pixels for places and sizes, two decimals for turns and scales.</summary>
    public static string Text(HandleKind kind, float[] value)
    {
        string One(float v) => kind is HandleKind.Turn or HandleKind.Scale
            ? MathF.Round(v, 2).ToString("0.0#", CultureInfo.InvariantCulture)
            : ((int)MathF.Round(v)).ToString(CultureInfo.InvariantCulture);
        return value.Length == 2 ? $"float2({One(value[0])}, {One(value[1])})" : One(value[0]);
    }
}
