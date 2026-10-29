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
python3 docs/ecs/lookup.py --path
```

## Look things up

```bash
python3 docs/ecs/lookup.py CommandBuffer
python3 docs/ecs/lookup.py CommandBuffer Playback    # every term must match
python3 docs/ecs/lookup.py QuerySystem --full        # params and remarks too
python3 docs/ecs/lookup.py Entity AddChild
```

Or grep the XML directly. Members are named `T:` type, `M:` method, `P:`
property, `F:` field:

```bash
XML=$(python3 docs/ecs/lookup.py --path)
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
| compose systems | `SystemRoot` + `SystemGroup` + `UpdateTick` | [UiSystemGroup.cs](../../UI/Systems/Ui/UiSystemGroup.cs) |

## Query performance

`ForEachEntity` is fine for UI-sized queries. The optimization forms matter for
playfield and note counts; the whole page is one fetch:

<https://friflo.gitbook.io/friflo.engine.ecs/documentation/query-optimization.md>

In this repo `Friflo.Engine.ECS.Boost` is already referenced, so
`query.Each(struct : IEach<…>)` works today. `Friflo.Engine.ECS.Generators` (the
`[Query]` source generator) is not referenced yet; it is an analyzer-only
package reference.

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
