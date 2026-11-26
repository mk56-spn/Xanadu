using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>
/// What a drag writes: the text between two offsets replaced (equal for an insert), or, for a
/// value bound to a tuned uniform, that value set instead.
/// </summary>
public readonly record struct HandleEdit(int From, int To, string Text, string? Uniform, float[] Value);

/// <summary>
/// A handle being dragged: everything as it was when it was taken, so each pull works the new
/// value out from there and the pointer's way since (no error builds up as the text changes under it).
/// </summary>
public sealed class SlangHandleDrag
{
    private readonly ChainHandles _shape;
    private readonly Vector2 _taken;
    private readonly float[] _value;

    public ChainHandle Handle { get; }

    /// <summary>Where the dragged chain's shape name starts: how it is found again in the changed text.</summary>
    public int ChainStart => _shape.Chain.Start;

    private SlangHandleDrag(ChainHandles shape, ChainHandle handle, Vector2 pointer, float[] value)
    {
        _shape = shape;
        Handle = handle;
        _taken = pointer;
        _value = value;
    }

    /// <summary>Takes <paramref name="handle"/> of <paramref name="shape"/> with the pointer at <paramref name="pointer"/>, from the preview's centre.</summary>
    public static SlangHandleDrag Take(ChainHandles shape, ChainHandle handle, Vector2 pointer, Func<string, int, IReadOnlyList<string>> parameterNames) =>
        new(shape, handle, pointer, Current(shape.Chain, handle, parameterNames));

    /// <summary>The value with the pointer now at <paramref name="pointer"/>.</summary>
    public float[] Pull(Vector2 pointer, Vector2 canvas)
    {
        Vector2 delta = pointer - _taken;
        Vector2 origin = _shape.Origin;
        switch (Handle.Kind)
        {
            case HandleKind.Move:
            {
                Similarity after = ShapePlacement.AfterFirstRun(_shape.Chain, canvas) ?? Similarity.Identity;
                Vector2 moved = after.Unlinear(delta);
                return [_value[0] + moved.X, _value[1] + moved.Y];
            }
            case HandleKind.Turn:
            {
                float from = Angle(_taken - origin), to = Angle(pointer - origin);
                float turned = MathF.IEEERemainder(to - from, MathF.Tau);
                return [_value[0] + turned];
            }
            case HandleKind.Scale:
            {
                float from = MathF.Max(Vector2.Distance(_taken, origin), 1), to = MathF.Max(Vector2.Distance(pointer, origin), 1);
                return [MathF.Max(_value[0] * to / from, 0.01f)];
            }
        }

        Vector2 local = Handle.Local + _shape.Placement.Unlinear(delta);
        Vector2 extent = _shape.Extent;
        return Handle.Kind switch
        {
            HandleKind.Radius => [local.Length()],
            HandleKind.Size or HandleKind.Half => Sized(local, Handle.Kind == HandleKind.Size ? 2 : 1),
            HandleKind.Corner => [Math.Clamp(extent.X - local.X, 0, MathF.Min(extent.X, extent.Y))],
            HandleKind.Point => [local.X, local.Y],
            _ => _value,
        };
    }

    /// <summary>A size from a dragged grip: only its axis changes, both for the corner.</summary>
    private float[] Sized(Vector2 local, float factor) => Handle.Axis switch
    {
        0 => [MathF.Abs(local.X) * factor, _value[1]],
        1 => [_value[0], MathF.Abs(local.Y) * factor],
        _ => [MathF.Abs(local.X) * factor, MathF.Abs(local.Y) * factor],
    };

    /// <summary>
    /// The edit that gives the handle's value <paramref name="value"/> in <paramref name="chain"/> (the
    /// dragged chain found again in the text as it is now); null when it can't be written there.
    /// </summary>
    public HandleEdit? Edit(ShapeChain chain, float[] value, Func<string, int, IReadOnlyList<string>> parameterNames)
    {
        string text = SlangHandles.Text(Handle.Kind, value);
        if (Handle.Kind is HandleKind.Move or HandleKind.Turn or HandleKind.Scale)
        {
            if (ShapePlacement.FirstRunStep(chain, Handle.Kind) is not { } step)
                return new HandleEdit(chain.Close + 1, chain.Close + 1, $".{Handle.Parameter}({text})", null, value);
            return step.Arguments is [{ Writable: true } argument] ? Write(argument, text, value) : null;
        }

        int index = SlangHandles.IndexOf(chain.Shape, Handle.Parameter, parameterNames);
        if (index >= 0 && index < chain.Arguments.Count)
            return chain.Arguments[index].Writable ? Write(chain.Arguments[index], text, value) : null;
        if (index == chain.Arguments.Count)
            return new HandleEdit(chain.Close, chain.Close, (index > 0 ? ", " : "") + text, null, value);
        return null;
    }

    private static HandleEdit Write(ChainArgument argument, string text, float[] value) =>
        new(argument.Start, argument.End, text, argument.Uniform, value);

    /// <summary>The value the handle is bound to now; the library's default for one not written (no move, no turn, scale 1, 0).</summary>
    private static float[] Current(ShapeChain chain, ChainHandle handle, Func<string, int, IReadOnlyList<string>> parameterNames)
    {
        if (handle.Kind is HandleKind.Move or HandleKind.Turn or HandleKind.Scale)
        {
            float[] none = handle.Kind switch { HandleKind.Move => [0, 0], HandleKind.Turn => [0], _ => [1] };
            return ShapePlacement.FirstRunStep(chain, handle.Kind) is { Arguments: [{ Value: { } value }] } ? value : none;
        }

        int index = SlangHandles.IndexOf(chain.Shape, handle.Parameter, parameterNames);
        return index >= 0 && index < chain.Arguments.Count && chain.Arguments[index].Value is { } own ? own : [0, 0];
    }

    private static float Angle(Vector2 v) => MathF.Atan2(v.Y, v.X);
}
