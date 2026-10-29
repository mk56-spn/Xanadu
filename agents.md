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
   hand-roll parent ids.
10. **Entities are handles.** `Entity` is a small struct (store + id); `default`
    is null, so check `IsNull`; `entity.Store` gets the store. Components live in
    archetypes, not in the handle.

Working examples of the slice this project uses: `docs/ecs/README.md`.

## UI

UI is ECS entities — `Rectangle`, `Text`, `TextStyle`, `ButtonVisual`,
`TakesInput`, `Toggle`, … — built with `UiBuilder` or `UiNode` and rendered by
the `Xanadu.Ui` systems.

**Never build UI out of Godot Control widgets.** This includes `Button`,
`Label`, `LineEdit`, `TextEdit`, `Panel`, `ColorRect`, `TextureRect`,
`VBoxContainer`, `HBoxContainer`, `GridContainer`, `MarginContainer`,
`ScrollContainer`, `TabContainer`, `ItemList`, `Tree`, `PopupMenu`,
`OptionButton`, `CheckBox`, `Slider`, `ProgressBar`, `ConfirmationDialog`,
`AcceptDialog`, `Window` and `FileDialog`. **IMPORTANT**

### The one exception

Opening files: `FileDialog` to pick a file, plus the invisible `LineEdit` that
`EditorOverlay` uses to receive OS keyboard/clipboard text (Godot has no other
way to get it). Both live in `Editor/EditorOverlay*`.

The rest of `EditorOverlay*` — the song picker's `ItemList`/`ConfirmationDialog`,
the `Label`s — is existing debt, not precedent. Do not add more Godot UI
anywhere, including test harnesses and tools.

### Roots and design space

- A scene root may be a `Control` or `Node2D` **only** to supply a canvas
  item/host for ECS UI. It must not draw, lay out, or take input itself.
- Author UI against the **1920x1080 design space, 16:9 only**. Do not read the
  window size in a scene and do not do per-control scale math; the host scales
  the whole surface. Relative layout (containers, anchors) is for different
  content, not for different resolutions yet.

### Hard coding

- Font sizes come from `UiType` (`Display`..`Caption`); control heights, gaps,
  borders and radii come from `UiMetrics`. A new screen carries no type or
  control size numbers.
- Author against `UiStage` (`Page()`, `Stage.Center`, `Stage.Bounds`), never a
  screen-size parameter. The stage is 1920x1080 and the host scales it.
- **Stop hard coding UI by not using containers, unless you have a really good
  reason. IMPORTANT**

## Code

- Use agents to implement your logic, if you need them or consider them a better
  fit for your use case.
- Prefer splitting things into files within reason; `main` should not become too
  large.
- Try and keep files under 400 lines.

## Verification

Use the Godot binary to verify visually — never just guess. It is the binary
inside this directory:

```text
/home/dhs/Documents/Godot_v4.8-dev7_mono_linux_x86_64/Godot_v4.8-dev7_mono_linux.x86_64
```

### Capturing what Godot drew

Capture **from inside Godot** (its viewport texture). Desktop tools capture the
desktop, not the game: on this Wayland box they take whichever window is on top,
and the sandbox cannot write their configs.

**Visual tests.** The harness reads its own viewport, so it needs no focus and
no visible window; it writes the PNG and exits. Do not combine with
`--headless` — headless cannot render.

```bash
GODOT=/home/dhs/Documents/Godot_v4.8-dev7_mono_linux_x86_64/Godot_v4.8-dev7_mono_linux.x86_64
"$GODOT" --path Tests/VisualTests --fullscreen res://VisualTests.tscn -- \
  --visual-test=<slug> --visual-test-shot=<path under the repo>
```

**The game.** `--shot=<path>` saves the window once it settles, then exits.
`F12` does the same on demand into `screenshots/` (`Main` falls back from
`user://` to `res://screenshots`). Do not combine either with `--headless`.

```bash
"$GODOT" --path . --resolution 1280x720 -- --shot=<path under the repo>
```

**Desktop capture**, if you really need it: set `XDG_CONFIG_HOME` and
`XDG_CACHE_HOME` to a directory inside the repo (e.g. `.tmp/config`) or
`spectacle` fails writing `~/.config/spectacle`, and the Godot window has to be
the top one.

In the agent sandbox `user://` is read-only, so replay saves and anything else
writing there fails. Write screenshots and scratch files under the repo, never
under `~/.local/share`.

### When things break

- If screenshot perms are broken, ask me about them. **Don't guess.**
- When in doubt, ask me.

## See also

- `Tests/VisualTests/README.md` — the scene-test harness, its step model and flags.
- `docs/ui-fluent-architecture.md` — the proposed `UiNode` API. A proposal plus a
  working prototype, not yet the house style; `UiBuilder` is still what ships.
