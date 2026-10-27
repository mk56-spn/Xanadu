using Godot;

namespace Xanadu.Animation;

/// <summary>Fixed-step Verlet chain for hair and swinging attachments.</summary>
public sealed class XafVerletChain
{
    private readonly Vector2[] _previous;
    private readonly float _segmentLength;

    public XafVerletChain(
        int segments,
        Vector2 anchor,
        Vector2 direction,
        float segmentLength)
    {
        Anchor = anchor;
        _segmentLength = Mathf.Max(0.001f, segmentLength);
        Points = new Vector2[segments + 1];
        _previous = new Vector2[segments + 1];

        Vector2 axis = direction.LengthSquared() > 0.0001f
            ? direction.Normalized()
            : Vector2.Down;
        for (int i = 0; i < Points.Length; i++)
        {
            Points[i] = anchor + axis * (_segmentLength * i);
            _previous[i] = Points[i];
        }
    }

    public Vector2 Anchor { get; set; }
    public Vector2[] Points { get; }
    public Vector2 Gravity { get; set; } = new(0, 900);
    public float Damping { get; set; } = 0.96f;
    public int ConstraintIterations { get; set; } = 8;

    public void Step(float delta)
    {
        if (delta <= 0f)
            return;

        float dt2 = delta * delta;
        for (int i = 1; i < Points.Length; i++)
        {
            Vector2 current = Points[i];
            Vector2 velocity = (current - _previous[i]) * Damping;
            _previous[i] = current;
            Points[i] = current + velocity + Gravity * dt2;
        }

        Points[0] = Anchor;
        for (int iteration = 0; iteration < ConstraintIterations; iteration++)
        {
            for (int i = 1; i < Points.Length; i++)
            {
                Vector2 direction = Points[i] - Points[i - 1];
                float length = direction.Length();
                if (length <= 0.0001f)
                    continue;
                Points[i] = Points[i - 1] +
                            direction / length * _segmentLength;
            }
        }
    }

    public void StepFixed(float delta, float fixedStep = 1f / 60f)
    {
        if (fixedStep <= 0f)
            return;

        int steps = Mathf.Max(1, Mathf.RoundToInt(delta / fixedStep));
        for (int i = 0; i < steps; i++)
            Step(fixedStep);
    }

    public void Reset(Vector2 anchor)
    {
        Anchor = anchor;
        for (int i = 0; i < Points.Length; i++)
            Points[i] = anchor;
        System.Array.Copy(Points, _previous, Points.Length);
    }
}

/// <summary>Single pendulum-like attachment that swings from a bone.</summary>
public sealed class XafPendulum
{
    private Vector2 _previous;

    public XafPendulum(Vector2 anchor, Vector2 position, float length)
    {
        Anchor = anchor;
        Position = position;
        _previous = position;
        Length = Mathf.Max(0.001f, length);
    }

    public Vector2 Anchor { get; set; }
    public Vector2 Position { get; private set; }
    public float Length { get; set; }
    public Vector2 Gravity { get; set; } = new(0, 900);
    public float Damping { get; set; } = 0.97f;

    public void Step(float delta)
    {
        if (delta <= 0f)
            return;

        Vector2 velocity = (Position - _previous) * Damping;
        _previous = Position;
        Position += velocity + Gravity * delta * delta;

        Vector2 direction = Position - Anchor;
        float distance = direction.Length();
        if (distance > 0.0001f)
            Position = Anchor + direction / distance * Length;
    }

    public void StepFixed(float delta, float fixedStep = 1f / 60f)
    {
        if (fixedStep <= 0f)
            return;

        int steps = Mathf.Max(1, Mathf.RoundToInt(delta / fixedStep));
        for (int i = 0; i < steps; i++)
            Step(fixedStep);
    }
}
