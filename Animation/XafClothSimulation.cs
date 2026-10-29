using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>
/// Simulates every <see cref="XafCloth"/> of a package on a fixed timestep, as
/// <see cref="XafChainSimulation"/> does chains. Pinned points ride their bones
/// where they sat on them in the rest pose; free points fall, are held to their
/// neighbours by rest distances across, down and diagonally, and resist folding
/// by the cloth's stiffness. Seen side-on, a dress holds the legs inside its
/// outline, so only its two side edges are pushed out of the colliders; the
/// points between stand for its fullness and may overlap them. Settings are
/// re-read every update; a new grid, bone, pins or shape rebuilds the cloth.
/// </summary>
public sealed class XafClothSimulation
{
    public const float FixedStep = XafChainSimulation.FixedStep;

    /// <summary>Constraint passes per step; enough for the default grid to hang without stretching.</summary>
    public const int Iterations = 12;

    /// <summary>Steps a new cloth takes before it is first seen, so it starts hanging and not mid-fall.</summary>
    private const int SettleSteps = 180;

    private const float MaxCatchUp = 0.25f;

    private readonly List<Entry> _entries = new();
    private float _pending;

    /// <summary>The points to draw, row by row from the top edge; empty before the first update or for a bad index.</summary>
    public IReadOnlyList<Vector2> Points(int cloth) =>
        cloth >= 0 && cloth < _entries.Count ? _entries[cloth].Render : Array.Empty<Vector2>();

    /// <summary>Drops all motion; the cloths hang from their bones again on the next update.</summary>
    public void Reset()
    {
        _entries.Clear();
        _pending = 0f;
    }

    public void Update(XafPackage package, Transform2D[] worldBones, float delta)
    {
        Sync(package, worldBones);
        _pending = Math.Min(_pending + Math.Max(0f, delta), MaxCatchUp);
        while (_pending >= FixedStep)
        {
            _pending -= FixedStep;
            foreach (Entry entry in _entries)
            {
                entry.Remember();
                entry.Step(FixedStep);
            }
        }

        float blend = _pending / FixedStep;
        foreach (Entry entry in _entries)
            entry.Blend(blend);
    }

    private void Sync(XafPackage package, Transform2D[] worldBones)
    {
        if (_entries.Count > package.Cloths.Count)
            _entries.RemoveRange(package.Cloths.Count, _entries.Count - package.Cloths.Count);

        Transform2D[]? rest = null;
        for (int i = 0; i < package.Cloths.Count; i++)
        {
            XafCloth cloth = package.Cloths[i];
            int shape = Shape(package, cloth);
            bool created = i >= _entries.Count || _entries[i].ShapeHash != shape;
            if (created)
            {
                rest ??= XafRig.WorldBones(package, XafClothLayout.RestPose(package));
                var entry = new Entry(package, cloth, rest, shape);
                if (i >= _entries.Count)
                    _entries.Add(entry);
                else
                    _entries[i] = entry;
            }

            Entry current = _entries[i];
            current.Configure(package, cloth, worldBones);
            if (created)
            {
                current.Settle(SettleSteps, FixedStep);
                current.Snap();
            }
        }
    }

    /// <summary>Everything that decides the grid and its rest distances; a change rebuilds the cloth.</summary>
    private static int Shape(XafPackage package, XafCloth cloth)
    {
        var hash = new HashCode();
        hash.Add(cloth.Id);
        hash.Add(cloth.BoneId);
        hash.Add(cloth.Offset);
        hash.Add(cloth.Columns);
        hash.Add(cloth.Rows);
        hash.Add(cloth.TopWidth);
        hash.Add(cloth.HemWidth);
        hash.Add(cloth.Length);
        foreach (XafClothPin pin in cloth.Pins)
        {
            hash.Add(pin.Column);
            hash.Add(pin.Row);
            hash.Add(pin.BoneId);
        }
        foreach (XafBone bone in package.Skeleton.Bones)
            hash.Add(bone.Rest);
        return hash.ToHashCode();
    }

