# Xanadu agent rules

## Architecture

- **Do not try and skirt around the ECS. Always use the ECS.**
- ECS docs are local, not a website: start with `docs/ecs/README.md` and look
  types up with `python3 docs/ecs/lookup.py <Type>`. The gitbook is tutorials
  only: <https://friflo.gitbook.io/friflo.engine.ecs/documentation>

### ECS in ten points

1. **Queries have escalating execution forms — the query stays, the body swaps.**
   `query.ForEachEntity(...)` is the default and pays a delegate per result. For
   a hot query keep `store.Query<T>()` and change only how it executes: `[Query]`
   with `Friflo.Engine.ECS.Generators` (generated chunk loop, no delegate;
   analyzer-only package, not referenced yet),
   `query.Each(struct : IEach<…>)` with `Friflo.Engine.ECS.Boost` (~3x and the
   fastest non-SIMD form; already referenced),
   `foreach (var (components, entities) in query.Chunks)` for manual work, and
   `AsSpan256` / `StepSpan256` for the vectorized body (item 6 covers parallel).
   Iterating `query.Entities` + `GetComponent` is much slower — only for tiny
   results, and parallel pays off for arithmetic, not heavy structural changes.
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
   (see `docs/ecs/README.md`).
10. **Entities are handles.** `Entity` is a small struct (store + id); `default`
    is null, so check `IsNull`; `entity.Store` gets the store. Components live in
    archetypes, not in the handle.

Working examples of the slice this project uses: `docs/ecs/README.md`.

## UI

UI is ECS entities — `Rectangle`, `Text`, `TextStyle`, `ButtonVisual`,
`TakesInput`, `Toggle`, … — built with `UiNode` and rendered by the `Xanadu.Ui`
systems. `UiBuilder` is the low-level factory behind it; the only direct caller
left is the F2 shader tuner (a debug overlay).

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
- **Tests:** `dotnet test Tests/Xanadu.Tests.csproj` and `dotnet test UI/Tests`.
  `docs/host.md` §7 lists the failures that are already known.
- **Perf:** `./run-autopsy.sh game|editor|labs|animator` records a launch and
  opens the frame autopsy (`docs/host.md` §5.10).

### Screenshots

Every exe reads its own frame, so a shot needs no focus and no visible window.

**The game.** `--shot=<path>` saves frame 60 and quits (`--shot-frame=<n>` to
pick another); `--scene=<name>`, `--demo`, `--play[=<name>]` and `--editor`
pick where it starts. `F12` saves into `user://screenshots` on demand.

```bash
./run-game.sh --scene=songselect --shot=<path>
./run-editor.sh --shot=<path>
```

**Visual tests.** One test, a shot once it settles, exit code 0 or 1:

```bash
./run-visual-tests.sh --visual-test=<slug> --visual-test-exit
./run-visual-tests.sh --visual-test=<slug> --visual-test-shot=<path> --visual-test-full
./run-visual-tests.sh --visual-test=all --visual-test-exit
```

**Tools.** `run-animator.sh`, `run-showcase.sh` and `run-labs.sh` take
`--shot=<path>` too; `./run.sh` asks which to start.

Screenshots and scratch files go under `screenshots/` (git-ignored) or the
scratchpad, never into `Assets/`.

### When things break

- If screenshot perms are broken, ask me about them. **Don't guess.**
- When in doubt, ask me.

## See also

- `docs/host.md` — the MoonWorks host: decisions, renderer, shaders, tools,
  perf tooling (GPU times, Autopsy) and what is left for later.
- `Tests/VisualTests/README.md` — the scene-test harness, its step model and flags.
- `docs/ui-fluent-architecture.md` — the `UiNode` design, the rules the
  migration settled on, and its open questions. This is the house style.
