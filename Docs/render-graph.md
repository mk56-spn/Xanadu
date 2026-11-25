# Render graph: status and next steps

Handoff for the shader-land restructure (2026-11-25). The proposal is the artifact
"Shader Land Structure": https://claude.ai/artifact/3B7y8SVr71759LEirbDNQR

## The goal

One render graph per effect instead of a fixed stack of bricks, so a scene, a mesh
in the same room, cheaper backdrops or new lighting are library code plus a line of
`graph()`, never a pipeline change. All 3D stays inside the effect; the game only
sees an item drawing pixels.

Four layers:

1. **Resources:** buffers, images and values, declared in code.
2. **Nodes:** compute, draw and copy.
3. **`graph()`:** the CPU-walked schedule.
4. **Library contracts:** `View`, `Surfaces`, `Lights`.

Phases:

1. Core
2. Contracts
3. Port the producers
4. Light views and AO
5. Effect to effect

## Done and committed

- `c4f9741b`: `xanadu.scene` (Assets/Shaders/Library/xanadu/scene.slang).
  - Solids are recorded in a BUILD pass into `scene.Part` / `scene.Object` buffers.
  - `scene.shot` marches them with tile lists, per-ray box gathers and box-distance skipping for shadows and AO.
  - `beat` now reaches BUILD passes.
- `2c40a361`: phase 1a, the graph core.
  - `void graph()` is the schedule; `build()` is still accepted as an alias.
  - `[Image(format)] RWTexture2D<T> name;` declares per-material images, the target's size. Formats: rgba16f, rgba8, rgba32f, rg16f, r16f, r32f, r32ui.
  - `[Pixels(w, h)] void name(ItemInput i, Canvas canvas)` is compute over the item's box. It writes images at `group.pixel`, a new field on `Group` in stage.slang.
  - `present(colour[, depth])` draws images on the item through the TRACE draw path (`TracePass.DrawPresented`). With no SHADE brick it is all the item draws (`CompiledBuild.Alone`).
  - One clock preamble (`SlangShaders.Clocks` / `TimelineClock` in SlangShaders.Pixels.cs) sets time, beat and dt in every compute wrapper. Pool steps and `update` now get `beat` too (`UpdateUniforms.Beat`).
  - Host side:
    - `Rendering/Passes/GraphPasses.cs` was split out of StackPasses.
    - `Rendering/EffectCompute.cs` was split out of CanvasPipelines.
    - Pass pipelines declare their storage textures, and pixel passes get 2-D tiles.
  - Reflection skips read-write images when counting samplers (SlangShaders.cs).
  - `Lab/scene_room` runs on graph + pixels + present, with no TRACE brick (about 2.1–2.4 ms at preview size; the old trace took 1.81).

## Done: phase 1b, draw nodes

What's written:

- **Declaring a draw:** `[Draw(across, down, shade: fn)] ItemVertex name(ItemVertex v)` plus `float4 fn(ItemInput i[, Canvas canvas])`.
  - Read in `BuildPasses.ReadDraws`.
  - Compiled in `Slang/SlangShaders.Draws.cs` (`CompiledDraw`, `WrapDrawVertex`, `WrapDrawFragment`).
  - The grid comes from `SV_VertexID`. The quad is vertex uniform 0 (`DrawQuad`).
  - Its depth is squashed as `z = 0.5 + 0.5 d / (|d| + 1024)`. The fragment writes colour to target 0 and linear depth to target 1.
- **Calling a draw:** `name(colour[, depth]);` in `graph()` (`BuildDraw`, a `Run` with A and B as images).
- **`clear(image[, value])`:** `BuildClear`, `Run.Clear` = -2, with the value's float bits in B.
- **Host:**
  - `CanvasEffect` moved to `Rendering/CanvasEffect.cs`. It holds `Draws` (shaders) and caches pipelines per draw (`DrawPipeline`).
  - `BuildBuffers.Z` is a per-graph depth texture, cleared at the frame's first draw.
  - `GraphPasses.RunDraw` runs the draws.
  - LineCap: CanvasPipelines.cs was lowered to 403.
- **The board:** `StackGraph` shows draws and the images they write.
- **The demo:** `Lab/scene_room/build.slang` draws a mesh `Torus` (`ringDrawn` / `ringLook`) into `shown`/`shownDepth` before `shoot`. `shoot` keeps only SDF hits nearer than the depth already drawn.

Status: runs. The torus sits in the marched room, hiding and hidden correctly.
Two fixes got it going:

- A draw binds its vertex stage's samplers too (white, like the fragment's).
- `clear` sets `ColorTargetInfo.ClearColor` as an `FColor`. MoonWorks' `Color` is bytes, so it clamped 1e30 to 1.

## Next, in order

1. Phase 2, the contracts:
   - `View`, `Surfaces` (depth, normal, base, shine, glow) and `Lights` in library modules.
   - `light.deferred` as a pixel pass, so meshes get the room's lamp, shadows and AO.
   - `scene.shoot` writes `Surfaces` and stops at the existing depth (pass a limit into the march, so hidden pixels cost nothing).
2. Phase 3: draw the room as a closed-form box (`room.box` draw) so the march only covers bounded objects. The target is beating 1.81 ms with the vase/torus in.
3. Phase 4: shadow depth per light from the same geometry schedule; screen-space AO; SDF soft shadows and AO as opt-in nodes.

## Known failures (not from this work)

- `new_shader_8` has a STEP brick outside `Lab/` (two Slang test failures).
- `APlainFloat4StaysAnError` fails with or without the scene module.