    /// <summary>A rest distance between two points and how hard it is held, 0 to 1.</summary>
    private readonly record struct Link(int A, int B, float Rest, float Strength);

    private readonly record struct Pinned(int Point, int Bone, Vector2 Offset);

    private readonly record struct Capsule(Vector2 From, Vector2 To, float Radius);

    /// <summary>One simulated cloth: its points, the links between them, and what carries and stops them.</summary>
    private sealed class Entry
    {
        private readonly Vector2[] _points;
        private readonly Vector2[] _previous;
        private readonly Vector2[] _before;
        private readonly bool[] _fixed;
        private readonly int[] _edges;
        private readonly Link[] _links;
        private readonly Pinned[] _pins;
        private readonly List<Capsule> _capsules = new();
        private Transform2D[] _world = Array.Empty<Transform2D>();
        private Vector2 _gravity;
        private float _damping;

        public Entry(XafPackage package, XafCloth cloth, Transform2D[] rest, int shape)
        {
            ShapeHash = shape;
            _points = XafClothLayout.RestPoints(package, cloth, rest);
            _previous = (Vector2[])_points.Clone();
            _before = (Vector2[])_points.Clone();
            Render = (Vector2[])_points.Clone();
            _fixed = new bool[_points.Length];
            var edges = new List<int>();
            for (int row = 0; row < cloth.Rows; row++)
            {
                edges.Add(cloth.Index(0, row));
                edges.Add(cloth.Index(cloth.Columns - 1, row));
            }
            _edges = edges.ToArray();

            var pins = new List<Pinned>();
            foreach (XafClothPin pin in cloth.Pins)
            {
                int bone = XafRig.FindBone(package, pin.BoneId);
                if (bone < 0 || bone >= rest.Length || pin.Column < 0 || pin.Column >= cloth.Columns ||
                    pin.Row < 0 || pin.Row >= cloth.Rows)
                {
                    continue;
                }

                int point = cloth.Index(pin.Column, pin.Row);
                pins.Add(new Pinned(point, bone, rest[bone].AffineInverse() * _points[point]));
                _fixed[point] = true;
            }
            _pins = pins.ToArray();
            _links = Links(cloth, _points);
        }

        public int ShapeHash { get; }

        public Vector2[] Render { get; }

        /// <summary>Reads the settings and the colliders for this pose; pins follow the bones on the next step.</summary>
        public void Configure(XafPackage package, XafCloth cloth, Transform2D[] world)
        {
            _world = world;
            _gravity = new Vector2(cloth.Lean, cloth.Gravity);
            _damping = Math.Clamp(cloth.Damping, 0f, 1f);
            BendStrength = Math.Clamp(cloth.Stiffness, 0f, 1f);
            _capsules.Clear();
            foreach (XafClothCollider collider in cloth.Colliders)
            {
                if (collider.Radius > 0f && XafClothLayout.Capsule(package, collider, world) is { } capsule)
                    _capsules.Add(new Capsule(capsule.From, capsule.To, collider.Radius));
            }
        }

        private float BendStrength { get; set; }

        public void Settle(int steps, float delta)
        {
            for (int i = 0; i < steps; i++)
                Step(delta);
        }

        public void Step(float delta)
        {
            float dt2 = delta * delta;
            for (int i = 0; i < _points.Length; i++)
            {
                if (_fixed[i])
                    continue;

                Vector2 current = _points[i];
                Vector2 velocity = (current - _previous[i]) * _damping;
                _previous[i] = current;
                _points[i] = current + velocity + _gravity * dt2;
            }

            PlacePins();
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                foreach (Link link in _links)
                    Relax(link);
                Collide();
            }
        }

        private void PlacePins()
        {
            foreach (Pinned pin in _pins)
            {
                if (pin.Bone >= _world.Length)
                    continue;

                Vector2 at = _world[pin.Bone] * pin.Offset;
                _previous[pin.Point] = _points[pin.Point];
                _points[pin.Point] = at;
            }
        }

