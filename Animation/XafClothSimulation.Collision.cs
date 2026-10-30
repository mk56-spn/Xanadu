using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

public sealed partial class XafClothSimulation
{
    private readonly record struct Capsule(Vector2 From, Vector2 To, float Radius);

    /// <summary>Share of its speed along the leg a touching point loses per pass, so a skirt rests on a raised leg instead of sliding off.</summary>
    private const float Friction = 0.2f;

    /// <summary>
    /// How the side edges meet the colliders. Seen side-on, a dress keeps the
    /// legs inside its outline, so each edge point remembers which side of each
    /// leg it is on, and is put back on that side when the leg moves into it or
    /// sweeps right past it in one frame, as a fast kick does. A kick
    /// therefore lifts the front edge and the leg comes out from under the hem,
    /// not through the side. An edge point with no side yet (the cloth was made
    /// mid-kick) takes the side facing away from the other edge.
    /// On top of that, every row's two edge points must enclose each leg the
    /// row's line crosses (<see cref="Entry.Contain()"/>), which needs no memory
    /// and is applied again to the drawn points, so a leg never shows through
    /// the side of the skirt whatever happened in between.
    /// </summary>
    private sealed partial class Entry
    {
        private readonly int[] _edges;

        /// <summary>For each edge point, the point at the other end of its row.</summary>
        private readonly int[] _opposite;

        private readonly List<Capsule> _capsules = new();

        /// <summary>Per edge point and capsule: -1 or 1 for the side of the leg it was last clear on, 0 if never.</summary>
        private sbyte[] _sides = Array.Empty<sbyte>();

        private static (int[] Edges, int[] Opposite) Edges(XafCloth cloth)
        {
            var edges = new int[cloth.Rows * 2];
            var opposite = new int[edges.Length];
            for (int row = 0; row < cloth.Rows; row++)
            {
                int left = cloth.Index(0, row);
                int right = cloth.Index(cloth.Columns - 1, row);
                edges[row * 2] = left;
                opposite[row * 2] = right;
                edges[row * 2 + 1] = right;
                opposite[row * 2 + 1] = left;
            }
            return (edges, opposite);
        }

        private void ReadCapsules(XafPackage package, XafCloth cloth, Transform2D[] world)
        {
            _capsules.Clear();
            foreach (XafClothCollider collider in cloth.Colliders)
            {
                if (collider.Radius > 0f && XafClothLayout.Capsule(package, collider, world) is { } capsule)
                    _capsules.Add(new Capsule(capsule.From, capsule.To, collider.Radius));
            }

            int sides = _edges.Length * _capsules.Count;
            if (_sides.Length != sides)
                _sides = new sbyte[sides];
        }

        private void Collide()
        {
            Contain();

            for (int c = 0; c < _capsules.Count; c++)
            {
                for (int e = 0; e < _edges.Length; e++)
                {
                    int i = _edges[e];
                    if (!_fixed[i])
                        Collide(_capsules[c], e * _capsules.Count + c, i, _opposite[e]);
                }
            }
        }

        private void Collide(Capsule capsule, int key, int i, int opposite)
        {
            Vector2 point = _points[i];
            Vector2 along = capsule.To - capsule.From;
            float length = along.Length();
            Vector2 target;
            Vector2 outward;
            if (length > 0.0001f)
            {
                Vector2 axis = along / length;
                var normal = new Vector2(-axis.Y, axis.X);
                float t = (point - capsule.From).Dot(axis);
                float side = (point - capsule.From).Dot(normal);
                if (t >= 0f && t <= length)
                {
                    int now = side < 0f ? -1 : 1;
                    bool clear = MathF.Abs(side) >= capsule.Radius;
                    int remembered = _sides[key];
                    if (remembered == 0)
                    {
                        remembered = clear ? now : Away(i, opposite, normal, side);
                        _sides[key] = (sbyte)remembered;
                    }

                    // On its own side and clear, or too far over for the leg to
                    // have swept past it, the point is left be; otherwise the leg
                    // went through it this frame and it goes back on top.
                    if (clear && (now == remembered || MathF.Abs(side) > length))
                    {
                        _sides[key] = (sbyte)now;
                        return;
                    }

                    outward = normal * remembered;
                    target = capsule.From + axis * t + outward * capsule.Radius;
                    Resolve(i, target, outward);
                    return;
                }

                // Past either end the leg is a round cap, and the edge may slip
                // round it to the other side, so the side is learnt afresh.
                Vector2 end = t < 0f ? capsule.From : capsule.To;
                if (point.DistanceTo(end) >= capsule.Radius)
                {
                    _sides[key] = (sbyte)(side < 0f ? -1 : 1);
                    return;
                }

                outward = Outward(point - end, i, opposite);
                Resolve(i, end + outward * capsule.Radius, outward);
                return;
            }

            if (point.DistanceTo(capsule.From) >= capsule.Radius)
                return;
            outward = Outward(point - capsule.From, i, opposite);
            Resolve(i, capsule.From + outward * capsule.Radius, outward);
        }

