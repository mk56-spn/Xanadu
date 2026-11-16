# Xanadu agent rules

## Architecture

- **Do not try and skirt around the ECS. Always use the ECS.**
- ECS docs are local, not a website: start with `Docs/ecs/README.md` and look
  types up with `python3 Docs/ecs/lookup.py <Type>`. The gitbook is tutorials
  only: <https://friflo.gitbook.io/friflo.engine.ecs/documentation>

### ECS in ten points

1. **Queries run through an `IEach` struct by default.** Write the loop body as
   a `struct : IEach<…>` (`Execute(ref T1, …)`) or `IEachEntity<…>`
   (`Execute(ref T1, …, int id)`, entity via `store.GetEntityById(id)`) and run
   it with `query.Each(new S(…))` / `query.EachEntity(new S(…))`
   (`Friflo.Engine.ECS.Boost`, referenced). Inputs go in as fields; `Each`
   returns the struct, so results (best hit, count, found entity) come back on
   it. The structs live at the bottom of the file, one per query, which keeps
   every query in one boxed-off place. Only when max perf is needed, step down to
   `foreach (var (components, entities) in query.Chunks)` and
   `AsSpan256` / `StepSpan256` for a vectorized body (item 6 covers parallel).
   `ForEachEntity` lambdas and `query.Entities` + `GetComponent` are legacy:
   don't add new ones. Convert on touch: when a file is being edited anyway, its
   loops move to `IEach` in that edit; never sweep untouched files for it. Parallel pays off for arithmetic, not heavy structural
   changes.
   **`ToEntityList()` only on queries of about a dozen entities or fewer**
   (sessions, editors, panels, dialogs). It allocates and fills a new list on
   every call and exists for structural changes in the middle of a loop. A query
   that grows with content (notes, mines, blocks, spikes, items, buttons, UI
   rectangles, `Store.Entities`) never uses it: run an `IEach` struct, and
   defer changes through `CommandBuffer`. To find the
   entity carrying a tag, query that tag; don't scan the store for it.
2. **Archetypes.** Entities are grouped by their exact component/tag set
   (`Archetype`); a query only walks the matching groups.
3. **Tags filter cheaply.** `Filter.AllTags`, `AnyTags`, `WithoutAllTags`,
   `WithoutAnyTags` over zero-size tags. Prefer a tag to a `bool` field for
   "dirty", "selected", "visible".
4. **Structural changes are deferred.** Create/delete, add/remove component or
   tag go through the system's `CommandBuffer`; doing it inside
   `Query.ForEachEntity` throws `StructuralChangeException`.
5. **Batching exists.** `CreateEntityBatch` buffers components/tags and stamps
   out entities without allocations; `EntityBatch` replays a cached add/remove
   set on one entity or many.
6. **Queries can run in parallel.** `query.ForEach(...)` returns a reusable
   `QueryJob`; give the store or job a `ParallelJobRunner` and call
   `RunParallel()` to split chunks across threads. Record structural changes
   from workers with `CommandBuffer.Synced`, then `Playback()`.
7. **Structural changes have native events.** `EventRecorder` + `EventFilter` +
   `EntityEvent` report what was added or removed, per component or tag — no
   polling or diffing.
8. **Relations are a component type.** `IRelation<T>` puts several components of
   the same type on one entity (inventory, attachments, buffs);
   `entity.GetRelations<T>()` returns them; `ILinkRelation` links to entities.
9. **The entity tree is built in.** `TreeNode` + `AddChild` / `Parent` /
   `ChildEntities`, with `TreeMembership` and `ChildEntitiesChanged`. Don't
   hand-roll parent ids, and don't store a reference to a child or parent the
   tree already gives you. Reorder with `UiLayout.MoveChild`, never `InsertChild`
   (see `Docs/ecs/README.md`).
10. **Entities are handles.** `Entity` is a small struct (store + id); `default`
    is null, so check `IsNull`; `entity.Store` gets the store. Components live in
    archetypes, not in the handle.

### Finding a specific entity

**Use an indexed component, never `UniqueEntity`.** Friflo's `UniqueEntity` /
`GetUniqueEntity` is the old way to name one entity; don't add new uses. Give the
entity a component implementing `IIndexedComponent<TValue>` (one field, returned
by `GetIndexedValue()`) and look it up by value: `store.ComponentIndex<TComponent,
TValue>()[value]` returns the matching entities in O(1), and a query narrows to
them with `.HasValue<TComponent, TValue>(value)` (example: `Layer` in
`UI/Ecs/Ui/UiComponents.cs`, queried in `DrawableInitialiseSystem`). One indexed
type covers every value, so prefer an enum or int key shared by a whole family of
entities over a new component per entity. `GetEntitiesWithComponentValue` is
obsolete too; use `ComponentIndex`.

Pick the right tool for "which entities": a **tag** for a fixed kind or state, an
**indexed component** for a value you look entities up by (a group, a layer, a
named switch), a **link component** (`ILinkComponent`, `ILinkRelation` for many)
for a relationship to another entity.

**"I want *that* entity"** — one particular entity, not a kind or a value:

- **Code that made it, or was handed it:** keep the `Entity` handle. It is a small
  struct; check `IsNull` before use.
- **A component that points at it** (an owner, a target, the item being dragged or
  edited): an `ILinkComponent` whose indexed value is the target `Entity`; never a
  plain `Entity` or `int` id field. Friflo indexes links, so the other direction is
  free: `entity.GetIncomingLinks<T>()` lists everything pointing at it. Several
  targets from one entity: `ILinkRelation`.
- **Its parent or children:** the entity tree (point 9), not a link.
- **"The" one of its kind** (the editor, the player): query its tag. If several
  share the tag and differ by a key, that key is an indexed component.