        /// <summary>
        /// Moves both ends towards the rest distance; a pinned end does not move,
        /// so its free neighbour takes the whole correction. Bend links are
        /// scaled by the stiffness, so a soft cloth folds and a stiff one holds out.
        /// </summary>
        private void Relax(Link link)
        {
            bool fixedA = _fixed[link.A];
            bool fixedB = _fixed[link.B];
            if (fixedA && fixedB)
                return;

            Vector2 along = _points[link.B] - _points[link.A];
            float length = along.Length();
            if (length <= 0.0001f)
                return;

            float strength = link.Strength < 0f ? -link.Strength * BendStrength : link.Strength;
            Vector2 correction = along * ((length - link.Rest) / length * strength);
            if (fixedA)
                _points[link.B] -= correction;
            else if (fixedB)
                _points[link.A] += correction;
            else
            {
                _points[link.A] += correction * 0.5f;
                _points[link.B] -= correction * 0.5f;
            }
        }

        /// <summary>
        /// Moves free edge points inside a capsule out to its surface, along the line
        /// from the closest point of its core, and drops the part of their speed
        /// heading in, so the cloth slides over a leg instead of bouncing off it.
        /// </summary>
        private void Collide()
        {
            foreach (Capsule capsule in _capsules)
            {
                foreach (int i in _edges)
                {
                    if (_fixed[i])
                        continue;

                    Vector2 core = XafClothLayout.ClosestOnSegment(_points[i], capsule.From, capsule.To);
                    Vector2 offset = _points[i] - core;
                    float distance = offset.Length();
                    if (distance >= capsule.Radius)
                        continue;

                    Vector2 outward = distance > 0.0001f ? offset / distance : Vector2.Left;
                    Vector2 velocity = _points[i] - _previous[i];
                    float inward = velocity.Dot(outward);
                    if (inward < 0f)
                        velocity -= outward * inward;

                    _points[i] = core + outward * capsule.Radius;
                    _previous[i] = _points[i] - velocity;
                }
            }
        }

        public void Remember() => Array.Copy(_points, _before, _points.Length);

        public void Snap()
        {
            Array.Copy(_points, _before, _points.Length);
            Array.Copy(_points, Render, Render.Length);
        }

        /// <summary>
        /// Draws the cloth between its last two steps; pinned points are put
        /// exactly where their bones are now, so the bodice never lags the body.
        /// </summary>
        public void Blend(float amount)
        {
            for (int i = 0; i < Render.Length; i++)
                Render[i] = _before[i].Lerp(_points[i], amount);
            foreach (Pinned pin in _pins)
            {
                if (pin.Bone < _world.Length)
                    Render[pin.Point] = _world[pin.Bone] * pin.Offset;
            }
        }

        /// <summary>
        /// The links: neighbours across and down at full strength, the two
        /// diagonals of each cell to stop it shearing, and every second point
        /// across and down as bend links, whose strength is the stiffness
        /// (stored negative so <see cref="Relax"/> can tell them apart).
        /// </summary>
        private static Link[] Links(XafCloth cloth, Vector2[] points)
        {
            var links = new List<Link>();
            void Add(int column, int row, int toColumn, int toRow, float strength)
            {
                if (toColumn < 0 || toColumn >= cloth.Columns || toRow >= cloth.Rows)
                    return;

                int a = cloth.Index(column, row);
                int b = cloth.Index(toColumn, toRow);
                links.Add(new Link(a, b, points[a].DistanceTo(points[b]), strength));
            }

            for (int row = 0; row < cloth.Rows; row++)
            {
                for (int column = 0; column < cloth.Columns; column++)
                {
                    Add(column, row, column + 1, row, 1f);
                    Add(column, row, column, row + 1, 1f);
                    Add(column, row, column + 1, row + 1, 0.7f);
                    Add(column, row, column - 1, row + 1, 0.7f);
                    Add(column, row, column + 2, row, -0.5f);
                    Add(column, row, column, row + 2, -0.5f);
                }
            }
            return links.ToArray();
        }
    }
}
