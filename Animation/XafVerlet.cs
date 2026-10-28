using Godot;

namespace Xanadu.Animation;

/// <summary>
/// Fixed-step Verlet chain for hair and swinging attachments. Each link is
/// relaxed from both ends, so a fast-moving anchor drags the whole chain along
/// instead of folding links over; relaxing only from the anchor end made fast
/// head movement whip the hair.
/// </summary>
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
    public int ConstraintIterations { get; set; } = 16;

    /// <summary>
    /// A circle the points cannot enter, so the chain rests on it rather than
    /// passing through; radius 0 turns it off. The anchor is not moved.
    /// </summary>
    public Vector2 ColliderCenter { get; set; }
    public float ColliderRadius { get; set; }

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

                // The anchor is fixed, so the first link moves only its free end.
                Vector2 correction = direction * ((length - _segmentLength) / length);
                if (i == 1)
                {
                    Points[i] -= correction;
                    continue;
                }

                Points[i] -= correction * 0.5f;
                Points[i - 1] += correction * 0.5f;
            }

            Points[0] = Anchor;
            PushOutOfCollider();
        }
    }

    /// <summary>
    /// Moves points that are inside the collider out to its edge, along the line
    /// from its centre, and drops the part of their speed that was heading in so
    /// they slide off instead of being thrown. A point dead on the centre goes
    /// up, which is where a chain pulled down would have come from.
    /// </summary>
    private void PushOutOfCollider()
    {
        if (ColliderRadius <= 0f)
            return;

        for (int i = 1; i < Points.Length; i++)
        {
            Vector2 offset = Points[i] - ColliderCenter;
            float distance = offset.Length();
            if (distance >= ColliderRadius)
                continue;

            Vector2 outward = distance > 0.0001f ? offset / distance : Vector2.Up;
            Vector2 velocity = Points[i] - _previous[i];
            float inward = velocity.Dot(outward);
            if (inward < 0f)
                velocity -= outward * inward;

            Points[i] = ColliderCenter + outward * ColliderRadius;
            _previous[i] = Points[i] - velocity;
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