        /// <summary>
        /// Keeps every leg inside the outline row by row: along the line
        /// through a row's two edge points, each capsule the line crosses must
        /// lie between them, so an edge left short is pushed out past the leg.
        /// Needs no memory, so it holds however far a leg moved in one frame.
        /// </summary>
        private void Contain() => Contain(_points, simulated: true);

        /// <summary>
        /// The same rule on the points as drawn, against this frame's bones:
        /// the solver steps at a fixed rate and can trail a fast kick by a
        /// frame, and a frame is all it takes for a knee to show.
        /// </summary>
        private void ContainDrawn() => Contain(Render, simulated: false);

        private void Contain(Vector2[] points, bool simulated)
        {
            for (int e = 0; e < _edges.Length; e += 2)
            {
                int left = _edges[e];
                int right = _edges[e + 1];
                Vector2 from = points[left];
                Vector2 across = points[right] - from;
                float width = across.Length();
                if (width <= 0.0001f)
                    continue;

                Vector2 axis = across / width;
                float least = float.MaxValue;
                float most = float.MinValue;
                foreach (Capsule capsule in _capsules)
                {
                    if (Crossing(from, axis, capsule) is not { } span)
                        continue;
                    least = MathF.Min(least, span.Least);
                    most = MathF.Max(most, span.Most);
                }

                if (least == float.MaxValue)
                    continue;
                if (least < 0f && !_fixed[left])
                    Place(points, simulated, left, from + axis * least, -axis);
                if (most > width && !_fixed[right])
                    Place(points, simulated, right, from + axis * most, axis);
            }
        }

        private void Place(Vector2[] points, bool simulated, int i, Vector2 target, Vector2 outward)
        {
            if (simulated)
                Resolve(i, target, outward);
            else
                points[i] = target;
        }

        /// <summary>Where the line from <paramref name="from"/> along <paramref name="axis"/> runs inside the capsule, or null.</summary>
        private static (float Least, float Most)? Crossing(Vector2 from, Vector2 axis, Capsule capsule)
        {
            float least = float.MaxValue;
            float most = float.MinValue;
            void Circle(Vector2 centre)
            {
                Vector2 offset = from - centre;
                float b = offset.Dot(axis);
                float c = offset.LengthSquared() - capsule.Radius * capsule.Radius;
                float disc = b * b - c;
                if (disc < 0f)
                    return;
                float root = MathF.Sqrt(disc);
                least = MathF.Min(least, -b - root);
                most = MathF.Max(most, -b + root);
            }

            Circle(capsule.From);
            Circle(capsule.To);

            Vector2 along = capsule.To - capsule.From;
            float length = along.Length();
            if (length > 0.0001f)
            {
                Vector2 core = along / length;
                var normal = new Vector2(-core.Y, core.X);
                float lo = float.MinValue;
                float hi = float.MaxValue;
                bool Clip(float start, float rate, float min, float max)
                {
                    if (MathF.Abs(rate) < 1e-6f)
                        return start >= min && start <= max;
                    float a = (min - start) / rate;
                    float b = (max - start) / rate;
                    lo = MathF.Max(lo, MathF.Min(a, b));
                    hi = MathF.Min(hi, MathF.Max(a, b));
                    return lo <= hi;
                }

                Vector2 offset = from - capsule.From;
                if (Clip(offset.Dot(core), axis.Dot(core), 0f, length) &&
                    Clip(offset.Dot(normal), axis.Dot(normal), -capsule.Radius, capsule.Radius))
                {
                    least = MathF.Min(least, lo);
                    most = MathF.Max(most, hi);
                }
            }

            return least <= most ? (least, most) : null;
        }

        /// <summary>The side of the leg facing away from the row's other edge, for an edge point with no side yet.</summary>
        private int Away(int i, int opposite, Vector2 normal, float side)
        {
            float away = (_points[i] - _points[opposite]).Dot(normal);
            if (MathF.Abs(away) > 0.0001f)
                return away < 0f ? -1 : 1;
            return side < 0f ? -1 : 1;
        }

        private Vector2 Outward(Vector2 offset, int i, int opposite)
        {
            float distance = offset.Length();
            if (distance > 0.0001f)
                return offset / distance;
            Vector2 away = _points[i] - _points[opposite];
            return away.LengthSquared() > 0.0001f ? away.Normalized() : Vector2.Left;
        }

        /// <summary>
        /// Puts the point on the leg's surface, drops the part of its speed
        /// heading in so it does not bounce off, and brakes the part along the
        /// leg, so the cloth drags over the leg instead of skating.
        /// </summary>
        private void Resolve(int i, Vector2 target, Vector2 outward)
        {
            Vector2 velocity = _points[i] - _previous[i];
            float inward = velocity.Dot(outward);
            if (inward < 0f)
                velocity -= outward * inward;
            Vector2 normal = outward * velocity.Dot(outward);
            velocity = normal + (velocity - normal) * (1f - Friction);

            _points[i] = target;
            _previous[i] = target - velocity;
        }
    }
}
