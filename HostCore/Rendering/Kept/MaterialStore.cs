using Friflo.Engine.ECS;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Host.Rendering.Kept;

/// <summary>
/// The GPU state each material's stack keeps between frames, as one entity per material
/// (MaterialComponents.cs): its state, its pool, its BUILD buffers. Forgetting a
/// material deletes its entity and lets go of all of it at once.
/// </summary>
internal sealed class MaterialStore : IDisposable
{
    private readonly EntityStore _store;
    private readonly ArchetypeQuery<StateBuffer> _states;
    private readonly ArchetypeQuery<ItemPool> _pools;
    private readonly ArchetypeQuery<BuildBuffers> _builds;

    public MaterialStore(EntityStore store)
    {
        _store = store;
        _states = _store.Query<StateBuffer>();
        _pools = _store.Query<ItemPool>();
        _builds = _store.Query<BuildBuffers>();
    }

    /// <summary>The material's entity, or null when nothing was kept for it.</summary>
    public Entity Find(CanvasMaterial? material)
    {
        if (material is null)
            return default;
        Entities found = _store.ComponentIndex<MaterialOf, CanvasMaterial>()[material];
        return found.Count > 0 ? found[0] : default;
    }

    /// <summary>The material's entity, made the first time it is asked for.</summary>
    public Entity For(CanvasMaterial material)
    {
        Entity entity = Find(material);
        return entity.IsNull ? _store.CreateEntity(new MaterialOf(material)) : entity;
    }

    public Buffer? State(CanvasMaterial? material) =>
        Find(material) is { IsNull: false } entity && entity.TryGetComponent(out StateBuffer state) ? state.Buffer : null;

    public Buffer? PoolItems(CanvasMaterial? material) =>
        Find(material) is { IsNull: false } entity && entity.TryGetComponent(out ItemPool pool) ? pool.Items : null;

    public Buffer[]? Built(CanvasMaterial? material) =>
        Find(material) is { IsNull: false } entity && entity.TryGetComponent(out BuildBuffers built) ? built.Buffers : null;

    /// <summary>Whether this is the first time <paramref name="frame"/> the material's compute runs; marks it run.</summary>
    public bool FirstRun(CanvasMaterial material, long frame)
    {
        Entity entity = For(material);
        if (entity.TryGetComponent(out RanFrame ran) && ran.Frame == frame)
            return false;
        entity.AddComponent(new RanFrame(frame));
        return true;
    }

    /// <summary>Lets go of everything kept for the material (its effect was dropped or recompiled away).</summary>
    public void Forget(CanvasMaterial material)
    {
        Entity entity = Find(material);
        if (entity.IsNull)
            return;
        if (entity.TryGetComponent(out StateBuffer state))
            state.Buffer.Dispose();
        if (entity.TryGetComponent(out ItemPool pool))
            pool.Items.Dispose();
        if (entity.TryGetComponent(out BuildBuffers built))
            Release(built.Buffers);
        entity.DeleteEntity();
    }

    public static void Release(Buffer[] buffers)
    {
        foreach (Buffer buffer in buffers)
            buffer.Dispose();
    }

    public void Dispose()
    {
        _states.Each(new DisposeStates());
        _pools.Each(new DisposePools());
        _builds.Each(new DisposeBuilds());
    }

    private struct DisposeStates : IEach<StateBuffer>
    {
        public void Execute(ref StateBuffer state) => state.Buffer.Dispose();
    }

    private struct DisposePools : IEach<ItemPool>
    {
        public void Execute(ref ItemPool pool) => pool.Items.Dispose();
    }

    private struct DisposeBuilds : IEach<BuildBuffers>
    {
        public void Execute(ref BuildBuffers built) => Release(built.Buffers);
    }
}
