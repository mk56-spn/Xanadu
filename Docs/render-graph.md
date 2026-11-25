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

## The Lab-only gate

The game already draws through the trivial path. Compute nodes, pools and graphs are refused outside `Lab/` today
(the `new_shader_8` failure). That gate comes off once stage B is in and measured.

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
- **D. Cost.** Flatten static graphs; batch prelude compute into levelled passes; problems per material.
- **E. Then the 3D work resumes** on top: `room.box`, shadow depth per light from the same geometry, screen-space
  AO, `[Surfaces]` sugar if it's still wanted once clears live on declarations.

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
- **Clears:** `[Image(format, clear: value)]`; `clear()` is gone from the schedule language.
- **Checked:** scene_room, bvh_march and trace_wave look as before; two items on scene_room's material (a smaller
  second copy in the preview) each march, draw and light their own box over one recording.

Stage B, second half (2026-11-25):

- **`[Kept] [Image(format)] RWTexture2D<T> name;`** holds its texels from frame to frame, cleared only when made.
- **`[History(source)] RWTexture2D<T> name;`** is what `source` held at the end of last frame (its format and clear
  come from the source). The two swap textures each frame and the source is cleared: nothing is copied. A `[Kept]`
  image takes no history (it already has last frame's texels).
- **Where they live:** `HeldImages` (`Rendering/Kept/HeldImages.cs`), a relation on the entity keeping the item's
  range (`KeptMesh`, keyed `(item, from)`), one per piece, keyed by where the piece starts. Steps carry that entity
  (`Step.Kept`). Held images are made again for a new compile or a new rounded size, and let go with the range.
- **Checked:** `Lab/trails`, a comet whose trail fades through `[History]` over paint spots that pile up in a
  `[Kept]` image on the beat.

Stage B, sizes (2026-11-25):

- **`[Image(format, scale: s)]`** is the box times s, rounded up (s above 0, at most 4). **`[Image(format, w, h)]`**
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
- **Checked:** `Lab/sizes`' band is drawn onto the frame over the present, reading the glow; scene_room with a still
  torus matches the previous commit's shot.

Visual tests: nine scene tests (`ui-tree`, `song-select`, `settings`, `pause`, …) time out waiting for their screen;
they fail the same way at `ade5f91c`, before this work.

Next: argument lists.

Also still open:

- `scene_room` lost its `view` option (steps, tile lists, normals) when it moved to Surfaces.
- Faint lighter squares on the floor round some pins predate this work (AO or tile lists).

## Known failures (not from this work)

- `new_shader_8` has a STEP brick outside `Lab/` (two Slang test failures).
- `APlainFloat4StaysAnError` fails with or without the scene module.
