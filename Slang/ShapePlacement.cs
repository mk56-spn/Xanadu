using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>
/// A move, a turn and an even scale, as the library's <c>Placement</c> applies them: scaled
/// round the centre, turned (radians, clockwise on screen, y down), then moved.
/// </summary>
public readonly record struct Similarity(Vector2 Offset, float Angle, float Scale)
{
    public static readonly Similarity Identity = new(Vector2.Zero, 0, 1);

    public Vector2 Apply(Vector2 point) => Offset + Linear(point);

    /// <summary>A direction or a distance carried over: turned and scaled, not moved.</summary>
    public Vector2 Linear(Vector2 vector) => Turn(vector * Scale, Angle);

    /// <summary>A direction or distance on screen back into this one's own space.</summary>
    public Vector2 Unlinear(Vector2 vector) => Turn(vector, -Angle) / Scale;

    /// <summary>This one done after <paramref name="inner"/>.</summary>
    public Similarity After(Similarity inner) => new(Apply(inner.Offset), Angle + inner.Angle, Scale * inner.Scale);

    /// <summary>Turned as the library's <c>rotate</c> turns.</summary>
    public static Vector2 Turn(Vector2 point, float angle)
    {
        (float s, float c) = MathF.SinCos(angle);
        return new Vector2(c * point.X - s * point.Y, s * point.X + c * point.Y);
    }
}

/// <summary>
/// Where a chain's shape is on the preview, worked out from its steps: each run of placement
/// steps (moved, rotated, scaled, anchored) is one placement, and every other step wraps the
/// shape in a new one (mirrored, melted), so the runs nest. A chain whose placement reads a value
/// the CPU can't work out has none.
/// </summary>
public static class ShapePlacement
{
    /// <summary>The shape's own space to the preview's, from its centre; null when a value is unknown.</summary>
    public static Similarity? Of(ShapeChain chain, Vector2 canvas) =>
        Outer(chain, canvas) is { } outer && Runs(chain.Steps, 0, canvas) is { } runs ? outer.After(runs) : null;

    /// <summary>What is done to the first run's result: the later runs, then where the chain itself sits.</summary>
    public static Similarity? AfterFirstRun(ShapeChain chain, Vector2 canvas) =>
        Outer(chain, canvas) is { } outer && Runs(chain.Steps, FirstRunEnd(chain), canvas) is { } runs ? outer.After(runs) : null;

    /// <summary>The steps straight after the constructor that place it, before any wraps it: the ones handles write.</summary>
    public static int FirstRunEnd(ShapeChain chain)
    {
        int end = 0;
        while (end < chain.Steps.Count && IsPlacement(chain.Steps[end]))
            end++;
        return end;
    }

    /// <summary>The first run's step of <paramref name="kind"/>, or null when there isn't one.</summary>
    public static ChainStep? FirstRunStep(ShapeChain chain, HandleKind kind)
    {
        int end = FirstRunEnd(chain);
        for (int i = 0; i < end; i++)
        {
            if (chain.Steps[i].Kind == kind)
                return chain.Steps[i];
        }

        return null;
    }

    private static bool IsPlacement(ChainStep step) => step.Kind is not null || step.Name == "anchored";

    /// <summary>Where the chain sits: inside its parent's shape, or after the parent's steps past the one it was given to.</summary>
    private static Similarity? Outer(ShapeChain chain, Vector2 canvas)
    {
        if (chain.Parent is not { } parent)
            return Similarity.Identity;
        if (chain.ParentStep < 0)
            return Of(parent, canvas);
        return Outer(parent, canvas) is { } outer && Runs(parent.Steps, chain.ParentStep + 1, canvas) is { } runs ? outer.After(runs) : null;
    }

    /// <summary>The steps from <paramref name="from"/> on as one placement, each wrap starting a new one outside the last.</summary>
    private static Similarity? Runs(IReadOnlyList<ChainStep> steps, int from, Vector2 canvas)
    {
        Similarity total = Similarity.Identity;
        Vector2 offset = Vector2.Zero;
        float angle = 0, scale = 1;
        for (int i = from; i < steps.Count; i++)
        {
            ChainStep step = steps[i];
            if (!IsPlacement(step))
            {
                total = new Similarity(offset, angle, scale).After(total);
                (offset, angle, scale) = (Vector2.Zero, 0, 1);
                continue;
            }

            if (step.Arguments is not [{ Value: { } value }])
                return null;
            switch (step.Kind)
            {
                case HandleKind.Move when value.Length == 2:
                    offset += new Vector2(value[0], value[1]);
                    break;
                case HandleKind.Turn when value.Length == 1:
                    angle += value[0];
                    break;
                case HandleKind.Scale when value.Length == 1:
                    scale *= value[0];
                    break;
                case null when value.Length == 2:
                    offset += new Vector2(value[0], value[1]) * canvas;
                    break;
                default:
                    return null;
            }
        }

        return new Similarity(offset, angle, scale).After(total);
    }
}
