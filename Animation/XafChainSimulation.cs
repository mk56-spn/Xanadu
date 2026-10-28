using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>
/// Simulates every <see cref="XafChain"/> of a package. Each chain's first
/// point follows its bone; the rest swing on a fixed timestep so they move the
/// same at any frame rate. A chain with a collider rests on it instead of
/// passing through. Settings are re-read on every update, so editing a
/// chain changes it live; only a new bone, segment count or length rebuilds it.
/// </summary>
public sealed class XafChainSimulation
{
    public const float FixedStep = 1f / 60f;

    /// <summary>Steps a new chain takes before it is first seen, so it starts at rest and not mid-fall.</summary>
    private const int SettleSteps = 180;

    /// <summary>Longest stretch of time caught up in one update, so a stall does not stall again.</summary>
    private const float MaxCatchUp = 0.25f;

    private readonly List<Entry> _entries = new();
    private float _pending;

    /// <summary>
    /// The points of a chain to draw, anchor first; empty before the first update
    /// or for a bad index. They are blended between the last two fixed steps, so
    /// the chain moves smoothly at any frame rate, and the first point is always
    /// exactly on its bone, so the hair never lags the head it grows from.
    /// </summary>
    public IReadOnlyList<Vector2> Points(int chain) =>
        chain >= 0 && chain < _entries.Count
            ? _entries[chain].Render
            : Array.Empty<Vector2>();

    /// <summary>Drops all motion; the chains hang from their bones again on the next update.</summary>
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
                entry.Verlet.Step(FixedStep);
            }
        }

        float blend = _pending / FixedStep;
        foreach (Entry entry in _entries)
            entry.Blend(blend);
    }

    /// <summary>The world position of a chain's first point, or null when its bone is missing.</summary>
    public static Vector2? Anchor(XafPackage package, XafChain chain, Transform2D[] worldBones)
    {
        int bone = XafRig.FindBone(package, chain.BoneId);
        return bone >= 0 && bone < worldBones.Length
            ? worldBones[bone] * chain.Offset
            : null;
    }

    /// <summary>The world position of a chain's collider, or null when it has none or its bone is missing.</summary>
    public static Vector2? Collider(XafPackage package, XafChain chain, Transform2D[] worldBones)
    {
        int bone = XafRig.FindBone(package, chain.CollideBoneId);
        return chain.HasCollider && bone >= 0 && bone < worldBones.Length
            ? worldBones[bone] * chain.CollideOffset
            : null;
    }

    private void Sync(XafPackage package, Transform2D[] worldBones)
    {
        if (_entries.Count > package.Chains.Count)
            _entries.RemoveRange(package.Chains.Count, _entries.Count - package.Chains.Count);

        for (int i = 0; i < package.Chains.Count; i++)
        {
            XafChain chain = package.Chains[i];

            // A chain on a missing bone is invalid, but keep the entries aligned with the list.
            Vector2 anchor = Anchor(package, chain, worldBones) ?? Vector2.Zero;
            bool created = i >= _entries.Count || !_entries[i].Matches(chain);
            if (i >= _entries.Count)
                _entries.Add(Create(chain, anchor));
            else if (created)
                _entries[i] = Create(chain, anchor);

            XafVerletChain verlet = _entries[i].Verlet;
            verlet.Anchor = anchor;
            verlet.Gravity = new Vector2(chain.Lean, chain.Gravity);
            verlet.Damping = chain.Damping;
            verlet.ColliderRadius = chain.HasCollider ? chain.CollideRadius : 0f;
            if (chain.HasCollider)
                verlet.ColliderCenter = Collider(package, chain, worldBones) ?? anchor;

            if (created)
            {
                for (int step = 0; step < SettleSteps; step++)
                    verlet.Step(FixedStep);
                _entries[i].Snap();
            }
        }
    }

    /// <summary>
    /// Starts the chain along the way gravity and lean pull it, which is where it
    /// would settle, so it does not begin by falling through whatever it hangs over.
    /// </summary>
    private static Entry Create(XafChain chain, Vector2 anchor) =>
        new(chain, new XafVerletChain(
            chain.Segments,
            anchor,
            new Vector2(chain.Lean, chain.Gravity),
            chain.SegmentLength));

    /// <summary>One simulated chain and the shape it was built for.</summary>
    private sealed class Entry(XafChain chain, XafVerletChain verlet)
    {
        private readonly string _id = chain.Id;
        private readonly string _bone = chain.BoneId;
        private readonly int _segments = chain.Segments;
        private readonly float _length = chain.SegmentLength;

        public XafVerletChain Verlet { get; } = verlet;

        /// <summary>The points before the last fixed step, and the blend of them with the latest that is drawn.</summary>
        private readonly Vector2[] _previous = (Vector2[])verlet.Points.Clone();

        public Vector2[] Render { get; } = (Vector2[])verlet.Points.Clone();

        public void Remember() => Array.Copy(Verlet.Points, _previous, _previous.Length);

        /// <summary>Makes the drawn points the simulated ones, with no motion in between.</summary>
        public void Snap()
        {
            Array.Copy(Verlet.Points, _previous, _previous.Length);
            Array.Copy(Verlet.Points, Render, Render.Length);
        }

        /// <summary>
        /// Draws the chain between its last two steps, carried along by however far
        /// the anchor has moved since, so the root sits exactly on its bone and the
        /// links keep their length however fast the bone goes.
        /// </summary>
        public void Blend(float amount)
        {
            Vector2 carry = Verlet.Anchor - _previous[0].Lerp(Verlet.Points[0], amount);
            for (int i = 0; i < Render.Length; i++)
                Render[i] = _previous[i].Lerp(Verlet.Points[i], amount) + carry;
        }

        public bool Matches(XafChain other) =>
            other.Id == _id && other.BoneId == _bone &&
            other.Segments == _segments && other.SegmentLength == _length;
    }
}