Working examples of the slice this project uses: `Docs/ecs/README.md`.

## UI

UI is ECS entities — `Rectangle`, `Text`, `TextStyle`, `ButtonVisual`,
`TakesInput`, `Toggle`, … — rendered by the `Xanadu.Ui` systems. Write new UI as a
tree of specs (`Xanadu.Ui.Tree`: `new Column(gap: Space.Md) { a, b }`, added with
`parent.Add(spec)`) so the code has the screen's shape, and change it afterwards
through the built nodes (`UiNode`); see `Docs/ui-best-practices.md`. `UiBuilder` is the low-level factory behind it; the direct callers
left are the F2 shader tuner (a debug overlay) and the older animator panels
(`Tools/Animator`), which are not to be copied.

There is no widget toolkit: every control is ECS UI. Opening and saving files
is the one thing handed to the OS, through the host's native dialog
(`HostCore/HostFileDialog.cs`), triggered by an ECS button and owning no layout.

### Roots and design space

- A scene draws under the canvas item its host gives it; it never owns the
  window or reads it.
- Author UI against the **1920x1080 design space, 16:9 only**. Do not read the
  window size in a scene and do not do per-control scale math; the host scales
  the whole surface. Relative layout (containers, anchors) is for different
  content, not for different resolutions yet.

### Debug views

Every debug view (F1–F4 engine, F6 gameplay, F9–F10 editor) takes its colours from `DebugPalette` in the UI
module, wherever the view lives. Add a missing colour there, never in the view.
The keys themselves live in one place, `DebugKeys`; see its comment for the bands.

### Hard coding

- Font sizes come from `UiType` (`Display`..`Caption`); control heights, gaps,
  borders and radii come from `UiMetrics`. A new screen carries no type or
  control size numbers.
- Author against `UiStage` (`Page()`, `Stage.Center`, `Stage.Bounds`), never a
  screen-size parameter. The stage is 1920x1080 and the host scales it.
- **Stop hard coding UI by not using containers, unless you have a really good
  reason. IMPORTANT**
- If the UI library is missing a feature you need (a control, a container
  behaviour, a scroll list, a tooltip…), ask me for permission to add it to the
  library. Do not hard code around the gap with hand-placed rects, raw canvas
  draws or your own pointer reading.

### Layers

Every layer's z index is named in `UiLayers`, bottom to top. Add a name there and
pass it to `UiNode.Layer`; never pass a number or keep a local constant.

### Showing and hiding

Hide with `UiLayout.SetHidden` / `UiNode.Hidden()`, never by moving a node off
screen, painting it transparent or keeping a `Visible` flag. `UiHidden` is what
you author; `UiTreeEvents` keeps the native `Disabled` tag on the subtree, so every
query, the canvas and the parent's stack leave it out. `LayoutIgnore` only means
"visible, but placed outside the stack". `UiDisabled` is a greyed-out control:
drawn and laid out, but taking no input.

Queries skip hidden entities, so add `WithDisabled` when a system:

- **shows the thing it queries**, or it can never come back;
- **reacts to a press**: a press can land on a control that hides itself that
  frame, and the system still has to see it and let go of it;
- **collects parts to delete or restyle** that may be hidden.

A system that paints its own canvas from a panel's rectangle must check
`panel.Enabled` itself.

## Code

- Use agents to implement your logic, if you need them or consider them a better
  fit for your use case.
- Prefer splitting things into files within reason; `main` should not become too
  large.
- Try and keep files under 400 lines.

## Verification

Verify visually — never just guess. Everything runs on the MoonWorks host, a
plain `dotnet run`; there is no engine to install. After a fresh clone, run
`External/fetch-moonlibs.sh` once (the native libraries).

- **Build:** `dotnet build` on each project touched.
- **Tests:** `dotnet test Tests/Xanadu.Tests.csproj`, `dotnet test UI/Tests` and
  `dotnet test Slang/Tests`.
  `Docs/host.md` §7 lists the failures that are already known.
- **Perf:** `./run.sh devtools autopsy game|editor|labs|animator` records a launch and
  opens the frame autopsy (`Docs/host.md` §5.10).

### Screenshots

Every exe reads its own frame, so a shot needs no focus and no visible window.

**The game.** `--shot=<path>` saves frame 60 and quits (`--shot-frame=<n>` to
pick another); `--scene=<name>`, `--demo`, `--play[=<name>]` and `--editor`
pick where it starts. `F12` saves into `user://screenshots` on demand.

```bash
./run.sh game --scene=songselect --shot=<path>
./run.sh editor --shot=<path>
```

**Visual tests.** One test, a shot once it settles, exit code 0 or 1:

```bash
./run.sh visual-tests --visual-test=<slug> --visual-test-exit
./run.sh visual-tests --visual-test=<slug> --visual-test-shot=<path> --visual-test-full
./run.sh visual-tests --visual-test=all --visual-test-exit
```

**Tools.** `./run.sh animator`, `./run.sh showcase` and `./run.sh labs` take
`--shot=<path>` too; `./run.sh` with no target asks which to start.

Screenshots and scratch files go **outside the repo**: your session's
scratchpad, or a temp folder. Never in the project folder, not even in
git-ignored ones.

### When things break

- If screenshot perms are broken, ask me about them. **Don't guess.**
- When in doubt, ask me.

## See also

- `Docs/host.md` — the MoonWorks host: decisions, renderer, shaders, tools,
  perf tooling (GPU times, Autopsy) and what is left for later.
- `Tests/VisualTests/README.md` — the scene-test harness, its step model and flags.
- `Docs/ui-best-practices.md` — the UI house style: the `UiNode` model, the API at
  a glance, its defaults, and one worked example; Atlas is the full-size one.
