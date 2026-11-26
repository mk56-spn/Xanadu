# Render graph: one path for every shader

Design for review (2026-11-25), then where the code stands. The first proposal is the artifact "Shader Land
Structure": https://claude.ai/artifact/3B7y8SVr71759LEirbDNQR. This replaces its phase plan. Phases 1b and 2
were built around `Lab/scene_room`, and this design starts from the other end.

## The starting point: most graphs are one node

Every shader runs through the graph. The stacks in the repo today (72, Lab included):

| Bricks | Stacks | Its graph |
|---|---|---|
| shade | 49 | one draw, into the canvas |
| deform + shade | 12 | one draw, into the canvas |
| deform + shade + step | 5 | a compute node, then one instanced draw |
| shade + update | 1 | a compute node, then one draw |
| anything with build or trace | 5 | a real graph |

So 61 of 72 graphs are "fragment node, exit". The design is judged on two things:

- **The trivial graph costs nothing.** A shade-only stack draws exactly as it does today: no CPU walk, no extra
  pass, no extra resource, the same draw call in the job's render pass.
- **It holds at the game's scale.** Thousands of items, hundreds of materials, in the game and not only in the Lab.

Everything else is built on top of those two.

**The shaders are work in progress like everything else.** They don't constrain the design. A shader is moved onto the graph
wherever the graph does its job better: tiling, a blur as separate passes, a march that stops at drawn depth, work
shared per material instead of redone per pixel. Brick syntax stays only as long as it's the shortest way to
write the common case.

## Rules

1. **One path.** The renderer runs graphs and nothing else. Bricks are sugar while they're the shortest way to
   write a graph: a stack without `graph()` gets the graph its bricks imply. A brick kind that stops paying for
   itself (TRACE, likely) is dropped, and its shaders are rewritten as graphs. `GraphPasses`, `StackPasses`, `TracePass` and the effect half of `DrawPass` become one
   runner with node kinds, not four code paths with their own bindings.
2. **The pipeline is generic; contracts live in Slang.** The C# compiler knows resources, node kinds and
   bindings. It never knows a library type (`Surface`, `Camera`, `Lighting`). A library contract that needs
   pipeline help asks for a generic feature (a draw with several outputs, a resource with a clear value).
3. **The graph says what it reads and writes; the runner decides where it runs.** Placement (before the canvas
   pass, or inside it), scope (per material or per item) and lifetimes come from the graph's declared and scanned
   access, not from special cases.

## The canvas is a resource

`graph()` names the canvas through three built-in resources:

| Name | What | Who may touch it |
|---|---|---|
| `frame` | the job's target as drawn so far, at the item's place in tree order | exit draws write it, blended as placed |
| `screen` | a copy of `frame` up to this item | anyone reads it; reading it costs a copy (shared per pass, as today) |
| `depth` | the canvas depth up to this item | anyone reads it (`sampleDepth` today) |

An **exit** is a draw node whose target is `frame`. It runs inside the job's render pass, depth-tested against
the canvas and blended as the item is placed. A graph has one or more exits, in order.

The implied graphs:

```slang
// shade                    → void graph() { shade(frame); }
// deform + shade           → the same; deform is the draw's vertex stage
// + step                   → void graph() { step(); shade(frame); }   // instanced by the pool
// + trace                  → void graph() { trace(); present(traced, tracedDepth); shade(frame); }
```

`present` stops being a special case. It's a library draw node whose shade samples the two images, with `frame`
as its target.

## Two places a node runs: before the pass, or inside it

SDL_GPU can't run compute or begin another render pass inside a render pass. So the runner splits every graph
in two:

- **Prelude:** everything that doesn't read `frame`, `screen` or `depth`. That's compute, draws into the graph's
  own images, and copies. All of it runs before the job's render pass begins, where `StackPasses.Run` sits today.
  It's batched across graphs (below).
- **Exits:** the draws into `frame`. They run in tree order inside the pass, like today's effect steps.

A node that reads the canvas so far (`screen`, `depth`) but isn't an exit is **mid-frame**. A trace that reads
the depth is the one case today. It ends the pass, runs, and starts it again, as `TracePass` does. That's allowed,
but the editor shows it as a cost (a pass break per item), the way a screen read is shown.

A trivial graph has no prelude and one exit, so the runner does what `DrawPass` does today.

## Scope: per material or per item

Today a stack's compute runs once per material per frame, and its draws run per item. Phase 1b's pixel passes and
draws took "the item's box" from whichever item came first, which breaks for two items on one material. Each node
needs a scope:

- **Per material:** a node that reads no item data runs once a frame, however many items show the material. Its
  resources are the material's. Examples: `record()`, a pool step, `update`.
- **Per item:** a node that reads the item runs once per placement. Its resources are that item's. Examples:
  a `[Pixels]` pass over the item's box, a draw over its quad, any exit, anything reading per-item values
  (`step.Values`).

Scope is inferred from what the node binds (the item-quad uniform, per-item params). A resource takes the scope of
the narrowest node that writes it. `scene_room` becomes: `record` per material, `ringDrawn`, `shoot`,
`illuminate` and `present` per item. Two items showing the same room each march their own box, and share one
recording.

## Resources: sizes, lifetimes, clears

Today every `[Image]` is the full target size, kept for ever, one set per material. Four images for `scene_room` is
about 66 MB. That can't hold for hundreds of materials.

- **Size.** `[Image(format)]` is the item's box by default, not the target. `[Image(format, scale: 0.5)]` is
  relative to the box. `[Image(format, 256, 256)]` is fixed. A per-material image has no box, so it must be fixed
  size.
- **Lifetime:**
  - **frame** (the default): it exists from its first writer to its last reader. The runner takes it from a pool
    bucketed by (format, size) and gives it back. Graphs that don't overlap reuse the same memory.
  - **kept** (`[Kept]`): it survives frames, per material or per item.
  - **history** (`[History]`): last frame's copy, double-buffered and swapped by the runner. That's the frame
    history the stack graph plan was waiting on.
- **Clear value on the declaration.** `[Image(r32f, clear: 1e30)]`. The runner clears it at first use: as a
  load-op on the first draw, or a fill before the first compute. Library contracts carry their own clears, and
  `clear()` calls in `graph()` go away. A graph's draw depth is a declared resource too, a frame-lifetime one,
  not a hidden `BuildBuffers.Z`.

## Nodes

Three kinds, plus the exit as a draw into `frame`.

