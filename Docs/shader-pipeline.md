# Shader pipeline: the top-level picture

What an effect is made of, from what it reads to where it lands. The shader
API (phases A–D) and the tutorial (`Docs/shaders.md`) build on this. The host
side is in `Docs/host.md`.

```
 0 GLOBALS ──► 1 PARAMS ──► 2 KEPT STATE ──► 3 DEFORM ──► 4 SHADE  ──► 5 BLEND ──► 6 WHERE IT LANDS
 (read-only,   (per effect,   (per effect,     user chunk    user chunk     user chunk
  everyone)     read-only)     writable)
                                     │
                            7 PARTICLES: kept state × N, drawn through 3 → 4 → 5
```

Data only flows down. Kept state (2, and per particle in 7) is the only thing
an effect writes, and it belongs to that effect.

**Stacks of bricks (built 2026-11-24).** The unit is a *shader stack*: a folder
with one file per user chunk, a *brick* (`Slang/ShaderStack.cs`). `update.slang`
is box 2, `step.slang` is box 7's kept state × N, `deform.slang` is box 3 and
`shade.slang` is box 4; they run in that order and each reads what the
bricks above it declare (they compile as one body, each under its own `#line`
label). Most stacks are a shade brick alone. Blend (5) isn't a brick: it's
set where the stack is placed (6). The shader tool edits a stack around this
pipeline: a strip over the code shows the bricks in order, adds and removes them.

## The API sits outside the game

The shader API knows nothing about rhythm. The game consumes it like any other
client would. The API supplies the mechanism: declare a global, give it a type
(uniform values, or a permanent storage-buffer struct of any shape), a name and
one writer, and every effect can read it. The game then declares its own
globals through that mechanism, wherever it likes: "I want this permanent SSBO
struct as a global". The chart, the play state and the event clocks are the
game's declarations, not part of the API.

- In the API: the pipeline (globals mechanism, params, kept state, vertex,
  fragment, blend, compute levels, particles), engine-level globals (time, dt,
  mouse, screen, depth) and the xanadu.* maths, shape and paint library.
- In the game: everything rhythm (chart, notes, judgements, player, score,
  combo, beat, event clocks, keys as play input, spectrum) and the attach
  points that are about play (on-hit, per-note, player, trail).
- **How a global is declared (decided):** markers inside the shader code, and
  the API handles the rest. The declarer ships a Slang module whose struct
  carries a `[Global]` marker, and registers one writer for it. The API
  assigns the binding, so the declarer never picks numbers. "Only ever add"
  applies to each declarer's own module.
