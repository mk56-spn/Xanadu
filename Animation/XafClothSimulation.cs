using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>
/// Simulates every <see cref="XafCloth"/> of a package on a fixed timestep, as
/// <see cref="XafChainSimulation"/> does chains. Pinned points ride their bones
/// where they sat on them in the rest pose; free points fall, are held to their
/// neighbours by rest distances across, down and diagonally, and resist folding
/// by the cloth's stiffness. Down the sheet the links hold their length, but
/// across it they only stop it bunching up: seen side-on a skirt is a tube,
/// and it can open out as wide as the legs inside it need. Only the two side
/// edges meet the colliders (see the Collision part); the points between stand
/// for its fullness and may overlap them. Settings are re-read every update; a
/// new grid, bone, pins or shape rebuilds the cloth.
/// </summary>
public sealed partial class XafClothSimulation
{
    public const float FixedStep = XafChainSimulation.FixedStep;

    /// <summary>Constraint passes per step; enough for the default grid to hang without stretching.</summary>
    public const int Iterations = 16;

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
        foreach (XafClothTether tether in cloth.Tethers)
        {
            hash.Add(tether.Column);
            hash.Add(tether.Row);
            hash.Add(tether.BoneId);
        }
        foreach (XafBone bone in package.Skeleton.Bones)
            hash.Add(bone.Rest);
        return hash.ToHashCode();
    }

    /// <summary>
    /// How far a link across the sheet may stretch before it pulls back: the
    /// skirt opening out round a kick or a wide stance.
    /// </summary>
    private const float Flare = 1.8f;

    /// <summary>The least share of its rest area a triangle of the mesh is let shrink to before it is pushed open.</summary>
    private const float KeptArea = 0.25f;

    /// <summary>
    /// A rest distance between two points and how hard it is held, 0 to 1. A
    /// slack link only pulls back once stretched past <see cref="Flare"/> times its rest.
    /// </summary>
    private readonly record struct Link(int A, int B, float Rest, float Strength, bool Slack = false);

    private readonly record struct Pinned(int Point, int Bone, Vector2 Offset);

    private readonly record struct Tethered(int Point, int Bone, Vector2 Offset);

    /// <summary>One simulated cloth: its points, the links between them, and what carries and stops them.</summary>
    private sealed partial class Entry
    {
        private readonly Vector2[] _points;
        private readonly Vector2[] _previous;
        private readonly Vector2[] _before;
        private readonly bool[] _fixed;
        private readonly Link[] _links;
        private readonly int[] _triangles;
        private readonly float[] _leastArea;
        private readonly Pinned[] _pins;
        private readonly Tethered[] _tethers;
        private float[] _tetherRadii = Array.Empty<float>();
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
            (_edges, _opposite) = Edges(cloth);

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

            var tethers = new List<Tethered>();
            foreach (XafClothTether tether in cloth.Tethers)
            {
                int bone = XafRig.FindBone(package, tether.BoneId);
                if (bone < 0 || bone >= rest.Length || tether.Column < 0 || tether.Column >= cloth.Columns ||
                    tether.Row < 0 || tether.Row >= cloth.Rows)
                {
                    continue;
                }

                int point = cloth.Index(tether.Column, tether.Row);
                if (!_fixed[point])
                    tethers.Add(new Tethered(point, bone, rest[bone].AffineInverse() * _points[point]));
            }
            _tethers = tethers.ToArray();
            _links = Links(cloth, _points);
            _triangles = XafClothLayout.Triangles(cloth.Columns, cloth.Rows);
            _leastArea = new float[_triangles.Length / 3];
            for (int t = 0; t < _leastArea.Length; t++)
                _leastArea[t] = Area(_triangles[t * 3], _triangles[t * 3 + 1], _triangles[t * 3 + 2]) * KeptArea;
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
            ReadCapsules(package, cloth, world);
            ReadTetherRadii(cloth);
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
                Unfold();
                Collide();
                Tether();
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

            if (link.Slack && length >= link.Rest && length <= link.Rest * Flare)
                return;

            float rest = link.Slack && length > link.Rest ? link.Rest * Flare : link.Rest;
            float strength = link.Strength < 0f ? -link.Strength * BendStrength : link.Strength;
            Vector2 correction = along * ((length - rest) / length * strength);
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

        private float Area(int a, int b, int c) =>
            0.5f * (_points[b] - _points[a]).Cross(_points[c] - _points[a]);

        /// <summary>
        /// Stops the sheet folding through itself, which side-on reads as a
        /// glitch rather than a fold: each triangle of the mesh keeps its
        /// winding and at least <see cref="KeptArea"/> of its rest area, its
        /// corners pushed apart along the directions that grow it fastest.
        /// </summary>
        private void Unfold()
        {
            for (int t = 0; t < _leastArea.Length; t++)
            {
                int a = _triangles[t * 3];
                int b = _triangles[t * 3 + 1];
                int c = _triangles[t * 3 + 2];
                float shortfall = _leastArea[t] - Area(a, b, c);
                if (shortfall <= 0f)
                    continue;

                Vector2 ab = _points[b] - _points[a];
                Vector2 ac = _points[c] - _points[a];
                var growB = new Vector2(ac.Y, -ac.X) * 0.5f;
                var growC = new Vector2(-ab.Y, ab.X) * 0.5f;
                Vector2 growA = -(growB + growC);
                float wa = _fixed[a] ? 0f : 1f;
                float wb = _fixed[b] ? 0f : 1f;
                float wc = _fixed[c] ? 0f : 1f;
                float sum = wa * growA.LengthSquared() + wb * growB.LengthSquared() + wc * growC.LengthSquared();
                if (sum <= 1e-8f)
                    continue;

                float step = shortfall / sum;
                _points[a] += growA * (wa * step);
                _points[b] += growB * (wb * step);
                _points[c] += growC * (wc * step);
            }
        }

        /// <summary>Radii in the same order as the tethers kept, read live so the inspector's field acts at once.</summary>
        private void ReadTetherRadii(XafCloth cloth)
        {
            if (_tetherRadii.Length != _tethers.Length)
                _tetherRadii = new float[_tethers.Length];
            int next = 0;
            foreach (XafClothTether tether in cloth.Tethers)
            {
                if (next >= _tethers.Length)
                    break;
                if (cloth.Index(tether.Column, tether.Row) == _tethers[next].Point)
                    _tetherRadii[next++] = Math.Max(0f, tether.Radius);
            }
        }

        /// <summary>
        /// Pulls a tethered point that strayed too far from its spot on the
        /// bone back to the edge of its reach, keeping the part of its speed
        /// that runs along that edge so it slides round rather than stopping dead.
        /// </summary>
        private void Tether()
        {
            for (int i = 0; i < _tethers.Length; i++)
            {
                Tethered tether = _tethers[i];
                if (tether.Bone >= _world.Length)
                    continue;

                Vector2 anchor = _world[tether.Bone] * tether.Offset;
                Vector2 offset = _points[tether.Point] - anchor;
                float distance = offset.Length();
                float radius = _tetherRadii[i];
                if (distance <= radius)
                    continue;

                Vector2 outward = distance > 0.0001f ? offset / distance : Vector2.Down;
                Vector2 velocity = _points[tether.Point] - _previous[tether.Point];
                float away = velocity.Dot(outward);
                if (away > 0f)
                    velocity -= outward * away;
                _points[tether.Point] = anchor + outward * radius;
                _previous[tether.Point] = _points[tether.Point] - velocity;
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
            ContainDrawn();
        }

        /// <summary>
        /// The links: neighbours down at full strength, neighbours across and
        /// the two diagonals of each cell as slack links that stop it bunching
        /// or shearing but let it open out, and every second point across and
        /// down as bend links, whose strength is the stiffness (stored negative
        /// so <see cref="Relax"/> can tell them apart).
        /// </summary>
        private static Link[] Links(XafCloth cloth, Vector2[] points)
        {
            var links = new List<Link>();
            void Add(int column, int row, int toColumn, int toRow, float strength, bool slack = true)
            {
                if (toColumn < 0 || toColumn >= cloth.Columns || toRow >= cloth.Rows)
                    return;

                int a = cloth.Index(column, row);
                int b = cloth.Index(toColumn, toRow);
                links.Add(new Link(a, b, points[a].DistanceTo(points[b]), strength, slack));
            }

            for (int row = 0; row < cloth.Rows; row++)
            {
                for (int column = 0; column < cloth.Columns; column++)
                {
                    Add(column, row, column + 1, row, 1f);
                    Add(column, row, column, row + 1, 1f, slack: false);
                    Add(column, row, column + 1, row + 1, 0.7f);
                    Add(column, row, column - 1, row + 1, 0.7f);
                    Add(column, row, column + 2, row, -0.5f);
                    Add(column, row, column, row + 2, -0.5f, slack: false);
                }
            }
            return links.ToArray();
        }
    }
}