- **Compute:** `[Pass(threads, over)]` over elements or groups; `[Pixels(w, h)]` over an image or the item's box.
- **Draw:**
  - **Geometry:** the item's quad as a grid (today's deform), a kept mesh, or N instances (a pool, or a count
    from a buffer, today's `[Draws]`).
  - **Outputs:** they come from the shade's return type. A `float4` is one target. A struct is one target per
    field, in order, each a `float`, `float2` or `float4`. The call names one image per output; the formats are
    checked against the fields. SDL_GPU's colour-target limit is the cap (4, to confirm).
  - **Depth for the shade:** the wrapper gives the shade the pixel's depth (pixels into the screen) on
    `ItemInput`, so a shade can write its own depth output. There's no hard-wired "target 1 is depth".
  - **Library side:** Surfaces then needs nothing from C#. `Surface` gets a `texels(depth)` method that returns
    a plain output struct, and the shade returns that.
- **Copy:** blit, mip, history swap, the screen copy.

Arguments are lists. A call carries its image and value arguments as an array, not the fixed `A/B/C/D` slots of
`StackBuild.Run`.

## Cost at scale

- **Static graphs are flattened when they compile.** A schedule with no uniform-dependent branch or loop is turned
  into a fixed node list once. The per-frame CPU walk is only for schedules that read values, once per scope.
- **Prelude compute is batched across graphs.** SDL_GPU orders storage writes only between passes, which is why
  each call is its own pass today. But two nodes that share no written resource can go in one compute pass. Nodes
  of different graphs never share resources, so all of a frame's per-material prelude can be a handful of passes,
  levelled by dependency. That replaces the "5 levels × 5 slots" idea in `Docs/shader-pipeline.md` §8: the levels
  come from the graph instead of being fixed slots.
- **Exits don't get worse.** Each effect step is its own draw today, and that stays. Later, consecutive exits of
  one material with no per-item prelude could become one instanced draw, but that's not part of this work.
- **No per-frame logging.** A graph that can't run records one problem for the material, and the editor's
  PROBLEMS panel shows it. `Console.Error` stays for start-up.

## The Lab-only gate (gone, 2026-11-25)

Graphs used to be refused outside `Lab/`. The gate came off with stage E's first slice: any stack may run a graph,
in the game, the editor and the shader tool alike. A stack without one compiles and draws exactly as before, so the
trivial path's cost doesn't move (no timing pass was run for it). `Lab/` stays the experiments folder: the game's
start-up compile skips it and the shader tool gives its cards a still.

Since 2026-11-26 the game draws any stack a node names, not only an allow-list: `CommonShaders.IsKnown` accepts
every folder under `Assets/Shaders/` but the library, and the renderer compiles it on first draw (a missing one draws
plain). Lab stacks aren't warmed at start, so a level using one compiles it when it first shows.

## Work, in order

Shaders change freely along the way. The bar is that each still does its job, judged by eye, and that the trivial
path costs no more than today: the canvas's draw and copy counts and the frame time don't go up for plain stacks.
The check each time: the visual tests, and shots of a few plain, deform, pool and graph stacks.

- **Alongside every stage: convert shaders onto the graph.** Go through the shaders for work the graph does
  better:
  - tiling or repeated cells (an instanced draw or a tiled compute instead of a per-pixel loop);
  - multi-tap blurs and filters (separable passes into frame images);
  - anything recomputed per pixel that's the same for the whole item (a per-material node);
  - TRACE stacks (a graph);
  - screen reads that could read a graph image instead.

  Port them in the stage that makes them possible, and drop brick kinds nothing needs any more.

- **A. Lowering and one runner.** Done (2026-11-25), except `present` as a library draw, which moved to C: a
  draw can't read images until draws take image arguments. Every stack compiles to a graph, its bricks lowered as above.
  One runner does the prelude and the exits. `StackPasses`, `GraphPasses`, `TracePass` and the effect half of `DrawPass`
  fold into node kinds. `frame` / `screen` / `depth` become resources.
- **B. Scope and resources.** Per-material and per-item nodes; item-sized frame images from a pool; `[Kept]`,
  `[History]`; clear values on declarations, `clear()` gone. `scene_room` with two items on one material is the
  test. Done (2026-11-25).
- **C. Generic draws.** Outputs from the shade's return struct; depth on `ItemInput`; argument lists instead of
  `A/B/C/D`; draws that read images, so `present` becomes a library draw and traced.slang goes. The `Surface` special case comes out of `SlangShaders.Draws.cs` and `BuildPasses.ReadDraws`.
- **C, dogfood: the graph board's underlay.** The editor's GRAPH view keeps its boxes, text and input on the UI
  `Board`; under them a stack draws the wires (a `[Pass]` lays out the curves, an instanced draw draws them), pulses
  along them from writer to reader, a half-scale glow under costly nodes and pass breaks, and frame images fading in
  at their first writer and out at their last reader. It needs a generic way for the host to hand a stack a buffer
  (the edges); a fixed count of edges as values does for a first cut.
- **C2. Graphs, not bricks (decided 2026-11-25).** The runtime is one graph path, but authoring was still
  brick-shaped: `graph()` lived in a BUILD brick, declarations "belonged in a BUILD brick", the item's quad was implied
  by a SHADE brick, and the tool built stacks from a strip of brick kinds. Rule 1 taken at its word:
  1. **Copies on `[Draw]`** (done): a count (a number, a value, or a buffer the GPU fills) and `copy` in the draw's vertex
     and shade, drawn indirectly when the count is on the GPU. Several counted draws in one graph.
  2. **A stack is a graph** (done): a stack that declares `graph()` is described by it alone, in whichever file it sits.
     No BUILD kind; resources and nodes may be declared in any of the stack's files.
  3. **Bricks only as sugar** (done) for a stack with no `graph()` that is `shade` (and maybe `deform`): one draw onto the
     frame. STEP, UPDATE and TRACE stop being kinds: a pool is a `[Pass]` over a kept buffer plus a counted draw, an
     update a `[Pass]` over one kept struct, a trace a `[Pixels]` pass and `present`. Their stacks are rewritten as
     graphs. **Full-screen passes stay first-class:** before TRACE goes, any `[Pixels]` pass reads the canvas so far
     (`screen`, `depth`) and runs mid-frame for it (done 2026-11-25, `Lab/post`), so nothing a trace could do is lost.
  4. **The tool shows the graph** (done, with 2), not a strip of brick kinds.
  5. **The board's wires** (done) as the first thing written that way: a host buffer, a counted `[Draw]`, `wire(frame);`.
     No image, no backing taken from the board.
- **D. Cost.** Done (2026-11-25), batching dropped (see below). Flatten static graphs; batch prelude compute into levelled passes; problems per material; skip a
  node whose inputs haven't moved since it last ran, so a `[Kept]` image filled by it is a cache (2026-11-25: "cache a
  result as a texture" for absurd static backgrounds; cache the surfaces and keep the lighting live).
- **E. Then the 3D work resumes** on top, built to the drawing model the user confirmed (2026-11-25):
  - **Makers:** SDFs, meshes and anything else (pixel passes, particles, counted draws). Meshes have a 2D and a 3D
    front door over one core (generators into a meshlet pool, rebuilt only on change, one vertex-pulled draw); 2D is
    the plane with z as layer, a flat camera, unlit by default.
  - **Alone or together:** a maker can be the whole graph (its own quad, camera and light), or join a scene: every
    maker writes one shared depth + ids, a resolve rebuilds normal/uv/material, lighting runs once a pixel whoever made
    it, and shadows are every maker drawn again from each light. SDFs and meshes are equals in it.
  - **Targets:** every pipeline names where it ends: the screen, or a named image. An image is an input to any maker
    (a mesh's texture, an SDF's material, a post pass, next frame), so a scene can render into an image a 2D card wears.
  - **Libraries against it** (agreed 2026-11-25): `xanadu.shape` (2D SDFs in a shade) and `xanadu.march` (one field
    marched in a shade) stay as they are: the alone case, and the SDF maker's step for finding its surface. `sort`,
    `bvh` and `particles` are passes and stay. `xanadu.mesh` is rebuilt: it bends the item's grid and works the shape
    out at every vertex every frame, 3D only, no pool. `xanadu.scene` keeps its authoring (solids, `melt`/`cut`,
    `moved`/`turned`, a box per object) and loses its plumbing (its own record buffers and `shoot` into `Surfaces`)
    for the shared depth + ids. `xanadu.light` keeps "producers write surfaces, light reads them once" but is reshaped
    round ids + resolve instead of three fat images, and its `IShadows` round every maker drawn from each light.
  First pieces: `room.box`, shadow depth per light from the same geometry, screen-space
  AO, `[Surfaces]` sugar if it's still wanted once clears live on declarations.

## Plain names (2026-11-25)

Declarations say what a thing is, not its GPU type: `[Size(n)] List<T> name;` (a structured buffer), `Counter name;`
(one uint; `name.add(1)`), `Image<T> name;` (a storage texture) with `[Scale(s)]`, `[Size(w, h)]`, `[Clear(v)]`,
`[Kept]`, `[Cached]`, `[History(source)]`. An image's format follows what it holds (float r32f, float2 rg16f,
float4 rgba16f, uint r32ui); `[Precise]` and `[Compact]` change it. Atomics on uint lists are `list.add(at, by)`,
`take`, `keepLarger`, `keepSmaller` (`xanadu.store`). Reader: `Slang/BuildDeclarations.cs`. The design sections
above keep the older `[Image(format, …)]` spelling as written at the time.

### Stored values and list histories (2026-11-26, uncommitted)

- **`Stored<T> name;`** is one T the graph keeps, zero when made, used by its plain name from any pass or draw:
  `book.frame += 1`, `InterlockedAdd(book.written, 1, slot)`. It is a list of one underneath (`BuildBuffer.Stored`); the
  rewrite turns every use of the name into `name[0]` (`StackBuild.Rewrite`). It takes `[Cached]`, `[Host]` or `[Shared]`
  like a list, never `[Size]`. It replaces header lists with numbered slots and `[Size(1)]` flags.
- **`[History(list)] List<T> name;`** is a list's last frame: a buffer of its own, as big, swapped with its list once a
  frame before the graph's first node (`GraphResources.Turn`, `BuildBuffer.Source`). Not cleared, so the list starts
  each frame with what it held two frames ago. A list or counter can have one; not a stored, host, cached or shared one.
- **The board** says `one value` on a stored and `last frame` on a history.
- **`Lab/particles`** is on both: its 32-uint `lists` header (offsets, `asfloat`/`asuint`) is a `Book` struct, and the
  halves of `deaths` and `field` with their parity flips are `lastDeaths` and `fieldBefore`. Shots at frames 240 and 600
  match the old stack (rockets, shells bursting from last frame's deaths, the fountain pooling in the field). Tests:
  `Slang/Tests/StoredAndHistoryListTests.cs`. Slang suite 429 pass (1 skipped).
- **Not yet:** swarm, klein_swarm (`made`) and raymarch_tiled (`tiles[0]`) still use one-element lists; a stored value
  can't be lent to a library pass as itself (it goes as its list); lists have no `[Clear]` or frame lifetime yet.

## Decisions (2026-11-25)

1. **Scope** is inferred from what a node binds.
2. **Default image size** is the item's box.
3. **Mid-frame nodes** are allowed; the editor shows the pass break as a cost.
4. **Names** as written: `frame`, `screen`, `depth`, prelude, exit, `[Kept]`, `[History]`.

## Where the code is now (2026-11-25)

Stage A is in. The commits it builds on:

- `2c40a361` graph core: `graph()`, `[Image]`, `[Pixels]`, `present`, one clock preamble for every compute stage.
- `d7e23c63` draw nodes: `[Draw(across, down, shade: fn)]` into graph images against a per-graph depth, and
  `clear(image, value)`.
- `ade5f91c` the library contracts in `xanadu.light` (`Material`, `Lighting`, `Surface`, `Surfaces`, `IShadows`,
  `Lighting.lit` / `deferred`, `Camera.screenDepth` / `along`) and `scene.shoot` / `scene.shadows` on them.

Stage A:

- **Lowering:** `Slang/GraphPlan.cs`. A compiled stack becomes an ordered list of `GraphNode`s (`Update`, `Step`,
  `Compute`, `Pixels`, `Draw`, `Clear`, `Trace`, `Present`, `Shade`), each placed `Prelude`, `MidFrame` or `Exit`.
  A shade-only stack is one `Shade` exit, kept on the effect (`CanvasEffect.Plan`) and never lowered again. A
  schedule is lowered each frame with its material's values (`LoweredGraph` on the material's entity).