- **A global that isn't declared where the effect runs (decided):** the effect
  fails to compile with a clear error ("needs `rhythm`"). A host that wants to
  run it anyway, such as the lab, supplies stand-ins under the same
  declaration (the lab's fake chart works this way today).
- Built 2026-11-20: `xanadu` has no rhythm left in it. The game's modules are
  `Assets/Shaders/Library/rhythm/` (`import rhythm;`), declaring `playFrame` and
  `chart`. Their C# side is `Core/Rendering/Rhythm/` (`RhythmGlobals`). Clocks
  count from anything `ISince` (`clock.since(…)`), so `xanadu.anim` doesn't
  know about keys, events or notes.

## 0. Globals: read-only, anyone reads

One set per frame, and every effect shares it. Each global has one writer: the
engine, or whoever declared it (see above).

| Source | Who declares it | What |
|---|---|
| `Frame` (uniform block) | engine | screen size, time, beat (whoever plays music sets it: `ShaderClock.Beat`) |
| `playFrame` (`rhythm.play`) | game | player position/velocity/state, score, combo, accuracy, song, event clocks, play keys |
| `chart` (`rhythm.play`) | game | the whole chart, one `ChartNote` per note, with results written in as notes are judged |
| **depth buffer** | engine | the frame's depth so far, see below |

- Rule: only ever add. Each value has one writer.
- Status: built, depth reads included. The spectrum (`energy`/`reactive`) is
  still bespoke to note_background. Publishing it as a game global is next.
- Open: what else is global (spectrum, the groups' current values, song
  sections, …).

### Depth: every vertex and fragment stage can read it

Any `deform` and any `shade` can read the depth buffer, like they read the
screen. It's a global, the same for everyone, never something an effect opts
into through the host.

- Its place in the frame: every primitive has the depth of its place (host.md
  §5.3). Opaque rects write depth in the pre-pass. Effects and particles test it
  and never write it, except an effect with its own `deform`.
- What's known on the host side (host.md): a depth texture can be a target and
  can be sampled later (Labs' depth lab). A depth read in the middle of a pass
  goes the way screen reads do: end the pass, copy, resume.
- What's known on the host side (host.md): MSAA depth can't be resolved.
- Built 2026-11-20 as `sampleDepth(uv)` (Docs/shaders.md §13): before a job
  that reads depth, the host draws the opaque pre-pass again, depth only, into
  a single-sampled texture it can sample. So reads see the opaque rects only;
  a deform's own depth isn't in it. `update` can't read the depth.

## 1. Params: per effect, read-only

`uniform` values with `[Default]`, shown as sliders. Group shader tracks key
them. Only the author or editor sets them; the shader can't write them.
Status: built.

## 2. Kept state: a small attached permanent struct

```slang
struct State { … };
void update(inout State s, float dt) { … }
```

- `update` runs once a frame on one GPU thread before the draw. Every vertex
  and pixel then reads `state`.
- Starts zeroed, up to 16 KB, one per material. A recompile starts it again
  from zero.
- Forward-only: it pauses with the song and never rolls back on a seek, and
  never will. Planned: a signed `seeked`, opt-in
  clear-on-seek, and an editor-only exact reseek that runs it again from 0 at speed.
- Status: lab only (`flashlight`, `raymarch_tiled`). In a level, an `update` is
  an error until the level runtime passes a paused `dt`.
- Open: one per material or one per placed item. Can other effects read it?

## 3. Deform: the vertex stage (user chunk, `deform.slang`)

Moves the quad's corners. Reads 0, 1, 2 and depth. Varyings to fragment:
32 vec4 is the real cap. No auto-packing.

## 4. Shade: the fragment stage (user chunk, `shade.slang`)

The pixel colour. Reads 0, 1, 2, the varyings, textures, the screen and depth.

### Textures: TBD

The game is mostly meshes. Textures will mostly not be external assets.
They'll come from noise, a previous shader's output and so on. How an effect
gets one is TBD.

## 5. Blend (user chunk, static)

Mix, add, sub, mul, premultiplied. It's pipeline state and never keyed. One
variant per (shader, blend), built the first time it's needed.

## 6. Where it lands

A placed item, a buffered group (its own tint, alpha, shader and blend), a
layer pass, or an attach point: on-hit, per-note, player, trail, background,
UI panel. Attach points are phase C and need the user's review. An attach point
fixes the quad and adds its own standard inputs (such as "the note this pop is
for").

### Attach points: placeholder examples

These are only examples. Gameplay is being reworked, so the real list waits
until that settles. The API supplies the mechanism: a named place that fixes
an effect's quad, its lifetime and a few extra inputs. The game declares the
places with markers, the same way it declares globals.

**`OnHit`**: the game's example, standing in for today's hit_bar placement.
- Quad: a column the height of the field, centred on the note that was hit.
- Lifetime: one instance per hit. It ends when `shade` returns transparent, or
  after a cap (1 s).
- Inputs: `hit.note` (the chart `Note`, which replaces hit_bar's `hit_note`
  uniform) and `hit.since` (seconds since the hit).

```slang
[AttachTo(OnHit)]
float4 shade(Canvas canvas)
{
    Clip life = hit.since.clip(0, 0.26);
    …  hit.note.colour …
}
```

**`Background`**: the API's example, since it's about the frame, not about play.
- Quad: the whole view, drawn behind everything else.
- Lifetime: always on.
- Inputs: none beyond the globals. note_background's `energy`/`reactive`
  become a game global (the spectrum), so the shader declares nothing special.

## 7. Particles: kept state × N

- **Pools (built 2026-11-24, lab only):** the STEP brick, `[Pool(count)]` items of the
  author's own struct, `start(i)` and `step(t, i, dt)` on the GPU, each item
  drawn once through the stack's deform and shade bricks, which read it as
  `item` (`Docs/shaders.md` §20, `Assets/Shaders/Lab/swarm/`). It is this box
  as a user chunk; the host's particles below are not on it yet.
- **Engine births (built 2026-11-24, user's choice over raw atomics):** a
  `rate` (`[Pool(count, rate)]`) and `bool step`. The engine bears exactly `rate` a second into dead
  slots (each takes a ticket from an atomic counter; the first `births` are
  born, through `start(n)`), frees a slot when `step` returns false, and folds
  dead slots out of the draw. No atomics in user code. `Lab/fountain` is the
  lab's FOUNTAIN on it. Bursts (rockets into shells) would be the engine
  bearing N in another pool per death: not built.
- **BUILD brick (built 2026-11-24, lab only):** named buffers
  (`[Buffer(size)] RWStructuredBuffer<T> name;`) and compute passes
  (`[Pass(threads, over: n | groups: g)]`), ordered by `void build()`, which the
  CPU walks each frame (loops and ifs over constants and uniforms); each call is
  its own compute pass, so a pass sees what the ones before it wrote. The
  wrapper places every buffer and each stage binds only what it reads
  (`Slang/StackBuild.cs`, `SlangShaders.Builds.cs`; `Rendering/Passes/GraphRunner.cs`
  runs it). `Docs/shaders.md` §21; `Lab/build_bars`, `Lab/bvh_march`.
- **Library passes (built 2026-11-25):** library modules ship `[Pass]`
  functions and `[Schedule]`s (pass calls, loops, ifs) that `build()` calls by
  module name, lending the brick's buffers as parameters; the library owns no
  buffer. A schedule is spread into the caller's walk, so the graph shows its
  passes one by one. `Slang/StackLibrary.cs` reads a module; `StackBuild.Walk.cs`
  spreads calls; `BuildPasses.cs` / `BuildText.cs` are the shared pass reader
  and comment blanker; `SpirvCoherence.cs` declares `globallycoherent` buffers
  plain to Slang (its coherent type can't be passed to a plain parameter) and
  patches the `Coherent` decoration into the SPIR-V at their bindings after. Shipped: `xanadu.sort` (radix, scan), `xanadu.bvh`
  (morton, leaf, tree = link + refit, `walk` with an `IVisit` visitor).
  `Lab/bvh_march` is built from them. `Docs/shaders.md` §21.
- **TRACE brick (built 2026-11-24, user's choice over `[Tile]` on SHADE):**
  `trace.slang` between BUILD and DEFORM, `[Tile(w, h)] float4 trace(…)`:
  compute, a thread a pixel over the quad's box on the target, tiles that share
  `groupshared` memory (`group.thread`, `.place`, `.index`, `.low`, `.high`).
  At the step's place the renderer ends the pass, dispatches it into two
  target-sized textures (colour rgba16f, depth r32f), starts the pass again and
  draws them over the box (`Rendering/Shaders/traced.slang`), depth into the
  step's slice as a deform's corners go (`traced.depth`, pixels into the
  screen). The stack's own triangles draw after it, depth-tested.
  `Rendering/Passes/ComputeNodes.cs` + `ExitDraws.cs`, `SlangShaders.Traces.cs`; `Docs/shaders.md` §22;
  `Lab/trace_wave`, `Lab/bvh_march`. `[Translucent]` on deform (test, never
  write depth) came with it, for the sparks.
- **Draws (built 2026-11-24, user's choice of syntax):** a BUILD brick may
  declare `[Draws] RWStructuredBuffer<uint> name;`, one uint its passes write:
  the copies of the stack's quad drawn this frame. The renderer draws such a
  step indirectly (`Rendering/Passes/IndirectDraws.cs`: the step's
  vertices and start go up from the CPU, the copies are copied in from the
  buffer on the GPU), and deform and shade read the copy as `copy`. `dt` is a
  global in passes and pool steps. `Lab/particles` is the whole particle lab on
  it (free slots, emitters, death bursts, trail rings, the field); its moves,
  shapes and looks are `xanadu.particles`.

- Today: host only (`Rendering/Shaders/particles.slang`), outside the DSL. A
  compute step moves every particle (motion, life, colour, look), then one
  instanced quad draws per particle, with ramps and curves from a table. There
  are six hard-coded bursts. Level emitters are static items and groups drive
  them (the Emit effect).
- As a user chunk, it's box 2 scaled up:
  - `struct Particle { … }`: a kept struct per particle
  - `spawn(inout Particle p, uint i)`: on emit
  - `step(inout Particle p, float dt)`: compute, once per particle
  - then the same 3 → 4 → 5 chunks per particle
- Reads 0 and 1 (and maybe the emitter's 2). Forward-only, like kept state.
- Open: a fixed `Particle` layout or a user struct, the cap per emitter, and
  whether particles can read other effects' state or the chart.

## 8. Compute: 5 levels × 5 slots

A basic shape (2026-11-20), to be thought through as the last step. Levels
may need to sit **between draws** rather than all before them, so where the
levels go in the frame isn't decided. The read rule is:

```
 level 1   [ ][ ][ ][ ][ ]      reads: globals only
 level 2   [ ][ ][ ][ ][ ]      reads: level 1
 level 3   [ ][ ][ ][ ][ ]      reads: levels 1–2
 level 4   [ ][ ][ ][ ][ ]      reads: levels 1–3
 level 5   [ ][ ][ ][ ][ ]      reads: levels 1–4
```

- 5 levels, 5 slots in each.
- A compute shader in a level can read any slot in an **earlier** level.
- It never reads a **sibling**, meaning another slot in its own level.
- It never reads **downstream**, meaning any later level.

What follows from the rule:
- Levels run in order, with one barrier between levels. Siblings don't depend
  on each other, so a level's slots can run together.
- No cycles, and no slot can read something that's half written.
- Undecided: where the levels sit relative to the draws. A draw might be able
  to read only the levels that came before it.

Open:
- Are the 25 slots for the whole frame, or does each effect get its own?
- What fills a slot: kept state, a pool (particles, indirect draw) or a field
  (grid). Does a slot hold one of these kinds, or raw compute?
- Does a slot read its own last frame (kept state does), and does that count
  as its own, not as a sibling?
- This rule may answer "effect to effect" below: an effect reads another
  effect's compute when that compute sits in an earlier level.

## 9. The render graph (built 2026-11-24)

A compiled stack is also a graph (`Slang/StackGraph.cs`): the passes it runs
this frame and the resources they share (state, pool items, each BUILD buffer,
depth, frame, the screen copy), with who reads and writes what. It shows what
*is*, not what could be:

- **Passes:** the BUILD passes are the ones `build()` really calls with the
  values as they are now, in the order first called, each with its calls
  (×4 for a loop) and the threads they run. A pass a value switches off isn't
  there; a schedule that can't run says why.
- **Reads and writes:** compute binds every buffer read-write, so each compute
  stage's SPIR-V is scanned (`SpirvScan.Access`) for the loads, stores and
  atomics that really reach each buffer. Graphics stages can only read.
- **Still as bound:** kept state and pool items show every stage that binds
  them, used or not.

- **The renderer walks it.** Each frame, every stack drawn runs its compute
  passes in the graph's order before any pass draws (UPDATE, STEP, then BUILD
  as `build()` calls the passes); TRACE and the triangles run at its draw.
- **The shader tool draws it.** GRAPH (or `--graph`) swaps the code for the
  graph on a read-only board: passes down the left, coloured like their bricks,
  resources down the right, an arrow for each read and write (one line with two
  heads for both), lines hopping where they cross. It follows the values as
  you tune them.
- **Next:** frame history as one more resource.

## Not covered yet

- **Effect to effect: TBD.** No path lets one effect's state, pool, field or
  pixels feed another effect. Buffered groups only pass pixels down to their
  own buffer shader.
- **Kept state in levels.** Every level-side box waits on the paused-`dt`
  runtime, and so do particles as chunks.
