# Friflo ECS, offline

The gitbook is for tutorials. For anything about the API, read the docs the
NuGet package already ships — they are local, greppable and match the referenced
version exactly.

## Where the docs are

| What | Where |
| --- | --- |
| API reference | `~/.nuget/packages/friflo.engine.ecs/<version>/lib/net10.0/Friflo.Engine.ECS.xml` |
| Package readme | `~/.nuget/packages/friflo.engine.ecs/<version>/README.md` |
| Tutorials | <https://friflo.gitbook.io/friflo.engine.ecs> |

Every GitBook page has a markdown twin: append `.md` to the page URL, which is
one request and no JavaScript. The full index is
<https://friflo.gitbook.io/friflo.engine.ecs/llms.txt>.

The XML file is the full public API: about 1100 members, each with the summary
the library author wrote. The version is whatever `Xanadu.csproj` references, so
do not hand-roll the path:

```bash
python3 Docs/ecs/lookup.py --path
```

## Look things up

```bash
python3 Docs/ecs/lookup.py CommandBuffer
python3 Docs/ecs/lookup.py CommandBuffer Playback    # every term must match
python3 Docs/ecs/lookup.py QuerySystem --full        # params and remarks too
python3 Docs/ecs/lookup.py Entity AddChild
```

Or grep the XML directly. Members are named `T:` type, `M:` method, `P:`
property, `F:` field:

```bash
XML=$(python3 Docs/ecs/lookup.py --path)
grep -A8 'name="M:Friflo.Engine.ECS.Entity.AddChild' "$XML"
```

## The slice this project uses

| I need to… | Do | Working example |
| --- | --- | --- |
| define a component | `struct Foo(...) : IComponent` | [Core/Tags.cs](../../Core/Tags.cs) |
| define a tag | `struct Bar : ITag` | [Core/Tags.cs](../../Core/Tags.cs) |
| create an entity | `store.CreateEntity(new Foo(...), Tags.Get<Dirty>())` | [UiBuilder.cs](../../UI/Ecs/Ui/UiBuilder.cs) |
| query every frame | `QuerySystem<A, B>`, `Filter.AllTags(...)`, `Query.ForEachEntity((ref A a, ref B b, Entity e) => …)` | [RectDrawSystem.cs](../../UI/Systems/Rendering/RectDrawSystem.cs) |
| change structure in a system | `CommandBuffer.AddTag/RemoveTag/AddComponent` | [RectDrawSystem.cs](../../UI/Systems/Rendering/RectDrawSystem.cs) |
| read the current matches | `Query.ToEntityList()` | [ContainerLayoutSystem.cs](../../UI/Systems/Layout/ContainerLayoutSystem.cs) |
| edit a component in place | `ref var c = ref entity.GetComponent<T>(); c.X = 1; entity.AddTag<Dirty>();` | [UiNode.Style.cs](../../UI/Ecs/Ui/UiNode.Style.cs) |
| walk the tree | `parent.AddChild(child)`, `entity.Parent`, `entity.ChildEntities` | [UiBuilder.cs](../../UI/Ecs/Ui/UiBuilder.cs) |
| hide a subtree | `UiLayout.SetHidden(entity, true)`; the native `Disabled` tag follows | [UiTreeEvents.cs](../../UI/Ecs/Ui/UiTreeEvents.cs) |
| react to the tree changing | `store.OnChildEntitiesChanged`, `store.OnTagsChanged` | [UiTreeEvents.cs](../../UI/Ecs/Ui/UiTreeEvents.cs) |
| find what links to an entity | `target.GetIncomingLinks<TLink>()` | [UiDropdownSystem.cs](../../UI/Systems/Ui/UiDropdownSystem.cs) |
| compose systems | `SystemRoot` + `SystemGroup` + `UpdateTick` | [UiRoot.cs](../../UI/Systems/Ui/UiRoot.cs) |

## Query performance

`ForEachEntity` is fine for UI-sized queries. The optimization forms matter for
playfield and note counts; the whole page is one fetch:

<https://friflo.gitbook.io/friflo.engine.ecs/documentation/query-optimization.md>

In this repo `Friflo.Engine.ECS.Boost` is already referenced, so
`query.Each(struct : IEach<…>)` works today. `Friflo.Engine.ECS.Generators` (the
`[Query]` source generator) is not referenced yet; it is an analyzer-only
package reference.

**Every-frame queries:** `store.Query<T>()` builds a fresh query each call
(matched against every archetype, allocating). For queries that run every
frame use `store.Cached<T>()` / `store.Cached<T1, T2>()`, one kept query per
store, and `store.ToggleOn<TTag>(none, hidden)` to read a tagged `Toggle`
([QueryCache.cs](../../UI/Ecs/QueryCache.cs)). The cached query is shared:
never add filters (`WithDisabled`, `AllTags`) to it; keep a filtered query of
your own instead. Find hot ones with the frame autopsy (`Docs/host.md` §5.10).

## Rules that bite

- **Structural changes inside `Query.ForEachEntity` throw
  `StructuralChangeException`.** Queue them on the system's `CommandBuffer`
  instead.
- `CommandBuffer.Playback` must run on the main thread. The system's buffer
  belongs to its parent group, so don't hold it across frames.
- `GetComponent<T>()` returns a `ref T`. Mutating it does not notify anything:
  tag the entity afterwards (`Dirty` to redraw, `LayoutDirty`/`Moved` for
  layout). See [UiNode.Style.cs](../../UI/Ecs/Ui/UiNode.Style.cs).
- `AddComponent` on an entity that already has that type **updates** it; it does
  not add a second one.
- Deleting a parent does not delete its children. Use
  `UiLayout.DeleteSubtree` ([UiLayout.cs](../../UI/Ecs/Ui/UiLayout.cs)).
- `Entity.InsertChild` silently loses the child if a handler structurally changes
  the parent during the move (seen on 4.0.0-preview.2), and `UiTreeEvents` does.
  Use `UiLayout.MoveChild`, which is built from whole `RemoveChild`/`AddChild` calls.
- A child added under a disabled parent stays enabled, and `EnableTree` re-enables
  everything under it, including what was disabled on purpose. Don't use
  `Enabled`/`DisableTree` directly on UI: `UiLayout.SetHidden` handles both.
- Queries skip disabled entities unless asked with `WithDisabled`; `ChildEntities`
  does not skip them.