- **A trace is graph nodes:** a `Trace` node writing two images the graph keeps (after its `[Image]`s), then a
  `Present` exit. It's mid-frame only when it reads the canvas depth; otherwise it runs in the prelude with no pass
  break. That's `bvh_march` and `trace_wave` today.
- **The board draws the plan:** `StackGraph.Of` walks the same `GraphPlan`, so what the board shows is what runs. A
  mid-frame node says "breaks pass".
- **One runner, node kinds:** `Rendering/Passes/`
  - `GraphRunner`: the prelude for every material drawn, and the mid-frame nodes;
  - `ComputeNodes`: update, step, pass, pixels, trace;
  - `ImageDraws`: draw, clear;
  - `GraphResources`: the kept buffers and images;
  - `IndirectDraws`: `[Draws]` args;
  - `ExitDraws`: present and shade.

  `DrawPass` keeps the pass itself: the pre-pass, primitives, particles, the screen copy, and breaking the pass for
  mid-frame nodes.

Stage B, first half (2026-11-25):

- **Scope:** `GraphNode.Scope`. Update, step and `[Pass]` are per material: they run the first time a step shows the
  material in a frame, on its buffers. Pixels, draws, traces and the exits are per item: they run for every step
  showing it (not for particle meshes, which borrow the shade only). A `[Pass]` that touches an image is a compile
  error: it has no box to size it by.
- **Item images:** `Rendering/Passes/ItemImages.cs`. Each item's `[Image]`s and a trace's two are its box's size
  (rounded up to 64), taken from a pool bucketed by format and size, cleared on taking (cycled, so last frame's use
  can still be in flight), and given back when the next frame begins. A bucket unused for 120 frames is let go. Draws
  get a box-sized depth from the same pool, cleared by the item's first draw. `BuildBuffers` keeps only buffers.
- **Addressing:** `group.pixel` is the texel in the item's images, from the box's top left; `i.pixel` and
  `group.low`/`high` stay on the target. A draw renders through a viewport moved by the box's corner, and its
  fragment wrapper adds the corner back (`DrawBoxSlot`) before building `ItemInput`. Present reads at the pixel's
  place in the box.
- **Clears:** `[Clear(value)] Image<T> name;`; `clear()` is gone from the schedule language.
- **Checked:** scene_room, bvh_march and trace_wave look as before; two items on scene_room's material (a smaller
  second copy in the preview) each march, draw and light their own box over one recording.

Stage B, second half (2026-11-25):

- **`[Kept] Image<T> name;`** holds its texels from frame to frame, cleared only when made.
- **`[History(source)] Image<T> name;`** is what `source` held at the end of last frame (its format and clear
  come from the source). The two swap textures each frame and the source is cleared: nothing is copied. A `[Kept]`
  image takes no history (it already has last frame's texels).
- **Where they live:** `HeldImages` (`Rendering/Kept/HeldImages.cs`), a relation on the entity keeping the item's
  range (`KeptMesh`, keyed `(item, from)`), one per piece, keyed by where the piece starts. Steps carry that entity
  (`Step.Kept`). Held images are made again for a new compile or a new rounded size, and let go with the range.
- **Checked:** `Lab/trails`, a comet whose trail fades through `[History]` over paint spots that pile up in a
  `[Kept]` image on the beat.

Stage B, sizes (2026-11-25):

- **`[Scale(s)]`** is the box times s, rounded up (s above 0, at most 4). **`[Size(w, h)]`**
  is fixed. `clear:` comes last. A history takes its source's size. `ImageSize` (`Slang/BuildImage.cs`) turns a
  declaration and a box into texels.
- **`[Pixels(w, h, over: image)]`** runs a thread a texel of that image instead of a pixel of the box: `group.pixel`
  is the texel, `i.pixel` where its centre falls on the box (`TraceQuad.Stretch`, pixels per texel). Without `over:`
  it is the box, as before; reading a smaller image from there is the pass's own sum (`glow[group.pixel / 4]`).
- **Per-material images:** an image a `[Pass]` touches is the material's, one for every item showing it, and so
  are its history or source (`CompiledBuild.MaterialImages`). It must be fixed: a `[Pass]` has no box. Frame ones come
  from the pool once a frame; `[Kept]` and histories are held on the material's entity (`HeldImages` keyed
  `uint.MaxValue`). Item nodes read them; an item node writing one writes for every item.
