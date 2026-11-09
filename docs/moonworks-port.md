# Port: Godot → MoonWorks

Hand-off and tracking doc for moving Xanadu off Godot onto
[MoonWorks](https://github.com/MoonsideGames/MoonWorks), a small C# framework
over SDL3 (SDL_GPU on Vulkan / D3D12 / Metal).

Written to be picked up cold by any agent. **Read sections 0–3 before touching
code.** Work one step at a time; tick the box when a step lands and add a line
under it saying what changed from the plan.

---

## 0. Current state

Update this block at the end of every session, including partial progress.

- **Updated:** 2026-11-09 (second session)
- **Done:** steps 0 and 1, and most of step 2: `dotnet run --project Host -- --demo`
  plays a level made in memory (notes, guides, misses, pops, blocks, HUD, song
  clock and audio) through the host's own renderer, in one draw call, with no
  Godot engine call left on that path.
- **Next:** the rest of step 2: try keyboard and mouse by hand (wired, not yet
  played), then the profiles and song-select screens (they need text, so in
  practice step 3 starts there). See the step 2 notes for what the host still
  skips.
- **Committed:** everything up to here, including the step 2 work. Nothing pending.
- **After cloning:** run `External/fetch-moonlibs.sh` once (the host build fails
  with that message otherwise).
- **Notes for whoever picks this up:** see the step 1 and step 2 notes (Slang
  usage, the seams, the engine-call trap, what the host skips).

---

## 1. Why

- Godot forces one `project.godot` per C# assembly with Godot scripts, so the repo
  has **five nested Godot projects** (root, `Tools/Animator`, `Tools/Lab`,
  `UI/Showcase`, `Tests/VisualTests`). Each one has its own `.godot` cache, its own
  `res://` root, symlinked assets, and a copy of the `realpath` csproj hack. The
  goal is plain .NET solutions: every tool is a normal `dotnet run` exe.
- Godot's C# future (godot-dotnet, LibGodot .NET hosting) is on hold or
  unmerged, with no date. We stopped waiting.
- Features Godot's 2D will not give us: 2D depth buffers, arbitrary blend modes,
  compute (GPU particles), instance data without per-shader limits, and control
  over present mode and frame latency.

---

## 2. Decisions (locked)

Agreed with the user. Don't revisit them without asking.

| # | Decision |
|---|---|
| P1 | **MoonWorks**, not MonoGame / FNA / Stride. MonoGame's Vulkan backend (3.8.6) exposes no compute or storage buffers (XNA API only) and compiles shaders offline through MGCB. Stride is 3D-first and uses its own shader language. MoonWorks gives SDL_GPU directly, and it is ~13k lines of C# we can patch ourselves. |
| P2 | **Hard switch, not two builds.** Pre-alpha: the only players are the user and close friends, and nothing ships. Don't keep the Godot build working alongside. Its code stays in the tree **as reference only** (nothing new goes into it) until the MoonWorks build matches it, and then it is deleted. A period where the game doesn't run is accepted. |
| P3 | **Break freely.** No migration of formats, settings or scenes unless it's trivial (same rule as the rest of the repo). |
| P4 | **The test suite stays green throughout.** It already runs without the engine (~2300 tests). It is the safety net while the game can't run. See §6 for how to run it. |
| P5 | **MoonWorks is vendored from source**, pinned to a commit (it is not on NuGet; that is upstream's choice). Patch it locally when needed; zlib allows that. **Never send AI-written PRs upstream**: their `AGENTS.md` forbids it. Using and patching it locally is fine. |
| P6 | **Native libs come from Moonside's bundle**, not the OS: `https://moonside.games/files/moonlibs.tar.gz` (SDL3, FAudio, IRO, Wellspring, dav1dfile). The system only has SDL3 and FAudio; IRO (images) and Wellspring (fonts) are **not** installed. *Changed in step 1:* the bundle is ~60 MB, so it is fetched (with a SHA-256 check) into gitignored `External/moonlibs/`, not committed. See `External/README.md`. |
| P7 | **Shaders are written in [Slang](https://shader-slang.org)** (HLSL syntax plus real modules: `import`, `public`/internal, interfaces, generics), compiled to SPIR-V **at runtime** through the `Slangc.NET` NuGet package (MIT; ships the native compiler for linux/win/osx, x64 and arm64; API: `SlangCompiler.Compile(slangc args)`). Runtime compile is needed for player shaders. *Changed 2026-11-09 from "GLSL via libshaderc":* the user plans to ship shader libraries to players, and GLSL's text-pasting `#include` (guards, leaking helpers, name clashes) is what made the legacy library painful (`~/Documents/Xanadus/Shaders`). If `Slangc.NET` is abandoned, call `libslang` directly or run `slangc` at build time. The spike now uses Slang end to end (the GLSL path is deleted). |
| P8 | **Godot math structs** (`Vector2`, `Rect2`, `Color`, …) stay as they are at first: `GodotSharp.dll` loads fine without the engine as long as only plain structs are touched (the tests prove it). At the end, copy them into Core under the same names (MIT licence), so ~460 files don't change. |
| P9 | **Order of work: get the playfield playable first**, then text and UI, then the look (shaders, groups, buffers), then particles, then the tools. |
| P10 | **No mesh shaders** (SDL_GPU doesn't expose them). GPU-made geometry = compute into a storage buffer + vertex pulling + `DrawPrimitivesIndirect`. |
| P11 | **Shaders are exposed to players as slots, not whole shaders** (§2a). Each item or group gets a **vertex** slot, a **fragment** slot (a chain of effects), a **blend** dropdown and, later, a **state** slot. Each slot has a built-in default, a library of ready-made choices, or player code that fulfils a **versioned contract** checked by the compiler. |

### 2a. Shader slot model (P11)

| Slot | Default | Player can |
|---|---|---|
| Vertex | the host's standard quad | pick a library modifier (wobble, perspective tilt, squash) or write `ItemVertex deform(ItemVertex v, ItemInput i)` |
| Fragment | plain colour or texture | pick library effects or write `float4 shade(ItemInput i)`; several in a row form a **chain** |
| Blend | mix | dropdown: hardware modes (mix, add, subtract, multiply, screen, min, max, premultiplied) are free; "exotic" modes (overlay, soft and hard light, dodge, burn, difference, exclusion, HSL) go through a group buffer and the composite shader |
| State (later) | 2D defaults | depth test and write once 2D depth exists |

- **The contract** lives in a shared Slang module, `xanadu.stage`:
  `ItemVaryings` (what every vertex stage hands every fragment stage: position,
  pixel UV, half size, colour, item index, …), `ItemInput` (what a fragment
  sees) and the `IFragment` / `IVertexModifier` interfaces. Slang's interface
  check reports a broken player shader on the player's line.
- **Version the contract from day one** (`xanadu.stage` v1). Only ever add
  fields, and show in the editor which version a shader targets. Pre-alpha may
  still break it, but the habit starts now.
- **Vertex and fragment shaders compile separately** and only meet when the
  pipeline is built, so any vertex choice pairs with any fragment choice. A
  vertex modifier is wrapped into the host's own vertex shader; players never
  write the quad and transform boilerplate.
- **Almost no player writes a vertex stage** (the user's estimate: 99% never
  will). Vertex needs are covered by **10–20 in-house variants** shipped as a
  pick list (standard quad, wobble, perspective tilt, squash, wave, …). In
  practice the surface players write against is `shade(ItemInput i)`, and the
  pipeline count is bounded by (in-house vertex variants) × (fragment chains) ×
  (blend modes) actually used. Player vertex modifiers are the rare case; keep
  their API small.
- **A fragment chain is one shader:** the host generates a wrapper that calls
  each effect in order, passing the colour along. That's one pass and one
  pipeline, not a pass per effect.
- **Blend mode is pipeline state, not shader code.** One compiled shader is
  reused in a pipeline per blend state; there is no per-mode shader copy (this
  replaces `Systems/Shaders/ShaderBlendVariants.cs`). Exotic modes are a
  runtime value in **one** composite shader (a switch on the mode; the branch
  costs almost nothing because every pixel takes the same path).
- **Pipelines are built on first use and cached** by (vertex, fragment chain,
  blend, state), and compiled at **level load** with a disk cache, never
  mid-song. Everything draws into offscreen targets of one format, and depth
  and MSAA are per pass, so in practice the blend mode is the only state that
  varies per item.
- **Binding sets keep the stages independent:** vertex resources in sets 0 and
  1, fragment resources in sets 2 and 3 (§3).
- **Shipped libraries are Slang modules** (`xanadu.sdf`, `xanadu.colour`,
  `xanadu.easing`, `xanadu.blend`, …), plus an umbrella `xanadu` module that
  re-exports the rest (`__exported import`). Port the useful maths from the
  legacy library (`~/Documents/Xanadus/Shaders`: 2D SDFs, SDF rendering, easings,
  colours, patterns, blend modes, gradients, perspective). Its `SIZE`,
  `PERSPECTIVE` and `OVERRIDE_DEFAULT_VERTEX` macros become `ItemInput` fields
  and vertex modifiers. Bug to leave behind: its `getSecondaryColour()` reads
  `index`, not `index_2`.

---

## 3. Facts and gotchas (verified)

Learnt the hard way; don't re-discover them.

- **The crash we already fixed.** Calling any Godot native API (e.g.
  `RenderingServer`) outside the engine jumps to a null pointer and kills the
  test host silently. Draw-system tests pass `Tests/Support/Fakes/FakeShaderCanvasItems`.
  Always check test output for "Test Run Aborted".
- **SDL_GPU SPIR-V binding layout** (resources must be declared exactly like this):
  - vertex: samplers → storage textures → storage buffers in `set = 0`; uniforms in `set = 1`.
  - fragment: the same order in `set = 2`; uniforms in `set = 3`.
  - compute: sampled textures → read-only storage textures → read-only storage
    buffers in `set = 0`; read-write storage textures → read-write storage
    buffers in `set = 1`; uniforms in `set = 2`.
- **`ColorTargetBlendState.Additive` multiplies the source colour by source
  alpha.** Output alpha 0 and nothing draws (this was the spike's one bug).
- **Swapchain textures can't be read back.** Render into an offscreen texture,
  then blit it to the swapchain. Screenshots download the offscreen texture
  (`Screenshot.cs` in the spike, with a dependency-free PNG writer).
- **MSAA:** 2×, 4× and 8× are supported and resolve into a normal texture at the
  end of the pass (`StoreOp.Resolve`). MSAA textures **can't be sampled** until
  resolved, and **depth can't be resolved**. Combining MSAA with reading depth
  later needs a design (e.g. a separate depth pass without MSAA).
- **Blend modes:** fixed-function covers mix, premultiplied, add, subtract,
  multiply, screen, min and max at no cost. Overlay, soft light, dodge, burn,
  difference and HSL modes need **copy, then blend in the shader**, or **render
  the group to a texture and composite**. SDL_GPU has no framebuffer fetch.
- **Depth in 2D:** supported. A depth texture can be both a depth target and
  sampled later. Writing `gl_FragDepth` gives per-pixel sprite depth (it turns
  off early-z, which costs little in 2D). Semi-transparent pixels still need
  sorting.
- **Instance uniforms:** put per-item `vec4`s in a per-frame storage buffer;
  vertices carry an item index. Batching is unaffected.
- **MoonWorks `Game`** has abstract `Step()`, `Update(TimeSpan)` and
  `Draw(double alpha)`. It starts its audio (FAudio) and video threads in the
  constructor.
- **MoonWorks targets net9.0.** Referencing it from net10 projects works (the
  spike does this).

---

## 4. Inventory: what touches Godot today

Measured 2026-11-09 (code lines, without comments or blanks). This is the
surface the port replaces; everything else is ECS and plain C#.

| Area | Lines | Where |
|---|---|---|
| Canvas, text, glow, blur | ~1,200 | `UI/Systems/Canvas/` (`GodotRectCanvas`, `GodotGlyphBlitter`, `GodotTextGlow`, `GodotShapeGlow`, `GodotBlur`, `CanvasItemHandle`, `VisualRidRelease`; `GlyphAtlas` is already portable) |
| UI rendering systems | ~670 | `UI/Systems/Rendering/` (`RectDrawSystem`, `LayerParentSystem`, `LayerOpacitySystem`, `DrawableInitialiseSystem`) |
| Shader routing and materials | ~1,130 | `Systems/Shaders/` (`ShaderRoutingCanvas`, `GodotShaderCanvasItems`, `LevelShaderMaterials`, `CommonShaderMaterials`, `ShaderBlendVariants`, `ShaderUniforms`, `GlowRingShader`) |
| Layer passes and surfaces | ~980 | files using `IPassSurfaces` / `SubViewport` |
| Particles | ~370 | `Systems/Particles/` (`GodotParticleEmitter`, `ParticleKit`, `ParticleInstance`), behind `IParticleEmitter` |
| Editor overlay (Godot nodes) | ~700 | `Editor/EditorOverlay*.cs` (`FileDialog`, the invisible `LineEdit` for text input, `ItemList`, `Label`) |
| SVG attachments | ~550 | `Animation/SvgAttachmentVisuals*.cs` (one `Image.LoadSvgFromString` call) |
| Input | ~90 | `UI/Systems/Input/Godot*.cs`, `KeyPressQueue.cs` |
| App shell | ~220 | `Main.cs` (+ `Main.tscn`) |
| Misc | small | `ProjectSettings.GlobalizePath` (25 calls), `DisplayServer`, `OS.GetCmdlineUserArgs`, `TranslationServer`, `UI/Ecs/Ui/GodotVisualRidFactory.cs`, `UI/Ecs/Ui/UiComponents.cs` |

Direct `RenderingServer` callers outside the Lab and Diagnostics:
`Systems/Characters/CharacterRigGhosts.cs`, `Systems/Particles/ParticleInstance.cs`,
`Systems/Particles/ParticleKit.cs`, `Systems/Shaders/ShaderRoutingCanvas.cs`,
`Systems/Shaders/ShaderUniforms.cs`, `UI/Ecs/Ui/GodotVisualRidFactory.cs`,
`UI/Ecs/Ui/UiComponents.cs`, `UI/Systems/Canvas/*`, `UI/Systems/Rendering/*`,
`UI/Systems/Ui/UiSliderSystem.cs`, `Animation/SvgAttachmentVisuals*.cs`,
`Tools/Animator/AnimatorTool.cs`.

Most-used calls: `CanvasItemClear`, `FreeRid`, `CanvasItemSetParent`,
`CanvasItemCreate`, `CanvasItemAddTriangleArray`, `CanvasItemSetVisible`,
`CanvasItemSetTransform`, `CanvasItemSetZIndex`, `CanvasItemAddTextureRect`,
`CanvasItemSetModulate`, `CanvasItemSetDrawIndex`, `CanvasItemSetCustomRect`,
`CanvasItemSetMaterial`, `CanvasItemSetCopyToBackbuffer`, `CanvasItemAddRect`,
`CanvasItemAddPolygon`, `CanvasItemSetCanvasGroupMode`,
`CanvasItemSetInstanceShaderParameter`, `CanvasItemSetClip`, `Particles*`.
The new renderer must cover this set: a retained item tree plus these draw
commands.

**Dropped, not ported:** `Tools/Lab/*` and `Diagnostics/*` are Godot render
experiments (`RenderingDevice`, compute, viewports). Rebuild individual ones
later if wanted.

Estimated new code: **8,000–12,000 lines**. The breakdown is in each step below.

---

## 5. Steps

Each step should end with the build and tests green. Steps 2 onward also end
with a screenshot from the new host (only one per step; the user's rule for lab
and visual work is build plus one screenshot, no timing sweeps unless asked).

### Step 0: Spike ✅

- [x] `Tools/Lab/MoonWorksParticles/`: 200k GPU particles (one compute step and
  one instanced draw reading the same storage buffer), an offscreen target
  blitted to the window, runtime GLSL→SPIR-V through libshaderc, and PNG
  readback. About 210 lines of C# and 75 of GLSL. Run it with `dotnet run`
  (`-- --screenshot x.png` saves frame 150 and quits). It now uses
  `External/MoonWorks` and, since step 2's Slang check, Slang shaders through
  `Slangc.NET` (the GLSL and libshaderc path is deleted).

### Step 1: Vendor MoonWorks and stand up the host ✅

- [x] MoonWorks vendored as a **committed, trimmed copy** in `External/MoonWorks/`
  (commit `bd841cb`, C# sources and licences only; `External/README.md` lists
  local patches, none yet). Native libs: `External/fetch-moonlibs.sh` → gitignored
  `External/moonlibs/lib64/`, copied next to the host exe.
  - Verified the bundle's SDL3 (3.2.17, June 2025) exports every SDL function
    MoonWorks calls, and that the host loads the **bundled** `libSDL3` and
    `libFAudio`, not the system ones (checked with `LD_DEBUG=libs`).
- [x] `Host/Xanadu.Host.csproj` (`Microsoft.NET.Sdk`, exe) references
  MoonWorks, `Xanadu`, `Core` and `UI`. `HostGame` clears an offscreen
  1920×1080 target to `Palette.Background` and blits it to the window. Added to
  `Xanadu.sln`; `Xanadu.csproj` excludes `External/**` and `Host/**` from its
  glob, and both folders have a `.gdignore`.
- [x] The game assemblies are consumed **as they are** (still `Godot.NET.Sdk`),
  the same way the tests reference them. That works from a plain exe as long as
  nothing calls into the engine. Switching their SDK is left to step 11.
- [x] `--shot=<png>` (plus `--shot-frame=<n>`, default 60) and
  `--resolution=WxH`. The step 1 shot was pixel-exact `#0b1020`.
- The ECS world isn't created yet: that belongs in step 2 with the renderer,
  since every draw system needs a canvas.
- The spike now references `External/MoonWorks` too; its private clone is gone.
  It still uses the system SDL3 (it's only a lab spike).

### Step 2: Renderer core and a playable playfield (2,500–3,500 lines)

Findings from mapping the surface (2026-11-09):

- **The composition root is `Ecs/XanaduSystemRoot.cs`.** It does `new GodotRectCanvas()`,
  `new GodotVisualRidFactory()` (twice), `new GodotKeyboardState()` and
  `new GodotPassSurfaces(passHost)` (a Godot `Node`). Give it a platform object
  (canvas, visual factory, keyboard, pointer, wheel, pass-surface factory) so the
  host can pass MoonWorks implementations. Defaults go elsewhere too:
  `SceneSystem` (line ~75), `UiBuilder.DefaultVisuals`, `UiInteractionSystemGroup`.
- **`IRectCanvas`** (`UI/Systems/Canvas/IRectCanvas.cs`, 318 lines, used in 63
  files) is the drawing contract: shapes, text variants, glows, layers, buffers
  and shader selection, all keyed by a canvas `Rid`.
- **`IVisualRidFactory`** has one method, `Rid CreateCanvasItem()`. The new
  renderer has to mint `Godot.Rid` values without the engine. `Rid` is a plain
  struct around a `ulong`; tests build them by reflection (`Tests/Support/TestRid.cs`).
  Do the same in the host for now (P8 replaces `Rid` at the end).
- **`IRectCanvas.DrawTexture` takes a Godot `Texture2D`**, which is an engine
  object and can't exist without the engine. It needs a texture-handle
  abstraction before textures work in the host.

- [x] Slang proven (2026-11-09, run with the user's go-ahead after auto mode
  blocked it twice). A compute shader with the SDL_GPU layout compiled to valid
  SPIR-V. A broken shader threw `Exception` with a rustc-style message,
  `error[E30015]: undefined identifier --> bad.slang:3:15` plus a caret, which
  is good enough to show players as is. The spike's three GLSL shaders are now
  **one Slang module** (`Shaders/particles.slang`, entry points `simulate`,
  `quad` and `glowDot`) and render the same as before.
  - Usage: `SlangCompiler.Compile([path, "-target", "spirv", "-entry", <name>, "-stage", compute|vertex|fragment])`.
    The SPIR-V entry point comes out as **`main`**, so pass `"main"` to
    `Shader.Create` / `ComputePipeline.Create`.
  - Bindings: `[[vk::binding(index, set)]]` on each resource, with the sets
    from §3. Varyings match by declaration order (`TEXCOORD0`, `COLOR0`, …).
  - Don't name an entry point after an intrinsic (`dot`): it shadows it.
  - Auto mode treats a new NuGet package's native code as "code from external".
    Expect to need the user's explicit OK the first time a new native package
    runs.
- [x] A retained canvas-item tree implementing what `IRectCanvas` and the visual
  RID factory need: create, free, parent, clear, transform, z-order, draw index,
  visibility, clip, modulate and self modulate (`Host/Rendering/CanvasTree.cs`).
  It follows Godot's order: an item draws before its children, siblings sort by
  draw index and then by when they were parented, and z is relative to the
  parent and sorts the whole tree.
- [x] The draw commands from §4: rect, line, polyline (mitred), multiline,
  polygon (ear clipping), triangle array, circle, textured rect. Nine-patch
  isn't needed: nothing in the game calls it.
- [~] Batching: one dynamic vertex buffer per frame and one draw per run of the
  same texture and clip rect. There is only one pipeline so far, so the
  pipeline cache waits for step 5.
- [~] Keyboard and pointer input through the existing input interfaces
  (`Host/Input/`). Wired, but not yet tried by hand.
- [x] Play mode draws and plays (no text, shaders or particles yet).

How it is wired (2026-11-09):

- **`CanvasServer`** (`UI/Systems/Canvas/CanvasServer.cs`) is the seam: a
  static `Current` (`ICanvasServer`, Godot's by default) with the subset of
  `RenderingServer` the game uses. Every shared caller listed in §4 goes
  through it now (UI rendering systems, `CanvasItemHandle`, `VisualRidRelease`,
  `VisualRid`, `CharacterRigGhosts`, shader routing, SVG visuals). Godot-only
  calls are left where they were (materials, back-buffer copies, instance
  uniforms, particles, texture filters); they belong to steps 5–8.
- **`ServerRectCanvas`** holds `GodotRectCanvas`'s shapes on top of the
  server. `GodotRectCanvas` derives from it and adds fonts and glows. The host
  uses `ServerRectCanvas` as it is, so it draws no text yet.
- **`HostPlatform`** (`UI/Systems/HostPlatform.cs`) holds what systems fall back
  on: keyboard, visual factory, FPS source, a canvas factory, vsync, and
  `IsGodot`. The host calls `HostPlatform.Use(...)` before building
  `XanaduSystemRoot`. This replaced the composition-root "platform object" plan:
  defaults were spread over several groups, so one static holder changed fewer
  signatures. The debug toggles became `KeyOverlayToggleSource` and
  `KeyOverlayPageSource` and read `HostPlatform.Keyboard`.
- **`GamePaths`** (`Core/Storage/GamePaths.cs`) is now the only place `res://`
  and `user://` become real paths (no IO spread). The host points `user://` at
  Godot's folder (`~/.local/share/godot/app_userdata/Xanadu`), so both builds
  share profiles, songs and levels.
- Also swapped: `GodotTrackClock` → `StopwatchTrackClock`, and
  `GodotCharacterRigFiles` → `DiskCharacterRigFiles` (engine-free now).
- **What the host skips while `!HostPlatform.IsGodot`:** shader materials
  (`ServerShaderCanvasItems` keeps runs as plain child items), particles
  (`SilentParticleEmitter`), rig art (`CharacterRigDrawSystem` isn't added, so
  the plain player box draws), canvas groups (children draw straight through)
  and text. Each one returns in its own step.
- **`Host/Diagnostics/GodotEngineTrap.cs`** fills GodotSharp's native function
  table with a stub that prints the managed stack and exits with code 70. A
  leftover engine call now names its caller instead of segfaulting. **Use it to
  find the next engine call on any new screen.** It relies on GodotSharp
  internals (`NativeFuncs._unmanagedCallbacks`, 237 entries in 4.8.0-dev.7) and
  says so when they move.
- **Rid minting:** `CanvasTree` reinterprets a `ulong` as `Godot.Rid` with
  `Unsafe.As`. Textures share the id space (`CanvasRenderer.Textures`).
- **Host flags:** `--demo` (a level made in memory, `Host/DemoLevel.cs`),
  `--play[=<name or file>]`, `--editor`, plus `--shot`/`--shot-frame`. Only one
  saved level loads today (`unnamed-1.xmf` is in an older format the loader
  skips), and it's empty, hence `--demo`.

### Step 3: Text (800–1,200 lines)

- [ ] Choose a rasteriser: SDL3_ttf (in moonlibs? check) or Wellspring
  (MoonWorks' own, MSDF). Feed the existing `GlyphAtlas`.
- [ ] Outline, bold and glow (replace `GodotTextGlow`).

### Step 4: UI and scenes

- [ ] UI systems render through the new canvas: menus, HUD, settings.
- [ ] Scene switching without Godot nodes (`Main` replacement).
- [ ] Window, display mode and settings adapters (500–800 lines with step 9's
  input work).

### Step 5: Shaders: Slang, slots, blend modes, instance data (800–1,200 lines + libraries)

- [ ] Shader compile service: Slang source → SPIR-V via `Slangc.NET`, with
  binding counts from Slang reflection filled into `ShaderCreateInfo` /
  `ComputePipelineCreateInfo` automatically, plus a disk cache keyed by source
  hash.
- [ ] `xanadu.stage` v1 contract and the host's standard vertex shader (§2a).
- [ ] Slot model: vertex modifier, fragment chain wrapper generation, blend
  dropdown, and a pipeline cache keyed by (vertex, chain, blend, state), built
  at level load.
- [ ] `IShaderCanvasItems` for the new renderer: a material per run (now a slot
  set), copy-screen and copy-rectangle.
- [ ] Per-item data (instance uniforms) in a storage buffer per frame (§3).
- [ ] Rewrite the game shaders as slot effects: `Assets/Shaders/note_background`,
  `flare`, `wave_background`, plus the UI materials (`UiTextGradient`,
  `ColorPickerMaterials`, `GodotShapeGlow`).
- [ ] Level shader slots move to the new model. `ShaderSourceRules` validates
  through Slang compile errors and the contract check. Godot shader source in
  levels is dropped (P3).
- [ ] First shipped library modules, ported from the legacy library (§2a).

### Step 6: Groups, buffers and layers (1,000–1,500 lines)

- [ ] Buffered groups (CanvasGroup equivalent): render to a texture, then
  composite with tint, alpha, shader and blend.
- [ ] Screen-read regions (back-buffer copy).
- [ ] Self-contained layers (`IPassSurfaces`) as offscreen targets.
- [ ] Re-check the decisions in `docs/render-architecture.md` (D7–D17) against
  the new implementation.

### Step 7: Particles (600–1,000 lines)

- [ ] A compute-based `IParticleEmitter` matching today's features: bursts,
  one-shots, curves and gradients. Start from the spike.

### Step 8: SVG and character rigs (200–400 lines)

- [ ] Replace `Image.LoadSvgFromString` with a library (Svg.Skia or resvg) plus
  a texture upload.
- [ ] `CharacterRigGhosts` and SVG attachment visuals draw through the new canvas.

### Step 9: Editor (500–900 lines)

- [ ] Replace `EditorOverlay*` Godot nodes: SDL3 native file dialogs
  (`SDL_ShowOpenFileDialog`), and SDL text-input events plus clipboard instead
  of the invisible `LineEdit`. Remaining debt (`ItemList`, `Label`) moves to ECS
  UI.
- [ ] The editor runs in the host.

### Step 10: Tools as plain exes

- [ ] Animator: `Tools/Animator` becomes a `Microsoft.NET.Sdk` exe on the
  host's renderer. Delete its `project.godot`, `.godot/` and the `realpath`
  hack. Its assets resolve through the one IO owner (memory: no IO spread).
- [ ] UI Showcase: the same.
- [ ] Visual tests: port the harness in `Tests/VisualTests/Harness` to the host's
  screenshot path.

### Step 11: Delete Godot

- [ ] Remove every `project.godot`, `.godot/`, `*.tscn`, `*.uid`, `*.import`,
  `Godot.NET.Sdk` reference and the `run-*.sh` Godot launchers.
- [ ] Copy Godot's math structs into Core (P8) and drop `GodotSharp`.
- [ ] Rewrite `agents.md` (its "Verification" section is all Godot) and update
  memory notes that mention Godot.

### Later (not part of parity)

- [ ] 2D depth buffer: per-item depth, per-pixel sprite depth maps, sampled depth
  (fog, outlines, soft particles).
- [ ] MSAA (decide how it interacts with depth sampling, §3).
- [ ] Photoshop-style blend modes via group composite.
- [ ] 2D lighting: point and directional lights, normal maps, 1D shadow maps with
  PCF, light masks (1,000–2,000 lines). Later: SDF or radiance-cascade GI.

---

## 6. Verification

- Build: `dotnet build` on each csproj touched.
- Tests: `dotnet test Tests/Xanadu.Tests.csproj`. Since the 2026-11-09 fix the
  full run completes (2298 tests). Still check for "Test Run Aborted", and use
  `--blame-crash` to find the culprit if it appears. Fourteen failures
  predate the port: friction, LayerPass, SelfModulate, XmfBinary, key-binding,
  `EveryButtonActionHasAKey`, two `EditorPlayfieldDrawSystemTests`, and
  `MapOffsetSettingsTests.ZeroLeavesNoEntry`. Don't count them as regressions,
  and don't fix them as part of this port unless asked.
- Visual: one screenshot per step from the host's `--shot`, saved under
  `screenshots/` (gitignored; never commit images outside `Assets/`).
- Git: work on `main`, no branches or PRs (pre-alpha, solo project). Commit only
  when the user asks.
