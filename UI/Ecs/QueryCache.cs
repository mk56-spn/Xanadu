using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>
/// One query per store and component set, made once. <c>store.Query&lt;T&gt;()</c>
/// builds a fresh query every call, which matches it against every archetype
/// in the store and allocates; a kept query is updated by the store as
/// archetypes appear. Use this for queries that run every frame.
/// </summary>
/// <remarks>
/// The query is shared, so never add filters to what this returns
/// (<c>WithDisabled</c>, <c>AllTags</c>…): every other caller would get them too.
/// Make and keep a filtered query of your own instead.
/// </remarks>
public static class QueryCache
{
    private static class Of<T1> where T1 : struct, IComponent
    {
        public static readonly ConditionalWeakTable<EntityStore, ArchetypeQuery<T1>> Queries = new();
    }

    private static class Of<T1, T2> where T1 : struct, IComponent where T2 : struct, IComponent
    {
        public static readonly ConditionalWeakTable<EntityStore, ArchetypeQuery<T1, T2>> Queries = new();
    }

    public static ArchetypeQuery<T1> Cached<T1>(this EntityStore store) where T1 : struct, IComponent =>
        Of<T1>.Queries.GetValue(store, static s => s.Query<T1>());

    public static ArchetypeQuery<T1, T2> Cached<T1, T2>(this EntityStore store)
        where T1 : struct, IComponent where T2 : struct, IComponent =>
        Of<T1, T2>.Queries.GetValue(store, static s => s.Query<T1, T2>());
}