- **Draws** still need box-sized targets (the draw depth is the box's): naming a scaled or fixed one is a compile error.
- **Present** stretches what it shows over the box: colour filtered, depth read nearest. A box-sized image samples at
  its texel's centres, so exactly as before.
- **Checked:** `Lab/sizes`, a 64 × 64 palette painted by a `[Pass]`, soft blobs worked out at a quarter of the box
  with `over:`, sharp rings at full size over them; `coarse` presents the quarter image alone. scene_room and trails
  look as before.

Stage C, first half (2026-11-25):

- **Outputs from the shade's return type** (`Slang/DrawOutput.cs`): a `float4` or `colour` shade is one target,
  discarded where its alpha is 0; a struct is one target per field (float, float2, float4, colour or uint), up to 4,
  found in the stack or the library. The call names an image per output, checked against the field.
- **Depth on `ItemInput`:** `i.depth` is the pixel's depth (pixels into the screen) in a draw, 0 elsewhere. The
  hard-wired "target 1 is depth" is gone: a shade that wants depth written returns it as a field.
- **No `Surface` in C#:** `Surface.texels(i.depth)` returns `SurfaceTexels` (depth, normal, base), discarding when
  nothing was found. scene_room's torus shade returns that.
- **Draws read images by name**, as pixel passes do: the fragment binds the images it reads as read-only storage
  textures after its samplers (`CompiledDraw.Reads`), and `group.pixel` is the texel under the pixel. A draw can't
  read an image it draws into, nor write any.
- **Checked:** scene_room as before; `Lab/sizes` has a waving band drawn into `sheet`, its shade reading the quarter
  `glow` under it.

- **Draws onto the frame:** `band(frame);` makes a draw an exit (colour shades only). It runs in the job's pass in plan
  order with the other exits, blended as the material says, its z in the item's slice of the canvas depth (the
  vertex stage's `slice`), and still reads the item's images (`group.pixel` from the box's corner). Image draws and
  exits are one compile; `ImageDraws.Draw` / `Exit` share their binding.
- **A draw's own blend (2026-11-26):** `[Draw(…, shade: fn, blend: add)]` (mix, add, sub, mul, premultiplied) blends
  that draw onto its targets: an exit over its material's blend, an image draw instead of overwriting. With
  `premultiplied` one draw holds normal and additive pixels together: `float4(rgb * a, a * (1 - glow))`, glow 1
  adding light; its empty pixels (discarded) are all-zero ones, not alpha-0 ones. `Lab/particles` does it per emitter.
- **Checked:** `Lab/sizes`' band is drawn onto the frame over the present, reading the glow; scene_room with a still
  torus matches the previous commit's shot.

Visual tests: nine scene tests (`ui-tree`, `song-select`, `settings`, `pause`, …) time out waiting for their screen;
they fail the same way at `ade5f91c`, before this work.

- **Argument lists:** a `Run` carries its call's values as `RunValues`, an inline array of four (`run.Values[k]`):
  a pass's uints, a draw's targets, a present's colour and depth (`Shown`, `ShownDepth`). Four is the real cap on
  both sides (the pass uniforms' four uints, SDL_GPU's four colour targets), and the schedule is lowered every frame,
  so it stays in place rather than an array per call.

Stage D, first piece (2026-11-25):

- **Static graphs flattened:** lowering notes whether the walk read any of the stack's values
  (`GraphPlan.ReadsValues`). A schedule that read none is lowered once per compile and kept (`LoweredGraph`, keyed by
  the effect like the other material state); one that reads values is lowered every frame as before.
- **Checked:** trails (reads none) and sizes (reads `coarse`) look as before.

Seen once and not again: right after a build, the shader tool showed "syntax error" on the first `[Image]` line of
trails and sizes at frame 120, as if a compile had seen the declarations without the rewrite. Two more runs, one right
after the Slang tests, were clean.

Stage C dogfood and C2 so far (2026-11-25):

- **Host buffers:** `[Host] [Size(n)] List<T> name;` is a BUILD buffer the host fills from
  `CanvasMaterial.SetBuffer(name, span)`, uploaded in the prelude when the material's `BufferVersion` moves
  (`GraphResources.For`); past the buffer's size is cut off. The count in use goes as a value.
- **C2.1, copies on `[Draw]`:** `[Draw(across, down, shade: fn, copies: n)]` draws the grid n times, `copy` its number in
  both stages. n is a number, constant or value (lowered into the run's `Over`, `Run.Copies`), or a uint buffer whose
  first element the graph's passes write: then it draws indirectly from per-material args (`BuildBuffers.DrawArgs`),
  the count copied in during the prelude (`GraphResources.Count`). A draw's stages read the graph's buffers (after their
  globals), and the board shows those reads. `Lab/copies` has both: spokes by a value, dots by a GPU count.
- **No quad of its own:** a stack with a graph (or a trace) and no SHADE brick draws only what its graph draws, whether
  or not it presents; any graph takes a slice of the frame's depth.
- **C2.5, the board's wires:** `Lab/graph_board` is two counted draws onto the frame: a glow quad per hot box and a
  line quad per straight piece of wire (round-ended, so bends join), carrying pulses from writer to reader. The board
  draws three canvas items, its backing and grid, then `BoardState.Underlay`'s, then its boxes; with an underlay it
  leaves its lines to it and draws only their arrowheads. `GraphUnderlay` (`HostCore/Tools/ShaderEditing`) feeds it the
  board's own routed wires (`Board.Paths()`, once per board shown), placed by the view's pan and zoom each frame.
- **`[Pixels]` reads the canvas so far:** a pixel pass declares compute samplers 0 (the canvas depth) and 1 (the screen
  copy) before its globals (`XANADU_SCREEN_BINDING`/`SET` in xanadu.stage), so `sampleDepth` and `sampleScreen` work
  in it. One that reads either is mid-frame (`CompiledPass.ReadsDepth` / `ReadsScreen`), and so is every item node
  after it in the plan; the break copies the screen first when one reads it (`GraphPlan.ReadsScreen`). Over an item
  the size of the screen it is a full-screen post pass. `Lab/post`: a half-size bloom off the canvas, a depth tint
  and a lens reading the canvas through a bend.
- **Not yet:** frame images fading in at their first writer and out at their last reader; hops where wires cross; the
  underlay lives in `Lab/` for the gate.
- **Checked:** shots of the GRAPH view on `Lab/scene_room` and `Lab/post`, and of `Lab/copies`, `Lab/post` and
  `Lab/bvh_march`.

- **C2.2 and C2.4, files, not bricks:** a stack is its folder's `.slang` files, named freely (`StackFile`), composed
  as one body under each file's label (`ShaderStack.Compose`, ordered by the first role each holds, then name). What
  a file does is its entries (`StackRole`: Update, Step, Graph, Trace, Deform, Shade, from `RolesOf`), each at most
  once in the stack; declarations sit in any file. The shader tool's brick strip is gone: `FileStrip` lists the files,
  coloured by role, with `+ FILE` (named as you type), rename and remove; GRAPH is the overview. `Lab/graph_board` is
  three files (`board`, `wires`, `hot`).

C2.3, STEP, UPDATE and TRACE gone (2026-11-25):

- **Roles:** `StackRole` is `Graph`, `Deform` and `Shade`; `NodeKind` is `Compute`, `Pixels`, `Draw`, `Present` and
  `Shade`. `deform` and `shade` are the two stages of a draw, kept as entries because a stack with only them is the
  common case: one draw of the item's quad onto the frame. Everything that was compute of a fixed shape is a node:
  - a pool (`[Pool] step` + `start`) is a kept buffer, a `[Pass]` over it that makes each slot on first use (a `made`
    buffer) and steps it, and a counted `[Draw]` (`swarm`, `klein_swarm`, `fountain`, `raymarch_pool`, `particles`);
  - an update is a `[Pass(1, over: 1)]` over a one-element kept buffer (`raymarch_tiled`'s `tiles`);
  - a trace is a `[Pixels]` pass into images and `present` (`trace_wave`, `bvh_march`'s `march`).
- **Gone:** the trace node and its two hidden images, `IndirectDraws`, `SlangShaders.Traces`, the pool half of
  `SlangShaders.Pools` (now `Places`), the stage's kept-state preamble and the implicit `state`, and their
  completions and lint.
- **Checked:** shots of all eight ported stacks in the shader tool; Slang tests pass but the known one.

Stage D, the rest (2026-11-25):

- **Caches:** `[Cached] Image<T> name;` is held like a `[Kept]` image (`ImageLife.Cached`). A node
  that touches only cached images (a draw's targets and reads too) and writes no buffer fills the cache
  (`GraphNode.Fills`, worked out when lowered): it runs only when the cache is stale, so the frame pays a read for it.
  Stale means just made (new compile, new rounded size, new item) or the material's stamp moved: its `Version` (values,
  textures) and `BufferVersion` (host buffers). The stamp lives on `HeldImages.Filled`; `ItemSet.Stale` says it for the
  item, `GraphRunner.Taken` for the material's own. A node reading a cache but writing anything else (a frame image)
  runs every frame, which is "cache the surface, keep the light live". The board says `cached` instead of threads and
  doesn't glow it. A fill that reads `time` freezes at the frame it filled: that's the contract, not checked.
- **Checked:** `Lab/cached`, a 600-step, 10-octave fbm range carved into cached `far` + `facing` and lit every frame by
  a circling sun; a throwaway probe showed its cached half lagging the live half by seconds, so the fill doesn't rerun.
- **Problems per material:** a `[Grid]` deform in a graph that works in the box (pixels, draws, present) is now a
  compile error on the `[Grid]` line (`DeformGrid.Check`), not a runtime message. A schedule its values make impossible
  keeps its reason on the lowered plan (`LoweredGraph.Plan.Problem`); `CanvasRenderer.GraphProblem(material)` reads
  it, and the shader tool lists it under PROBLEMS (`IEditorPreview.Problem`). No graph problem goes to `Console.Error`.
- **Levelled prelude passes: dropped.** SDL_GPU binds a compute pass's read-write images and buffers when the pass
  begins, by slot. Two nodes share a pass only if they write the same resources in the same slots, which nodes of
  different items or graphs never do. Caches and once-per-material passes are what cut prelude cost instead.
- **Not yet:** the stamp is the whole material's, so changing a value no fill reads (the range's `spin`) still refills
  it. Narrowing it to the values a fill's shader reads needs per-pass uniform use from reflection.

`present` as a library draw (2026-11-25):

- **Lowered before compiling** (`Slang/PresentDraws.cs`, called first in `CompileAsWritten`): each distinct
  `present(colour[, depth])` becomes a generated draw named for its images (`present_shown_depth`), the call replaced on
  its own line by `present_shown_depth(frame);`, the draws appended under the label `present (generated)`. A
  `BuildPresent` that reaches the walk was never compiled, and says so.
- **What it needed, now generic draw features:**
  - `[CoversBox]` before a `[Draw]`: the host hands it the item's box as its quad (`CompiledDraw.CoversBox`), so a turned
    item's images show whole; the draw sees the box as its canvas.
  - A shade struct field `float name : SV_Depth` writes the pixel's depth (pixels into the screen), put into the slice
    the way the vertex stage puts a corner's; it isn't a target. A draw onto the frame is one colour, plus maybe a depth.
  - `filtered(image, at, size)` in xanadu.stage: a bilinear read of a storage image, for an image shown at another
    size (a scaled or fixed one is stretched; its depth is read nearest). A box-sized one is read texel for pixel.
- **Gone:** `NodeKind.Present`, `ExitDraws.Present` and its pipelines, the flat-depth texture, `TracedView`,
  Rendering/Shaders/traced.slang, `Run.Present`/`Shown`/`ShownDepth`; and from xanadu.stage the dead `[Pool]` and
  `[Tile]` attributes and the old role names in its comments.
- **Checked:** shots of trace_wave, sizes (and sizes' quarter-size glow presented alone, stretched, against the last
  commit's), scene_room, bvh_march, trails, post and cached look as before.

Stage E, first slice: the mesh core (2026-11-25):

- **`[Cached]` buffers:** `[Cached] [Size(n)] List<T> name;` (`BuildBuffer.Cached`). A node fills
  caches when it touches at least one cached image or writes a cached buffer, every image it touches and every buffer
  it writes is cached, and every buffer it reads is cached or `[Host]` (`GraphPlan.Fills`, `Steady`). A material's fill
  passes run when its cached images or its cached buffers are stale; the buffers' stamp is `BuildBuffers.Filled`,
  asked once a frame (`GraphResources.Stale`), stale when just made or the material's stamp moved.
- **The pool** (xanadu.mesh, "The pool"): pages of `mesh.PAGE` (128) corners and 128 triangles, one object each.
  `MeshVertex` (position, normal, uv in the object's own space), `Meshlet` (object, triangles and corners used),
  triangles as three 8-bit corner numbers inside the page. A stack declares the three buffers, `[Cached]` for a mesh
  built once, plain for one rebuilt every frame.
- **Generators are passes:** `mesh.grid(surface, t, cells, first, object, …)` is thread t of building any
  `ISurface` as a grid into pages from `first` (8 × 8 cells a page, `mesh.pagesFor`), called from the stack's own
  `[Pass]` (a library pass can't take the stack's surface).
- **One vertex-pulled draw:** `[Draw(64, 1, shade: …, copies: pages)]`, a page a copy; `mesh.corner` pulls the
  corner for `v.index` from page `copy`, folding away triangles the page doesn't use. Two front doors over it:
  `mesh.pulled(v, placement, camera, …)` (3D: the object placed live by the draw, so a cached mesh still turns and
  moves) and `mesh.flat(v, offset, angle, scale, …)` (2D: x and y in pixels on the item, z the layer, unlit).
  `mesh.lit(i, camera, base)` lights a pixel from what the vertex stage handed on. `Stroke` (`curve.stroked(width)`)
  is the first 2D-native surface.
- **What draws gained for it:** `ItemVertex.world` / `normal` carried to the shade as `i.world` / `i.normal` (a draw's
  own varyings, so canvas items pay nothing), and `i.rest` in a draw's shade is the item's quad at rest from a uniform
  (`DrawBox` carries the quad), whatever uv the triangles carry.
- **Checked:** `Lab/meshes` (seven cached surfaces and a live ocean, two counted draws) and `Lab/flat_meshes` (cards
  and strokes sorted by layer whatever the draw order); a probe logged the cached generator filling only on its first
  frame and once when the tool set the values, over 150 frames; sizes, scene_room and copies look as before. Slang
  tests pass but the known one.
- **Then (same day):** the gate came off, and every Learn mesh stack moved onto the pool: `tour_mesh`,
  `tour_mesh_more`, `tour_mesh_2d`, `tour_mesh_overlap` (shapes that only turn in `[Cached]` pages, the ones whose
  shape moves rebuilt each frame into plain pages, two counted draws), `planet` (nine cached steps), `ocean`,
  `beat_tunnel` and `fold_on_beat` (rebuilt each frame). `tour_bends` is one `[Draw(31, 17, copies: 9)]`. Gone from
  xanadu.mesh: `deformed`, `mesh.split` and `Piece`; `place` stays (the pool's own draw and hand-made draws use it).
  `mesh.facing()` is now the GPU's triangle facing (`SV_IsFrontFace`, read in both fragment wrappers into
  `frontFacing`) instead of the uv-derivative winding (`uvWinding`, gone). The tours now look as they did or
  better: the overlap tour's see-through patches are gone and tour_mesh's terrain takes its colour from its height.
- **Not yet:** no culling per page yet (a page's bounds would
  let a pass drop pages off screen or facing away); no index-list generator (anything that isn't a grid); paging is
  static (each generator owns a fixed range of pages).

Stage E, second slice: the shared scene (2026-11-25):

- **Two images, not three:** every maker writes only `depth` (r32f) and `ids` (r32ui) (`Visibility` in xanadu.light).
  An id is its maker in the top two bits and then which of its things: `makers.sdf(object)` for a recorded scene's
  object, `makers.mesh(copy, drawnTriangle)` for one triangle of a pool page. `drawnTriangle` is new in xanadu.stage:
  a draw's vertex stage hands `index / 3` on, flat, to its shade. `Surfaces`, `SurfaceTexels` and `Surface.texels`
  are gone; `Surface` is a normal and a material.
- **One resolve, in the light pass:** `Visibility.at(pixel, camera, canvas)` reads a `Seen` (found, depth, id, the
  pixel's ray and world point). The maker rebuilds the surface: `scene.surface(parts, objects, seen)` takes the
  normal from that object's field and its material there; `seen.onMesh(placement, corners, triangles)` meets the ray
  with the pool triangle and blends its corners' normals and uvs. `Lighting.deferred(seen, surface, camera, caster)`
  lights it once.
- **Shadows from every maker:** `IShadows.shadow` now says which light it is asked about (`Lighting.SUN`,
  `Lighting.LAMP + k`). `ShadowMap` is one light's depth as a camera at the light sees it (`Camera(eye, focal)`, new:
  looking at the origin from a point), drawn by any maker's draw through that camera; nine taps, a bias of two texels.
  `both(a, b)` casts two together, so the room's marched shadows fall on the torus and the torus's map shadow falls
  on the room.
- **What draws gained:** a shade may return a bare `float`, `float2` or `uint` (one image: a shadow map's depth); a
  draw starts its depth test afresh when it writes different images from the draw before (`ItemSet.DrawnInto`), so a
  draw from a lamp doesn't test against the camera's depth.
- **Checked:** `Lab/scene_room` (the torus from the pool, the room marched only where nearer, one light pass, the
  torus shadowing the floor), a debug view of the map's shadow; the Slang tests pass but the known one.
- **Not yet:** shadow maps are one perspective view (a lamp lighting all round would want six); SDFs don't draw into
  a map (they cast through the march, which is exact); xanadu.scene still keeps its own record buffers (they are its
  authoring, not plumbing, now that `shoot` writes ids); screen-space AO and `room.box` are still to come.

The GRAPH view, prettified (2026-11-25):

- **Laid out as data flows, top to bottom** (`StackGraphView.Columns`, `Order`): each pass a layer below the latest
  write of what it reads, each resource a layer below its first writer (inputs nothing writes at the top, the frame
  and its depth always at the bottom), empty layers closed up, and each layer ordered by where its neighbours sit
  (four barycentre sweeps) so wires cross little.
- **Boxes say what they are**, coloured by kind: compute (threads, or `cached`), pixels, draws, the item's quad;
  buffers (bytes), images (`r32f · box`, `0.5× box`, `64×64`, kept / cached / last frame: `GraphResource.Detail`), the
  targets. A mid-frame pass says `breaks pass` and glows red. The generated present draw is just `present`.
- **Arrows say what the code does with an image:** `SpirvScan.Access(…, image: true)` follows a storage image through
  its loads to `OpImageRead` / `OpImageWrite` (and texel pointers), so a pass that only writes an image no longer
  shows read-write (`GraphPass.Kind` and the scan are what the view reads). `SpirvScan` has its own file.
- **A key** left of the graph: one small box per kind in its colour (compute, pixels, draw, item quad, buffer, image,
  target), framed and titled.
- **Checked:** GRAPH shots of scene_room, meshes, post and sizes against three bars: every box's text on two lines at
  most, no wire through a box (the board routes round them), and every colour named in the key. StackGraphTests pass
  (the march now only writes `marched`).

The shared scene's "Not yet", done (2026-11-25):

- **Lamp shadows all round** (`LampShadow` in xanadu.light): the six faces of a cube round the lamp, tiled 3 × 2 over
  one box-sized image, 90° each. A pool draw over six times its pages fills it (`LampShadow.pulled`: face
  `copy / pages`, page `copy % pages`; a triangle with a corner behind its face is folded, its shade
  `LampShadow.depthOf` writes only inside its face's tile). The lighting picks the face by the longest axis, nine taps,
  two texels of bias. `ShadowMap` (one view) stays for a spot or the sun. `ItemVertex.fold` is public for it.
- **Screen-space AO** (`ScreenOcclusion`): twelve points in the half ball over the pixel's point, closed where the
  shared depth drawn there is nearer (and not far in front); it is an `IShadows` with no shadows, so `both(...)` takes
  the less open of it and the scene's own SDF occlusion. Every maker gets it, meshes included.
- **`room.box`** is `Room(size)` in xanadu.scene: the inside of a block, floor at y = 0, added first.
- **Checked:** Lab/scene_room (`Room`, the six-face map of the torus shadowing the floor, AO in every corner and round
  every pin's foot), with a debug view of AO and the map; the Slang tests pass but the known one.

Still open from the shared scene: SDFs drawing into the lamp map is not needed (they cast through the march, exactly).
Next on the render graph: nothing planned in this doc beyond stage E; new work starts from the user.

Was: stage E on the confirmed drawing model, starting (user's call, 2026-11-25) with the mesh core: a paged meshlet
pool in kept buffers, generators as passes that rerun only when their inputs change (today's grid surfaces first, built
once), one vertex-pulled counted draw for every mesh, and 2D and 3D front doors over the same core; xanadu.mesh is
rebuilt onto it. Then the shared scene: SDFs and mesh draws writing one depth + id image, a resolve pass, one lighting
pass, shadows from every maker; xanadu.scene keeps its authoring and loses its plumbing, xanadu.light moves onto ids +
resolve.

Also still open:

- `scene_room` lost its `view` option (steps, tile lists, normals) when it moved to Surfaces.
- Faster again 11-25 (same measure): 5.1 → 2.5–3.3 ms with AO, 3.9 → 2.4 without, from two well-known moves:
  - **Coarse to fine** (demoscene cone / beam marching; Aaltonen's Claybook talk, GDC 2018): a `[Pixels]` pass over a
    1/8 image marches one cone per 8 × 8 block (`scene.coneDepth`: hits count once anything but the room comes within
    half the block's diagonal) and writes how far it got, backed off by the cone's radius; `scene.shoot` starts every
    pixel's march there (`start`). The room is met where each ray leaves it, so a cone reaching only the wall says
    nothing (`Shot.wall`). Shoot 1.1 → 0.6 ms for 0.05 of cones.
  - **Soft things at half size** (Wright, "Dynamic Occlusion with Signed Distance Fields", SIGGRAPH 2015):
    `ShadowMask` holds the sun, lamps 0 and 1, and the openness in one half-size rgba16f image, filled at each
    texel's top-left full-size pixel (`Visibility.under`), read back by the light pass from the four nearest texels,
    each weighted by nearness and by how like this pixel's its depth is, so edges don't bleed.
- Sped up 11-25 (Lab/scene_room filling a 1600 x 900 window, GPU ms a frame): 20 → 5.1 with AO, 12.5 → 3.9 without.
  The ray's object list was 16 entries in three arrays that spilled out of registers (`scene.RAY` 4 now, with
  `ROUNDS` 12 gathers; shadows keep their own `SHADOW_RAY` 16): shoot 7.3 → 2.8. A ray starting inside a plain
  inverted block (the room) meets it where the ray leaves it, worked out instead of marched, so walls and floor cost no
  steps (depth-only march): 2.8 → 1.1. A hollow (one plain inverted convex part) with both the point and the light
  inside is skipped by the shadow march: 1.8 ms off illuminate. Shoot stops within half a pixel and no longer works
  out a normal the resolve rebuilds anyway; shadow marches take 40 steps at most, stop once fully dark, and step no
  finer than 1% of the way travelled. scene_room has an `ao` switch.
- Fixed 11-25: the lighter squares round the pins were the tile lists. Occlusion asked only what a tile sees, so a
  floor beside a pin it didn't see lost the pin's darkening at the pin's screen box; and a shadow's soft edge spread
  past the swept box a tile lists casters by. A tile now lists objects within `Lighting.OCCLUSION_REACH` of its view,
  and the sweep grows by the penumbra (far / hardness).

## Stage F: across materials, views, meshes, limits (decided 2026-11-26)

The user's picks, in the order they're built:

1. **More values per call.** `RunValues` and a pass's uint arguments go past four (the pass uniforms grow with it).
2. **Packing draw outputs.** Up to four outputs stay a target each. Past four, narrow outputs share a target's
   channels where they can (same size and lifetime, a format that holds them), written with per-channel masks and
   read back by channel. Nothing is refused for its type: an output that can't share keeps a target of its own.
3. **The mesh pool:** culling per page (bounds per page, a pass compacting the pages a camera sees into a counted
   list the draw reads), a generator for triangle lists (anything that isn't a grid), and pages taken as needed
   instead of fixed ranges per generator.
4. **`[Shared]` resources:** any graph declaring `[Shared] Image<T> name;` or `[Shared] [Size(n)] List<T> name;` gets
   the one frame-wide resource of that name and type, whatever its material. Writers run before readers across
   materials (ordered as UE5's RDG would). A shared image is screen-sized or fixed.
5. **`[View]` images:** `[View] Image<float4> name;` is filled before the frame by drawing a canvas subtree the game
   names for that material, with its own transform (zoom, offset, flip): reflections, a minimap, a second camera.
   Built on the offscreen roots.
6. **A renderer post list:** the host lists post materials on the renderer; each runs after the tree as a full-screen
   item, in order, reading `screen`.

### Stage F, where it stands (2026-11-26, uncommitted)

- **1, values: done.** `StackBuild.MaxArguments` is 16: a pass takes up to 16 uints (`uint4 args[4]` in its wrapper,
  `PassUniforms.Arguments` the `RunValues` inline array), and a call carries up to 16 draw targets.
- **2, packing: done.** `Slang/Texels.cs` is the one table of image types and formats; images now also hold `uint2`,
  `uint4`, `int`, `int2`, `int4`, and a `[Precise]` (rg32f) or `[Compact]` (rg8) `float2`. A draw may return up to 16
  outputs: `DrawLayout` keeps four or fewer a target each, and past four packs narrow outputs of one kind into shared
  targets, first fit. `ImagePacking` turns the images a call names for one shared target into one texture (named
  `a+b+c+d` on the board, cleared channel by channel), reached in code through a struct per member (`name[pixel]`,
  `name.GetDimensions`). It refuses with a message where packing would change what the graph does: different sizes or
  lives, a history on either side, a member passed on whole, or another draw writing part of a pack. Tests:
  `Slang/Tests/ImagePackingTests.cs`.
- **3, the mesh pool: done.** `Meshlet` carries bounds (a ball, and a normal cone); `pool.bound(n)` works them out after
  building; `pool.cull(n, placed, camera, halfView, seen)` / `pool.sees` keep the pages a camera sees in a
  `Gathered<uint>` the draw counts by (`copies: seen`, `pool.pulled(..., page)`); `oneSided` also drops pages facing
  away. `pool.list(ITriangles, …)` builds any triangle list (`CornerList`, `IndexedList`), 42 triangles a page. Pages are
  taken as needed: `pool.clear()`, `pool.take(n)`, `pool.place(object, first)`, `pool.taken(i, most)`, and `copies:
  pool` draws the pages taken; the counts live in the pool's own cached `book` list, so a cached pool's claims stay
  fills. Lab: `Lab/culled_meshes`.
  - **Watch:** the user saw flicker while testing it with `VIEW` shrunk to 300×200 (the cull boundary inside the
    window). With `VIEW` back at the stage's 960×540 it "seems better"; a still scene gives identical frames across runs
    and the cached fill runs only twice, so no race or reshuffle was found. If pages blink well inside the screen,
    look at the cull test against the draw's projection first. A material-scope cull pass can't know the item's box,
    so `halfView` is the author's.
- **4, `[Shared]`: done.** `[Shared] Image<T> name;` and `[Shared] [Size(n)] List<T> name;` are the frame's: every
  graph declaring one of that name and format (an image) or element type (a list) gets the same.
  - **Images** are the screen's size (`[Scale(s)]` scales the screen, `[Size(w, h)]` is fixed), taken from the image
    pool and cleared at their first use in the frame (`ItemImages.FrameShared`), read and written at `i.pixel`, not
    `group.pixel`. Any pass may touch one, a `[Pass]` too, whatever its size. A draw can't render into one (its depth is
    the box's) and `over:` a screen-sized one is refused; `present` reads one at the pixel. A later declaration with
    another size or clear gets its own texture and says so under PROBLEMS (`CanvasRenderer.GraphProblem`).
  - **Lists** are kept from frame to frame like any list, zeroed when made, as big as the biggest declaration: a bigger
    one makes it again and the materials holding the old one move over (`BuildBuffers.SharedGeneration`). They stay when a
    material goes. Neither kind can be `[Kept]`, `[Cached]`, `[Host]` or a history.
  - **Writers before readers:** `SharedUse` (Slang) lists each graph's shared writes and reads over all its passes, from
    the code (`CompiledPass.ImageAccess` now carries each image's scanned access, which the board reads too).
    `SharedOrder` runs the frame's preludes level by level, a material one past the writers it reads from, tree order
    within a level and in a cycle. Exits run after every prelude, so they see every writer. An exit that reads images
    takes its item's set in the prelude, so a graph that only reads a shared image at its exit draws.
  - **The board** says `screen · shared` on an image and `shared` on a list.
  - **The tool:** `--beside=<stack>` draws a second stack after the open one over the same quad (two materials).
  - **Checked:** `Lab/shared_look` opened with `--beside=Lab/shared_paint`: the reader comes first in tree order, yet
    shows the writer's heat and rings round its blobs the same frame. Alone, its heat is dark (cleared). Its rings
    stay from whatever wrote `spots` last (the writer's card), as a list keeps its contents. Tests:
    `Slang/Tests/SharedResourceTests.cs`. Suites after it: Slang 402 pass (1 skipped), game 209 (14 skipped), UI 456.
  - **Not yet:** a mid-frame writer is not ordered against other materials' preludes; a shared image is never drawn
    into (it would need a screen-sized draw depth).
- **5, `[View]`: done.** `[View] Image<float4> name;` takes nothing else: it is rgba8 (the canvas's own format, so the
  view draws straight into it), the screen's size and read at `i.pixel` like a shared image (`BuildImage.Screen`). The
  graph only reads it: a pass writing it or a draw into it is refused. The game names it per material with
  `CanvasMaterial.SetView(name, root, transform)`: the subtree under `root`, placed by the tree's transforms above it, then
  by `transform` on the screen; a view nothing names is clear.
  - **The frame:** `MaterialViews` looks through the collected jobs for materials naming views, collects a job per
    (material, name) into a held screen-sized texture, views inside views first, each once a frame, and puts them before
    the other jobs. The view's preludes run, the views draw, then everyone else's preludes and jobs
    (`GraphRunner.Begin` / `Prelude(from, end)`), so any node may read a view, a `[Pass]` too. The viewing material's
    own items are left out of its views (`Job.Viewer`). An item drawn in a view keeps its own effect mesh and held
    images there (`MeshOf` is keyed by `Job.View` too), since it is placed differently.
  - **The tool** names every `[View]` of the open stack: the backdrop's subtree, flipped about the preview's middle.
  - **Checked:** `Lab/view_mirror` opened with `--beside=Lab/view_scene`: the sun and hills show mirrored in the water,
    rippled. Tests: `Slang/Tests/ViewImageTests.cs` (which caught `present(view)` being refused as a write).
  - **Not yet:** a view draws single-sampled at the screen's size (no `[Scale]`); a material that only shows inside
    another view runs its preludes before its own views draw; particles step once, after the views' preludes.
- **6, the post list: done.** `CanvasRenderer.AddPost(material)` / `RemovePost` keep a full-screen rect per post material
  (an item of the tree with no parent), put after everything else in the main job each frame, in the order they were
  added (`CanvasCollect.Last`). It is an ordinary item, so a post stack is any stack: a shade reading `sampleScreen`
  costs one screen copy, a graph runs like any other, its views too.
  - **The tool:** `--post=<stack>` puts a stack on the list. `Lab/post_grade` grades, vignettes and scanlines the window.
  - **Checked:** a shot of `Lab/view_mirror --beside=Lab/view_scene --post=Lab/post_grade`: the grade covers the whole
    window, editor and all, from one screen copy. Suites: Slang 413 (1 skipped), game 209 (14 skipped), UI 456.
  - **Not yet:** the game puts nothing on the list yet; a post item is always the window's size.
- **Stage F is done.**

### Sampled reads (2026-11-26, uncommitted)

- `sampled(image, at, size)` reads a float4 image between its texels like `filtered` (`at` in texels, clamped inside
  `size`). In a `[Pixels]` pass it goes through the GPU's bilinear filter: the rewrite (`StackBuild.Rewrite`, `sampled:`)
  gives each float4 image a `Sampler2D` companion in set 0 and points the calls naming it there; the ones a pass uses get
  samplers after the canvas's two (`CompiledPass.Sampled`), the globals after them, and the runner binds them
  (`ComputeNodes.Pixels`). Anywhere else `sampled` is the library's, which is `filtered`.
- A pass can't read an image through `sampled` and touch it as an image too. The board, shared ordering and cache fills
  count a sampled image as read.
- **Checked:** `Lab/dual_kawase --under=Lab/test_card` looks the same as with `filtered`, at half the GPU time (chain
  0.163 → 0.078 ms, the last step 0.120 → 0.063 ms, full screen). Tests: `Slang/Tests/SampledImageTests.cs`.
- **Handles:** `sampled(glow)` hands out a `Sampled` a helper can take (`float4 blur(Sampled image, …)`, read with
  `image.at(texel, size)`), so a chain of steps shares its taps (`Lab/dual_kawase`). In a `[Pixels]` pass it is the
  image's sampler; anywhere else, or on something that isn't one of the graph's float4 images, it is a stand-in on no
  sampler (`StackBuild.NoSampler`), and any stage reaching it is refused with a message. Reflection leaves samplers
  parked at 159 and up out of a stage's count: declared in every stage, they had made a draw's vertex pipeline ask for
  160 samplers and crash the driver.
- **Not yet:** draws and `[Pass]`es filter by hand (`sampled(image, at, size)`); float4 images only; a packed one can't
  be sampled.

## Blocks (proposed and first slice built 2026-11-26, uncommitted)

A stack's files compose into one body: every global is everyone's, nothing is used twice, so splitting a big stack into
files is partial classes, not structure. The library holds no state ("the library owns no buffer"), so machinery that
needs lists (a field, a pool, events) can't live there either. A **block** is the layer between: a struct in a library
module that owns its lists, its passes and its functions, used by declaring it.

```hlsl
// Library/xanadu/field.slang
[Block(w, h)]
public struct Field
{
    uint w, h;                                // the declaration's size: [Size(640, 360)] Field name;
    [Size(w * h)] List<float> cells;          // this frame's
    [History(cells)] List<float> before;      // last frame's
    [Size(w * h)] List<uint> deposits;

    public float at(float2 uv) { … }          // reads before
    public void deposit(float2 uv, float amount) { … }
}

[Pass(64, over: w * h)]
public void spread(uint i, Field f, float fade, float amount) { … }

// a stack
[Size(640, 360)] Field field;
field.deposit(uv, 1);                         // in any pass or draw
void graph() { …; field.spread(fade, spread); }
```

- **Lowering:** a declaration lays out the block's lists as `name_part` (as bundles do today), with its histories and
  stored values, and a property by its name that builds the struct from them. Methods are the struct's own.
- **A block's passes** take the block as a parameter; `field.spread(…)` in graph() calls the pass with that instance,
  its lists bound, its size names (`w`, `h`) in scope for `over:`.
- **Pass arguments take floats**, not only uints: `fade` is a uniform passed straight through.
- **Where:** library modules first; a stack's own folder later (a file marked as a block, not composed into the body).
- **Bundles move onto it:** `Meshes` became a block; `Slots` and `Gathered` stay built in (blocks hold bundles, below).
- **First user:** `Lab/particles`, as `Field`, then `Pool`, `Events` (deaths) and `Trails`.

### Where blocks are (2026-11-26)

- **Built:** a library struct marked `[Block(sizes…)]` (`BuildBlocks`) holds `uint` sizes, then `[Size(n)] List<T>`,
  `[History(list)] List<T>`, `Stored<T>` and `Counter` parts, then its functions. Slang sees a plain struct: the marks are
  blanked and a constructor taking every field is added (`BuildBlocks.Prepared`, through `StackLibrary.Prepared`); a
  Stored part is read as its one element inside the body. Generic over one type: `Events<T>`, declared `Events<Death>`.
- **A stack declares one** at its top level, `[Size(640, 360)] Field field;` (`BuildDeclarations.Blocks`; the type found in
  any library module, `StackLibrary.Block`). It lays out as `name_part` lists, histories and stored values, and a property
  by its name that builds the struct (the bundle machinery: `BuildBundle.Block`, `.Sizes`).
- **A block's pass** takes the block first after `i`; `field.spread(fade, spread)` in graph() runs it on that declaration
  (`StackBuild.BlockCall`), the block's sizes in sight of `over:`, one pass per declaration (`BuildPass.Instance`;
  the board calls it `field.spread`).
- **Pass arguments take floats and ints** (`BuildParameter.Scalar`): a float goes as its bits and the wrapper reads it with
  `asfloat`. Brick passes too.
- **Shipped blocks:** `Field` (`xanadu.field`: deposit, at, slope, shown; pass `spread`), `Events<T>` (`xanadu.store`:
  turn, add, heard, `[i]` last frame's), `Trails` (`xanadu.particles`: turn, start, record, back).
- **`Lab/particles`** runs on all three: no field cells, deposit scale, death counts or trail head left in the stack, and
  its `Book` is down to the emitters' plan. Shots at 240 and 600 look as before (fountain pooling in the field, shells
  bursting from last frame's deaths and crackling, trails on rockets, shells and drops). Tests: `Slang/Tests/BlockTests.cs`.
- **Blocks hold bundles** (user's pick, 2026-11-26): a block's `[Size(n)] Slots` and `[Size(n)] Gathered<T>` parts are
  laid out in the stack's shared bookkeeping list as a stack's own are (`BundleKinds.Lay` from `BuildBlocks.Lay`), so
  every count still costs one binding against SDL_GPU's 8 a pass. Slots and Gathered stay built in; `BundleKinds` stays.
- **`Pool<T>`** (`xanadu.store`): items, free places (Slots) and the frame's live ones (Gathered<uint>); take, give,
  live, living, count, turn, `[i]` and `put(i, value)`. A block reaches the stack through a read-only property, so a
  subscript setter can't be called on it: blocks write with methods. `Lab/particles` runs on it (shot at 600 as before).
- **`Meshes` is a block** (2026-11-26, `xanadu.mesh`): `[Cached] [Size(PAGES)] Meshes pool;` replaces the built-in
  `[Pages(n)]` kind, which is gone (`BundleKinds` keeps Slots and Gathered only). Two things blocks gained for it:
  - **`[Cached]` on a declaration** caches every list of the block; a block holding a history, Slots or Gathered changes
    every frame and refuses it (`BlockType.Cacheable`).
  - **`[Copies]` on one part** (a Counter, a Gathered, or a `[Size(n)] List<uint>`, its first uint) is what `copies: name`
    draws by: `Meshes.book` (pages taken), `Pool.alive` (`copies: pool` draws once per live one). A draw by a block with
    no `[Copies]` part is refused.
  Every mesh stack moved over; `Lab/meshes` and `Lab/culled_meshes` shot as before. Tests: `BlockTests` (cached, copies).
- **Not yet:** a block's pass is called on one block only, by its first parameter; a library schedule can't call block
  passes; block passes can't be generic.

## Probes and reports (2026-11-26, slice 1 of Docs/gpu-clips.md)

Two graph features the editor's timeline needed, usable by any graph:

- **`[Reported]`** on a `List<T>` or `Stored<T>`: the host reads it back a few frames late, without waiting on the GPU,
  into `CanvasMaterial.Reports` (`Rendering/Passes/GraphReports.cs`: a ring of 4 download buffers a material, copied
  after the frame's last draw). For counts the CPU sizes something by, never for data it draws from.
- **Probe nodes** (`Slang/ProbeNodes.cs`): a body declaring `uniform float4 xanaduProbe;` gets a generated pass per draw
  that runs the draw's own vertex function at sampled times into the shared list `xanaduProbes`. Lowered like
  `present`: source in, source out, then the graph is read as usual, so it shows on the board, binds like any pass and
  orders against its readers through `[Shared]`. Details and what's left: Docs/gpu-clips.md.

## The resident UI on the graph (plan, 2026-11-26)

The resident UI (Docs/host.md §5.3) is the biggest GPU path left outside the graph: hand-ordered compute and draws in
`CanvasRenderer` / `StepBuilder` / `PlacedPrimitives` / `ResidentTrees` / `DrawPass`, not a stack. Putting it on the graph
is dogfooding: the graph running the one thing drawn every frame on every screen, at the 20,000-cell CJK lab's scale
(18.7 → 0.5 ms today, the bar to hold).

### What it was (before stage 1)

One plain-primitive path, shared by the resident trees and every CPU-placed item:

- **Kept:** the primitive pool (`PrimitivePool`: every recording's primitives in its own space, ranges sent when their
  hash moves); per tree, its nodes, entries, run slots, pieces and patches (`ResidentGpu`).
- **Each frame, compute:** `resident_patch` (changed slots into the nodes), `resident_resolve` (each drawn slot's
  ancestors → an instance after the CPU's), `primitive_expand` (an instance a workgroup → placed primitives in pixels
  and order, plus the pre-pass list).
- **Each job, draws:** the opaque pre-pass (one draw over the job's pre-pass slots, depth written), then each run
  (`StepKind.Primitives`: a range of placed primitives and its eight texture slots) at its place among the job's
  other steps, depth tested.

### The shape on the graph

The canvas becomes **one engine stack** (`Assets/Shaders/Engine/canvas/`, shown on the board like any other) that the
renderer owns a material for:

- **Lists:** `pool`, `instances` (the CPU's), `slots`, the trees' `nodes` / `entries` / `runSlots` / `pieces` /
  `patches` as `[Host]` lists; `placed`, `opaque` as frame lists. All sized at run time.
- **Passes:** `patch`, `resolve`, `expand` as `[Pass]`es, ordered by what they read and write.
- **Draws:** `opaque` (copies over the job's pre-pass range, depth written) and `ordered` (copies over a run's range,
  eight textures by slot plus the glyph layers), each an exit drawn at many places in the frame.
- **Trees in one store** (decided 2026-11-26, with the whole plain path as the scope): every tree's nodes, entries and run slots are ranges of the same lists, as
  recordings are of the pool, and an entry names its tree's mount. One resolve dispatch for all trees instead of one
  per tree, and no need for several blocks of one kind.

### What the graph grows for it

1. **Host lists sized at run time, with ranged writes.** `[Host] List<T> name;` with no `[Size]`: the host sets its
   length (it grows, kept contents copied) and writes ranges into it (`material.WriteBuffer(name, at, span)`), kept
   between frames, so a pool range sent once stays. Frame lists sized by a value (`[Size(placedCount)]`).
2. **Exits with no item quad.** A draw of copies in target pixels, drawn from the canvas material's steps, not an
   item's kept mesh.
3. **One exit drawn at many places.** A step names a range of the exit's copies (first, count) and the draw covers
   only that range; the canvas's runs become steps of its material.
4. **Textures by slot.** A draw samples up to eight textures each step names, plus a layer array (the glyph pages).
5. **Depth writes from an exit** (`[Draw(…, depth: write)]`), drawn first in its job.

### Stages

- **1. The compute half.** The engine stack holds the lists and the three passes; the renderer fills its host lists
  and runs them through `GraphRunner` / `ComputeNodes` (feature 1), the trees in one store. `ResidentTrees.Resolve`, `PlacedPrimitives.Expand`
  and their pipelines are deleted; `DrawPass` still draws, reading the stack's `placed` / `opaque` buffers.
- **2. The draws.** `opaque` and `ordered` become the stack's exits (features 2–5); `PrimitivePipelines`,
  `primitives.slang`'s hand pipeline and `StepKind.Primitives` go.
- **3. The CPU side (`UiResidentSync`, pieces, the hash) moved wherever the
  graph gives it a better shape.

The bar: frame times on the CJK lab and the editor no worse than today, and the old hand-written path deleted, not
kept beside it.

### Stage 1, where it stands (2026-11-26, committed)

- **Lists the host sizes** (feature 1): a `List<T>` with no `[Size]` (`[Host]` before it or nothing) is sized at run
  time (`BuildBuffer.HostSized`; its compiled bytes are one element's). The host side is `CanvasMaterial.Lists`
  (`UI/Systems/Canvas/HostLists.cs`, replacing `SetBuffer`): `Set` (whole data from the start), `Write` (a range,
  kept on the GPU) and `Size` (a length; the buffer grows ×2, copying what it held). `GraphResources.Upload` grows
  first, then sends whole data whose version moved and the writes, from one transfer piece.
- **A material the host runs:** `GraphRunner.RunHosted` sends its lists and runs its graph's compute nodes now, for a
  material no item shows.
- **The canvas stack** (`Assets/Shaders/Engine/canvas`): `lists.slang` (the records and lists), `trees.slang`
  (`patchNodes`, `resolve`), `expand.slang`, `graph.slang`. The renderer owns it as `CanvasStack`
  (`Rendering/Passes`), runs it after the globals upload and hands `DrawPass` its `placed` and `opaque`.
- **Trees in one store:** `ResidentTrees` gives each tree ranges of `nodes`, `entries` and `runSlots` (a bump store;
  when more than half is waste, every tree takes a fresh range and sends everything) and an index into `trees`
  (`TreeMount`: the mount's place and the tree's bases) that its entries name. One `resolve` over every entry; a
  tree not drawn, or an entry past its tree's count, is skipped.
- **Gone:** `resident_patch.slang`, `resident_resolve.slang`, `primitive_expand.slang` and their pipelines, the
  pool's and the instances' own GPU buffers, per-tree buffers.
- **Checked:** song select, the editor and the CJK lab at 20,000 cells look as before (the CJK shot is the same as the
  old path's). CJK lab, mean GPU canvas 0.407 ms (stack 0.239 of it: expand 0.084, resolve 0.030, the rest its
  uploads) against 0.399 ms on the old path (uploads 0.126, expand 0.108); record + submit 1.38 against 1.29 ms.
  Suites: Slang 483 (37 skipped), UI 473, game 209 (14 skipped).
- **Not yet:** graph lists are bound without cycling, so a frame's `placed` and `opaque` wait for the last frame's
  draws (the old path cycled them); a host-sized list never shrinks; the stack recompiled live (the shader tool) starts
  on fresh lists, so what was only written in ranges (the pool, the trees) would need sending again.

### Stage 2, where it stands (2026-11-26)

- **The draws are the stack's exits** (`draws.slang`): `prepass` (`[Opaque] [OrderDepth]`, the job's opaque rects)
  and `ordered` (`[Translucent] [OrderDepth]`, a run of placed primitives), both `[Draw(1, 1, …)]` whose deform places
  a copy's corners from `placed` / `opaque` and whose shade reads the record back by `copy` (the old varyings' shape,
  kind and clip come from the list, not interpolants). `graph()` calls them onto the frame; the host draws them.
- **What the graph grew** (features 2–5, every draw has them):
  - **A range of copies:** `XanaduDrawQuad.firstCopy`, and a copy is also `index / vertices` so a host can draw copies
    flat in one instance (`ImageDraws.Hosted`: small grids pack better that way than as tiny instances).
  - **No item quad:** `ImageDraws.Hosted` draws a hosted material's draw over the whole target and depth.
  - **Textures by slot:** a shade's own samplers (from 3: `ImageDraws.OwnSamplers`) take what the host hands; the
    canvas's eight run textures and the glyph pages' layer array.
  - **`[Opaque]`:** writes the depth, no blend, never discards (early depth stays on), and drawable depth only
    (`CanvasEffect.DepthPipeline`, the job's depth for effects that read it). Not with `[Translucent]`.
  - **`[OrderDepth]`:** the deform's `depth` is its z in the slice as it is, not pixels squashed, so the canvas keeps
    each primitive's exact place.
- **Steps:** a run is `StepKind.Canvas` (copies `Start`..`Count` of `ordered`, eight textures from `FirstTexture`);
  `CanvasStack.Prepass` / `Run` draw them. `PrimitivePipelines`, `primitives.slang` and `StepKind.Primitives` are gone.
- **Checked:** song select, the editor, the depth lab (GPU validation on) and the CJK lab at 20,000 cells look as
  before. CJK lab, mean GPU canvas 0.331 ms (main target 0.138, stack 0.188: expand 0.070, resolve 0.028) against
  0.407 at stage 1; record + submit 1.55 against 1.38 ms (each run now binds as a graph draw does). Suites: Slang 483
  (37 skipped), UI 473, game 209 (14 skipped).
- **Not yet:** the stage-1 leftovers (lists bound without cycling, never shrinking, a live recompile starting on
  fresh lists); a run's binding cost on the CPU. Stage 3 (the CPU side) is next.
