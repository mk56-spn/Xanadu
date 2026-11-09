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

- **Updated:** 2026-11-09 (third session)
- **Done:** steps 0, 1, 3, 4 and 6, most of step 2, and most of step 5. The
  host draws buffered groups, self-contained layer passes, background-reader
  passes and screen-reading shaders (frosted glass) through a multi-pass
  renderer.
- **Next:** step 7 (particles). Still open from earlier steps:
  - step 2: play a level by hand with the keyboard;
  - step 5: per-item data in a storage buffer, the vertex slot and chains,
    and shape glow.
- **Committed:** everything.
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

### Step 3: Text ✅

- [x] Rasteriser: **FreeType + HarfBuzz** (NuGet `FreeTypeSharp` 3.1.0,
  `HarfBuzzSharp` + `HarfBuzzSharp.NativeAssets.Linux` 14.4.0; all MIT and
  ship their own native libs). Chosen with the user over SDL3_ttf (not in
  moonlibs), Wellspring (MSDF atlases baked offline with a fixed character
  set), StbTrueTypeSharp (no variable fonts, no shaping) and SkiaSharp (heavy).
- [x] Faces: regular text is **Source Sans 3 at weight 400** and bold the same
  file at 700 (the user's choice; Godot drew regular text in its built-in Open
  Sans, which isn't in the repo, so the UI looks slightly different). Mono is
  JetBrains Mono.
- [x] Outline, bold and glow (replaces `GodotTextGlow`).
- [ ] Feeding `GlyphAtlas`: deferred. Its only users are `ParticleKit` and
  `NoteAtlas`, which hand out a Godot `Texture2D`, so it moves with the texture
  abstraction in steps 5 and 7. `HostText` can already render any glyph into
  the atlas.

How it is wired (2026-11-09):

- **`Host/Text/`**, host only; the game assemblies gained no packages.
  - `HostText` is the one owner of the font files (read through `GamePaths`),
    the faces, and a glyph cache keyed by (face, size, glyph, look).
  - `FontFace`: HarfBuzz shapes the text and FreeType renders it with light
    hinting at whole-pixel pen positions, matching Godot's defaults. FreeTypeSharp
    and HarfBuzzSharp don't bind the variable-font calls, so `FreeTypeVariations`
    P/Invokes `FT_Get_MM_Var`, `FT_Set_Var_Design_Coordinates` and
    `hb_font_set_variations`. Set the weight on both libraries, or advances and
    outlines disagree.
  - `TextAtlas`: 1024² RGBA pages, white with coverage in alpha, so glyphs
    draw through the canvas's one pipeline, tinted by vertex colour. Glyphs are
    packed on shelves, pages are never evicted (retained items keep their UVs),
    and the changed rows upload once a frame (`HostGame.Draw`, before `Prepare`).
  - `HostRectCanvas` (`ServerRectCanvas` + text) replaces `ServerRectCanvas` in
    `HostPlatform`. Each string is one textured triangle array per atlas page,
    added to the item **in command order**. Godot drew glows on child items,
    which put them over everything else on that item; here they keep their
    place. Turned and vertical text use `AddSetTransform`.
- **Outline:** FreeType's stroker, outer border only, radius = outline size / 2
  (taken to be the stroke's full width; compare with the Godot build if
  outlines look thin or thick).
- **Glow:** `GlowBlur` reproduces `GodotBlur` on the CPU (33 taps, σ = 5 taps,
  taps `max(1, radius/8)` px apart, alpha × intensity, clamped), **per glyph**,
  and caches the result in the atlas. Godot blurred the whole string, so where
  halos overlap between letters the glow is a little fainter. Exact parity
  needs step 6's group buffers.
- **Not done here:** `DrawShapeGlow` (draws nothing in the host; it goes with
  the step 5 materials), `DrawTexture` (needs the texture abstraction), and
  **font fallback**: glyphs missing from Source Sans (CJK, emoji) are skipped.
  Godot fell back to system fonts. Fontconfig or a bundled Noto font is a later
  choice for the user.
- Host flag **`--scene=<name>`** (any `SceneKind`, any case) starts on that
  screen. Song select is the best text check: it has bold, glow and outline.
- Shot: `screenshots/step3-songselect.png` (bold titles, the gold glow on
  SCORES, the outlined LEVEL 1). Black squares there are buffered groups and
  shaders (steps 5 and 6).

### Step 4: UI and scenes ✅

- [x] UI systems render through the new canvas. Checked by opening every
  `SceneKind` with `--scene=` (2026-11-09). All of them open with no engine
  call and lay out right. Gaps left to later steps: colour-picker gradients and
  card art (shader materials, step 5), buffered groups (step 6), the profile
  avatar (rig art, step 8), and the editor overlay (step 9).
- [x] Scene switching was already ECS (`SceneDirector`); nothing Godot was left
  in it. `HostGame` now does the rest of the Godot `Main`'s job:
  - On a scene change it clears the command queue, plus the editor history when
    entering the editor.
  - **F12** saves a PNG to `user://screenshots` (`ScreenshotNaming`) and shows
    a toast. F12 is kept out of the shortcut queue, as Godot's
    `SetInputAsHandled` did.
  - **Add song:** SDL's native open dialog (`HostFileDialog`; the desktop
    portal on Linux), then `SongStore.Import` and song timing, as before.
    SDL may call back off the main thread, so the picked path is queued and
    handled in `Update`. This was planned for step 9 but belongs to `Main`.
  - **Text fields:** `HostTextInput` replaces both hidden `LineEdit` bridges
    (`UiTextBridge` and the editor overlay's). It uses SDL text input, so IME
    and keyboard layout work. The caret stays at the end, Backspace deletes and
    repeats when held, Ctrl+V pastes, Enter submits, and numeric fields keep
    digits, one dot and a leading minus. While a field has focus only Escape
    reaches the shortcut queue.
- [x] Window and settings adapters:
  - **VSync on:** `PresentMode.VSync`, with one update per display refresh
    (`SDL_GetCurrentDisplayMode`).
  - **VSync off:** mailbox (or immediate) presents and up to 1000 updates a
    second, as Godot ran uncapped. The settings toggle drives it through
    `HostPlatform.ApplyVSync`.
  - The game has no display-mode setting, so there was nothing else to adapt.
  - `UpdateTick` now gets real elapsed time, not MoonWorks' fixed step, since
    a late frame drops catch-up updates. MoonWorks' "Exceeded max update time"
    log is filtered out in `Program.cs`.
- Tried by hand with `xdotool`, running `SDL_VIDEODRIVER=x11` so XWayland
  accepts synthetic input: clicking a field to focus it, typing, Backspace,
  F12, and the vsync toggle both ways (the profile and `display.json` were
  backed up and put back afterwards). Closing the window that way ends with an
  X `BadWindow` error. That's an artefact of xdotool destroying the window,
  not of the host.
- Not done: the window isn't resizable, and the pointer scale is fixed from the
  start size. That can wait until a window-size setting exists.

### Step 5: Shaders: Slang, slots, blend modes, instance data (800–1,200 lines + libraries)

- [x] Shader compile service: Slang source → SPIR-V via `Slangc.NET`, with
  binding counts from Slang reflection filled into `ShaderCreateInfo` /
  `ComputePipelineCreateInfo` automatically, plus a disk cache keyed by source
  hash. (Done; see the notes below.)
- [x] `xanadu.stage` v1 contract and the host's standard vertex shader (§2a).
- [ ] Slot model: vertex modifier, fragment chain wrapper generation, blend
  dropdown, and a pipeline cache keyed by (vertex, chain, blend, state), built
  at level load.
- [~] `IShaderCanvasItems` for the new renderer: a material per run (now a slot
  set), copy-screen and copy-rectangle.
- [ ] Per-item data (instance uniforms) in a storage buffer per frame (§3).
- [~] Rewrite the game shaders as slot effects: `Assets/Shaders/note_background`,
  `flare`, `wave_background`, plus the UI materials (`UiTextGradient`,
  `ColorPickerMaterials`, `GodotShapeGlow`).
- [x] Level shader slots move to the new model. `ShaderSourceRules` validates
  through Slang compile errors and the contract check. Godot shader source in
  levels is dropped (P3).
- [x] First shipped library modules, ported from the legacy library (§2a).

How the first half landed (2026-11-09):

- **Materials are engine-free now.** `CanvasMaterial` (`UI/Systems/Canvas`)
  holds a shader path, a blend, and named values and textures. `UiMaterial`
  holds one, scenes call `material.Set(...)` instead of `SetShaderParameter`,
  and `ICanvasServer` gained `SetMaterial` and `SetInstanceValue`.
  `IVisualRidFactory.LoadsAssets` replaces `visuals is GodotVisualRidFactory`.
  The host's factory says yes; test factories say no, as before. Under Godot
  these materials draw plain (P2: Godot is reference only).
- **Shipped effects are `.slang` files under `Assets/Shaders`,** each a
  fragment entry point named `shade`. The shared paths moved: `CommonShaders`'
  rainbow and the flare are `.slang` now. Old levels that name the
  `.gdshader` paths lose those shaders (P3).
- **`xanadu.stage` v1** (`Assets/Shaders/Library/xanadu/stage.slang`) is the
  contract. It defines `ItemVaryings`, `ItemInput` (uv, Godot's COLOR, vertex
  colour, local position, screen UV, pixel, time), `input(v)`, `sampleItem`,
  and the `[Default(...)]` attribute. The host owns sampler 0 (the item's
  texture) and uniform buffer 0 (the frame). An effect's textures start at
  sampler 1, and its values are uniform buffer 1. `xanadu.colour` holds
  `hsvToRgb` and `hueCycle`.
- **Host:**
  - `SlangShaders.CompileReflected` runs Slang with `-reflection-json`. From that
    it fills the `ShaderCreateInfo` counts, the sampler names, and the values'
    offsets, sizes and `[Default]`s.
  - `CanvasPipelines` caches one pipeline per (effect, blend), on Godot's five
    blend modes. An effect that is missing or fails to compile logs once and
    draws plain.
  - `CanvasRenderer` breaks batches on material. An item with instance values
    is a batch of its own. Values pack as the defaults, then the material's,
    then the item's.
  - Vertices carry their item-space position (`local`, Godot's VERTEX).
- **Glyph strips:** `HostPlatform.GlyphStrip` builds a `GlyphAtlas` on the GPU
  with mipmaps (`Host/Text/GlyphStrips.cs`), and `GlyphAtlas.TextureId` hands it
  to materials. Source Sans 3 has ♪ and ♫.
- **Dropped:** `GlowRingShader` (nothing selects it any more) was not ported.
  The F2 tuner's visual test was deleted: the tuner still reads Godot materials
  and finds none, so it needs rewriting on Slang reflection.
- **Not done yet:** `[ ]` items above that aren't ticked, namely:
  - level shader slots: slots draw plain; `ShaderSourceRules` still takes Godot
    source;
  - the fragment-chain wrapper and the vertex slot;
  - the disk cache and compiling at level load (effects compile on first draw
    today, which can hitch);
  - per-item data in a storage buffer (instance values are per batch for now);
  - frosted glass (needs the screen read, step 6);
  - `wave_background` and `preview_plasma` (nothing on the host path uses them yet);
  - `GodotShapeGlow`;
  - copy-screen and copy-rectangle (step 6).

Level slots, cache and tuner (2026-11-09):

- **A level shader slot holds an effect body** in Slang:
  - global `uniform`s, tagged `[Instance]` when a group's item-shader track
    may key them, with `[Default(...)]`s;
  - `float4 shade(ItemInput i)`.

  `SlangShaders.Wrap` puts it in `namespace effect0`, hoists its `import`
  lines, and adds `import xanadu.stage` and the entry point `xanaduFragment`.
  `#line` keeps Slang's errors on the author's own lines under "slot N (name)".
  `-fvk-bind-globals 1 3` makes the globals uniform buffer 1, and reflection
  lists them one by one. A chain would add one namespace per effect; the level
  format holds one effect per slot, so the chain waits for the format.
- **`ShaderSourceRules`** now refuses Godot source with a message saying so,
  and wants `float4 shade(ItemInput ...)`. Whether the effect compiles is only
  known when the host builds it; if not, it logs and draws plain. Old levels'
  Godot slot shaders are dropped on load (P3). **`ShaderSourceUniforms`**
  reads the `[Instance]` uniforms (the group-keyable settings). Defaults are
  taken as written, with missing channels zero, the same as Slang's reflection.
- **`LevelSlotMaterials`** makes one `CanvasMaterial.FromSource` per slot and
  blend, with the slot's tuned values set once, as Godot did.
  `ServerShaderCanvasItems.SlotMaterialOf` is set each frame by the play and
  editor draw systems. The host caches effects by their source text.
- **The demo level has a field shader in slot 1** (slow blue bands), so
  `--demo` covers the slot path.
- **Disk cache:** `user://shader_cache_slang/`, holding SPIR-V and
  reflection JSON. The key hashes the source, the entry and flags, every
  library module, and the Slangc.NET version.
  - **At start**, every shipped effect is compiled with mix (`WarmShipped`).
  - **When a level appears**, each of its slots is compiled (`WarmLevelShaders`
    in `HostGame`).
  - Blend variants are only pipeline state, so they are still made on first use.
- **F2 tuner:** it uses `CanvasMaterial`. `HostPlatform.DescribeMaterial`
  hands it the compiled effect's values. A four-channel value whose name says
  colour or tint gets a colour picker, standing in for Godot's `source_color`.
  There's no `[Range]` attribute yet; slider ranges are guessed from the value,
  as before.

Shipped library (2026-11-09): `Assets/Shaders/Library`, ported from the legacy
GLSL includes, keeping their function names so old effects carry over:

| Module | Contents |
|---|---|
| `xanadu.math` | `PI`, `TAU`, GLSL-style `mod` (HLSL's `fmod` truncates, which differs for negatives), `rotate`, `rand` |
| `xanadu.sdf` | circle, cross, ring, box, rounded box, chamfer box, equilateral triangle, parallelogram, hexagram, moon; `opRepPolar`, `opOnion`; `sdfAlpha`, `renderSDF`, `renderSDFWithOutline`, `outerGlowSDF`; `sdShape` by id |
| `xanadu.easing` | all 31 Penner easings and `getEasing` |
| `xanadu.colour` | `hsvToRgb`, `hueCycle`, `blendOverAlpha`, and the X11/web colours as `float4` constants |
| `xanadu.blend` | 12 in-shader blend modes (dodge and burn were named in the legacy include but never written; they are now), `blendMode`, `alphaOver`, `blendOver` |
| `xanadu.pattern` | line, checker and recursive-checker patterns, `tileUvWithId` |
| `xanadu.gradient` | `gradientT`: linear, radial, conical, square |
| `xanadu.curve` | cubic Bézier and its tangent |
| `xanadu` | re-exports all of the above, plus the stage |

- Rotations were rewritten from GLSL's column-major `mat2(...) * p` by hand.
- `sdShape`'s rounded box now passes its radii right (`zzww`). The legacy
  version passed the half size as the radii.
- **Left behind:**
  - `curve_node` (unfinished);
  - the global colours texture with `getColour` and `getSecondaryColour`
    (and so the `index_2` bug);
  - `Perspective`, which becomes the first vertex modifier when the vertex
    slot exists.
- **`xanadu.stage` v1 gained two fields** (additive, so still v1):
  `ItemInput.halfSize` and `ItemInput.centred`, the legacy `SIZE` macro's
  `CANVAS_HALF_SIZE` and `PIXEL_UV`. They are worked out from screen-space
  derivatives of `uv` against `local`, so they are exact for quads whose uv runs
  0..1 across them. One difference from the legacy macro: `centred` is y down,
  where `SIZE`'s corner names were y up.
- The demo's field shader now draws a rounded frame with `xanadu.sdf`, as a
  check of the library through the slot path.

### Step 6: Groups, buffers and layers ✅

- [x] Buffered groups (CanvasGroup equivalent): render to a texture, then
  composite with tint, alpha, shader and blend.
- [x] Screen-read regions (back-buffer copy).
- [x] Self-contained layers (`IPassSurfaces`) as offscreen targets.
- [x] Re-check the decisions in `docs/render-architecture.md` (D7–D17) against
  the new implementation (below).

How it landed (2026-11-09):

- **`CanvasRenderer` draws a frame as a list of jobs**, each one root drawn
  into one target, in order:
  1. every registered offscreen root (a self-contained layer's surface);
  2. every buffered group, found while walking the tree, each drawn into a
     frame-sized texture from a pool;
  3. the tree itself into the frame.

  Nested groups come before the job that composites them. All vertices go up in
  one upload, then each job is one render pass. Batches never merge across
  jobs: their vertices are adjacent in the buffer, and merging them was a bug.
- **A group's image** is premultiplied. It is composited back in its place as
  one quad covering the area its content drew into (plus 2 px). The quad uses
  the group item's material (buffer shader and blend), its self modulate times
  the inherited modulate (buffer tint × alpha × fade), and its instance values
  (buffer keys). `Frame.itemPremultiplied` tells `xanadu.stage` to hand effects
  straight colour, so any slot shader works on a composite as it does on an
  item. The group item's own draws go inside the image, plain.
- **Screen reads:** `xanadu.stage` gained `screenTexture` (host sampler 1) and
  `sampleScreen(uv, lod)`. That moves effect textures to sampler 2 and up;
  `note_background` was renumbered.
  - **When the screen is copied:** before a batch whose SPIR-V really samples
    the screen copy, or whose item called `SetCopyScreen`, the pass ends, the
    target is blitted into a mipmapped screen texture, mips are generated, and
    the pass resumes. This happens only if something was drawn since the last
    copy.
  - **Why the SPIR-V is scanned:** reflection lists every declared global,
    used or not. `CompiledShader.ReadsScreen` scans the SPIR-V for a variable
    bound at set 2, binding 1.
  - Frosted glass is ported (`Ui/frosted_glass.slang`). Song select makes 7
    copies a frame.
- **Self-contained layers:**
  - `HostPassSurfaces` gives each layer a parentless canvas item, drawn into its
    own frame-sized texture before the frame. `IPassSurface.Texture` (a Godot
    `Texture2D`) became `TextureId`, and `IRectCanvas` gained
    `DrawTexture(Rid, Rect2, Rid)`. `XanaduSystemRoot` takes a pass-surface
    factory for hosts other than Godot.
  - **Fixed for both builds:** the camera shift on the playfield root never
    reached a surface. On a level with a large camera shift the layer's content
    fell off its screen-sized surface, so it wasn't drawn at all.
    `ShaderRoutingCanvas` now puts the root's transform on every surface, and
    the pass quad covers `ToLocal(screen)`, the screen in the root's own
    coordinates. Two tests cover it.
- **`--demo`** now has a buffered group (the square block, drawn back through
  an inverting shader at 80%) and a self-contained layer (the long block,
  rippled by its pass shader), so both paths show in one shot. The shot line
  prints the number of offscreen passes and screen copies.

D7–D17 against the host:

- **Unchanged:** D7, D8, D9, D11, D12, D13, D14 and D17 hold as designed:
  - D8: a layer's surface is a host offscreen target instead of a SubViewport;
  - D14: buffer keys reach the composite through the group item's instance
    values;
  - D17: blend variants are pipelines made on first use.
- **D10 changed for the better:** without a region, Godot's screen read saw the
  snapshot from the last boundary. The host refreshes the copy whenever a read
  would otherwise be stale, so a read is always current. Regions are kept, as
  forced copies, but are no longer needed for correctness.
- **Known difference:** a screen read inside a buffered group samples the
  group's own image so far, not the screen behind it. The row on line ~311 of
  `docs/render-architecture.md`, "full back-buffer copy between members", read
  the screen from before the group. That's rare in levels; revisit if a level
  needs it.
- **Not checked:** MSAA and depth stay in "Later".

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
