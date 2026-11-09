using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Particles;

namespace Xanadu.Systems.Particles;

/// <summary>
/// Plays one-off bursts of the library's particles. Each effect gets a small
/// pool of <see cref="ParticleInstance"/>s, made the first time it goes off, so
/// bursts overlap without cutting each other off. The pools are dropped when
/// the library changes and are remade from the new effects.
/// </summary>
public sealed class GodotParticleEmitter : IParticleEmitter, IDisposable
{
    private const int PoolSize = 10;

    private readonly Rid _parent;
    private readonly ParticleKit _kit = new();
    private readonly Dictionary<string, Pool> _pools = new(StringComparer.Ordinal);

    public GodotParticleEmitter(Rid parentCanvas)
    {
        _parent = parentCanvas;
        ParticleLibrary.Changed += Drop;
    }

    public void Burst(
        string particle,
        Vector2 position,
        Color tint,
        Vector2 area = default,
        float angle = 0f,
        Vector2 heading = default)
    {
        if (!_pools.TryGetValue(particle, out Pool? pool))
        {
            XpfParticle? effect = ParticleLibrary.Find(particle);
            if (effect is null)
                return;

            pool = new Pool(new ParticleInstance[PoolSize]);
            for (int i = 0; i < PoolSize; i++)
                pool.Instances[i] = new ParticleInstance(_kit, _parent, effect);
            _pools[particle] = pool;
        }

        ParticleInstance instance = pool.Instances[pool.Next];
        pool.Next = (pool.Next + 1) % pool.Instances.Length;
        instance.Burst(position, tint, area, angle, heading);
    }

    public void Dispose()
    {
        ParticleLibrary.Changed -= Drop;
        Drop();
        _kit.Dispose();
    }

    private void Drop()
    {
        foreach (Pool pool in _pools.Values)
        {
            foreach (ParticleInstance instance in pool.Instances)
                instance.Dispose();
        }

        _pools.Clear();
    }

    private sealed class Pool(ParticleInstance[] instances)
    {
        public ParticleInstance[] Instances { get; } = instances;
        public int Next { get; set; }
    }
}
