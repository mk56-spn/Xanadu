# The host: Xanadu on MoonWorks

Xanadu runs on [MoonWorks](https://github.com/MoonsideGames/MoonWorks), a small
C# framework over SDL3 (SDL_GPU on Vulkan / D3D12 / Metal). Every exe is a
plain `dotnet run`; there is no engine. This doc holds the decisions behind the
host, how it is built, the tooling around it, and what is left for later.

Godot was removed on 2026-11-10. The port's step-by-step log is in git history
(`docs/moonworks-port.md` up to commit `337ff7a0`).

---

## 1. Why MoonWorks

- Plain .NET: every tool is a normal exe, with no per-assembly engine project,
  no `res://` root per tool, no symlinked assets.
- Features Godot's 2D would not give: 2D depth buffers, arbitrary blend modes,
  compute (GPU particles), instance data without per-shader limits, and control
  over present mode and frame latency.
- Godot's C# future (godot-dotnet, LibGodot hosting) had no date.

---

## 2. Decisions (locked)

Agreed with the user. Don't revisit them without asking.

| # | Decision |
|---|---|
| P1 | **MoonWorks**, not MonoGame / FNA / Stride. MonoGame's Vulkan backend exposes no compute or storage buffers and compiles shaders offline. Stride is 3D-first with its own shader language. MoonWorks gives SDL_GPU directly and is ~13k lines of C# we can patch. |
| P3 | **Break freely.** No migration of formats, settings or scenes unless it's trivial. |
| P4 | **The test suite stays green** and runs without a window (~2300 tests). See §7. |
| P5 | **MoonWorks is vendored from source**, pinned to a commit, as a trimmed copy in `External/MoonWorks/` (it is not on NuGet). Patch it locally when needed; zlib allows that, and `External/README.md` lists the patches. **Never send AI-written PRs upstream**: their `AGENTS.md` forbids it. |
| P6 | **Native libs come from Moonside's bundle**, not the OS (SDL3, FAudio, IRO, Wellspring, dav1dfile). `External/fetch-moonlibs.sh` fetches it with a SHA-256 check into gitignored `External/moonlibs/`; run it once after cloning. |
| P7 | **Shaders are [Slang](https://shader-slang.org)**, compiled to SPIR-V **at runtime** through the `Slangc.NET` NuGet package, because players write shaders and will get shader libraries. Slang's real modules replace GLSL's text-pasting `#include`. If `Slangc.NET` is abandoned, call `libslang` directly. |
| P8 | **Maths types:** vectors are `System.Numerics` (what MoonWorks and Friflo use), aliased project-wide by `Core/Maths/Maths.props`; Godot's vector methods live on them as C# 14 extension members (`Core/Maths/VectorExtensions.cs`). `Color`, `Rect2`, `Rect2I`, `Transform2D`, the `I` vectors and `Mathf` are our own types in `Xanadu.Maths`, copied from Godot 4.4 (MIT). Canvas ids are `Handle`. |
| P10 | **No mesh shaders** (SDL_GPU doesn't expose them). GPU-made geometry = compute into a storage buffer + vertex pulling + indirect draws. |
| P11 | **Shaders are exposed to players as slots, not whole shaders** (§3). |

(P2 "hard switch" and P9 "order of work" belonged to the port and are done.)

---

## 3. Shader slot model (P11)

| Slot | Default | Player can |
|---|---|---|
| Vertex | the host's standard quad | pick a library modifier (wobble, perspective tilt, squash) or write `ItemVertex deform(ItemVertex v, ItemInput i)` |
| Fragment | plain colour or texture | pick library effects or write `float4 shade(ItemInput i)`; several in a row form a **chain** |
| Blend | mix | dropdown: hardware modes (mix, add, subtract, multiply, screen, min, max, premultiplied) are free; "exotic" modes (overlay, soft and hard light, dodge, burn, difference, exclusion, HSL) go through a group buffer and the composite shader |
| State (later) | 2D defaults | depth test and write once 2D depth exists |

Built today: the fragment slot with one effect, and the blend modes Godot had.
The rest is under "Later" (§6).

- **The contract** is the Slang module `xanadu.stage`
  (`Assets/Shaders/Library/xanadu/stage.slang`), **versioned** (v1). Only ever
  add to it. It defines `ItemVaryings`, `ItemInput` (uv, colour, vertex colour,
  local position, screen UV, pixel, time, `halfSize`, `centred`, and
  `canvas`: the quad being drawn as it lands on the target, in pixels: `size`,
  `position`, `corner`, `rotation`, `shear`, `xAxis`, `yAxis`, `aspect`,
  `uvAt(pixel)`; worked out from uv derivatives, exact for any 0..1 quad), `input(v)`,
  `sampleItem`, `sampleScreen(uv, lod)` and `[Default(...)]`.
- **Bindings:** the host owns sampler 0 (the item's texture), sampler 1 (the
  screen copy) and uniform buffer 0 (the frame). An effect's own textures
  start at sampler 2 (by name, from `CanvasMaterial.Textures`) and its values
  are uniform buffer 1, a struct of any name.
- **Vertex and fragment compile separately** and meet when the pipeline is
  built, so any vertex choice pairs with any fragment choice. Almost no player
  writes a vertex stage (the user's estimate: 99% never will); vertex needs are
  covered by 10–20 in-house variants as a pick list.
- **A fragment chain is one shader:** the host wraps the effects, each in its
  own namespace, and calls them in order. One pass, one pipeline.
- **Blend is pipeline state, not shader code.** One compiled shader, a
  pipeline per blend state. Exotic modes are a runtime value in one composite
  shader.
- **Pipelines are cached** by (effect, blend), built on first use; effects are
  compiled at start (shipped ones) and at level load (a level's slots), with a
  disk cache, so nothing compiles mid-song.
- **Shipped libraries are Slang modules** (§5.5).

---

## 4. Facts and gotchas (verified)

- **SDL_GPU SPIR-V binding layout** (resources must be declared exactly so):
  - vertex: samplers → storage textures → storage buffers in `set = 0`; uniforms in `set = 1`.
  - fragment: the same order in `set = 2`; uniforms in `set = 3`.
  - compute: sampled textures → read-only storage textures → read-only storage
    buffers in `set = 0`; read-write storage textures → read-write storage
    buffers in `set = 1`; uniforms in `set = 2`.
- **Slang usage:** `SlangCompiler.Compile([path, "-target", "spirv", "-entry", <name>, "-stage", compute|vertex|fragment])`.
  The SPIR-V entry point comes out as **`main`**. Bindings are
  `[[vk::binding(index, set)]]`; varyings match by declaration order. Don't
  name an entry point after an intrinsic (`dot`): it shadows it. Errors come
  back rustc-style with a caret, good enough to show players as they are.
- **`ColorTargetBlendState.Additive` multiplies the source colour by source
  alpha.** Output alpha 0 and nothing draws.
- **Swapchain textures can't be read back.** Everything renders into an
  offscreen target that is blitted to the window; screenshots read that.
- **MSAA** (2×, 4×, 8×) resolves at the end of a pass. MSAA textures can't be
  sampled until resolved, and **depth can't be resolved**: MSAA plus sampled
  depth needs a design.
- **No framebuffer fetch.** Blend modes beyond fixed-function need a copy then
  blend in the shader, or a group rendered to a texture and composited.
- **Depth in 2D works.** A depth texture can be a target and sampled later;
  `SV_Depth` gives per-pixel sprite depth (Labs' depth lab).
- **SDL_GPU has no timestamp queries.** GPU timing comes from our own Vulkan
  layer (§5.10).
- **MoonWorks targets net9.0**; net10 projects reference it fine.
- **Auto mode treats a new NuGet package's native code as external code.**
  Expect to need the user's OK the first time a new native package runs.

---

## 5. How the host is built

### 5.1 Projects and exes

- **`HostCore`** (library): what every exe shares. `HostApp` is an abstract
  MoonWorks `Game` that owns the window (and its icon), the 1920×1080 target,
  fonts, keyboard, pointer and text input, `HostPlatform` wiring, vsync and
  window mode, F12, `--shot`, the GPU timing layer and the frame autopsy. An
  exe derives from it, builds its ECS world on `Canvas`, and runs it in `Tick`.
  It copies the moonlibs, `HostCore/Shaders/*` and the timing layer next to
  every exe.
- **`Host`**: the game (`HostGame`: the world, the add-song dialog, the editor
  overlay, level shader warm-up), `HostPassSurfaces` and `DemoLevel`.
- **Tools:** `Tools/Animator`, `UI/Showcase`, `Tools/Labs`,
  `Tests/VisualTests` are `HostApp`s; `Tools/Autopsy` reads frame captures.
- **Launchers:** `./run.sh` asks which to start; `run-game.sh`,
  `run-editor.sh`, `run-animator.sh`, `run-showcase.sh`, `run-labs.sh`,
  `run-autopsy.sh`, `run-visual-tests.sh`.
- **Game flags:** `--scene=<name>` (any `SceneKind`), `--demo` (a level made in
  memory with a field shader, a buffered group and a self-contained layer),
  `--play[=<name or file>]`, `--editor`, `--shot=<png>` (`--shot-frame=<n>`,
  default 60), `--resolution=WxH`.
- `GamePaths.RootMarker` (`Xanadu.csproj`) marks the game folder `res://`
  means; `user://` is `~/.local/share/godot/app_userdata/Xanadu` (kept so old
  profiles, songs and levels stay put).

### 5.2 Seams the game draws through

Game, UI and Core code never touch MoonWorks. They go through static seams the
host fills; tests leave the headless defaults.

- **`CanvasServer`** (`UI/Systems/Canvas/CanvasServer.cs`): a retained canvas
  item API (create, free, parent, z, draw index, transform, modulate, clip,
  material, instance values, canvas group, copy screen, draw commands).
  Default `HeadlessCanvasServer`.
- **`IRectCanvas`**: the drawing contract every draw system uses (shapes, text,
  glows, layers, buffers, shader selection), keyed by a canvas `Handle`.
  `ServerRectCanvas` draws shapes on the server; the host's `HostRectCanvas`
  adds text.
- **`HostPlatform`** (`UI/Systems/HostPlatform.cs`): keyboard, visual factory,
  FPS source, canvas factory, vsync, window mode, glyph strips, particles, GPU
  memory, material description for the F2 tuner.
- **`TextureServer`**: `Create` (RGBA8 pixels, optional GPU mipmaps),
  `CreateFloat` (RGBA32F data a shader reads with `Load`), `Load` (an image or
  SVG by `res://` path) and `Free`.
- **`GamePaths`** (`Core/Storage/GamePaths.cs`) is the only place `res://` and
  `user://` become real paths (no IO spread).
- Draw-system tests pass `Tests/Support/Fakes/FakeShaderCanvasItems`.

### 5.3 Canvas tree and renderer

- **`CanvasTree`** (`HostCore/Rendering`): the retained items. An item draws
  before its children; siblings sort by draw index, then by when they were
  parented; z is relative to the parent and sorts the whole tree. Ids are
  minted as `Handle`s; textures share the id space (`CanvasRenderer.Textures`).
- **Draw commands:** rect, line, polyline (mitred), multiline, polygon (ear
  clipping, `Core/Rendering/Triangulate.cs`), triangle array, circle, textured
  rect, set-transform.
- **`CanvasRenderer`** draws a frame as a list of jobs, each one root into one
  target, in order: every offscreen root (self-contained layers), every
  buffered group (nested first, each into a pooled frame-sized texture), then
  the tree into the frame. All vertices go up in one upload; each job is one
  render pass. Batches break on texture, clip rect and material; an item with
  instance values is a batch of its own. Batches never merge across jobs.
- **Materials:** `CanvasMaterial` holds an effect path (or a level slot's
  source), a blend (or a full `CustomBlend`), named values and textures.
  Values pack as the effect's `[Default]`s, then the material's, then the
  item's instance values. `CanvasPipelines` caches a pipeline per (effect,
  blend, custom blend); a missing or broken effect logs once and draws plain.
- **Shader routing:** `ShaderRoutingCanvas` splits one item's draws into runs
  of child items, one per change of shader, so things stack exactly as drawn
  (`SelectCommonShader`, `SelectShader`, `SetShaderValue`, `NewRun`).
- **Buffered groups:** the group's image is premultiplied and composited back
  as one quad over what its content covered (plus 2 px), with the group item's
  material, self modulate × inherited modulate, and instance values (buffer
  keys). `Frame.itemPremultiplied` makes `xanadu.stage` hand effects straight
  colour, so any slot shader works on a composite.
- **Screen reads:** before the first batch in a pass whose SPIR-V samples the
  screen (`CompiledShader.ReadsScreen` scans for set 2, binding 1; reflection
  lists unused globals too), the pass ends, the target is blitted into the
  screen texture, and the pass resumes. Later reads share that copy; an item
  with `SetCopyScreen` copies again if anything was drawn since. A read is
  therefore always current (better than Godot's snapshot; see
  `render-architecture.md` D10). `ScreenMips` builds levels 1–6 through a
  13-tap Gaussian-like filter (`HostCore/Shaders/screen_mips.slang`), so the
  frosted glass reads smooth from one low-mip tap.
- **Self-contained layers:** `HostPassSurfaces` gives each layer a parentless
  item drawn into its own texture before the frame; `ShaderRoutingCanvas` puts
  the root's transform on every surface, and the pass quad covers the screen
  in the root's coordinates.
- **Known difference:** a screen read inside a buffered group samples the
  group's own image so far, not the screen behind it.
- **GPU scopes:** `using (commands.Scope("name"))` names GPU work for the
  timing layer (§5.10). Open and close it on the same side of a render pass.

### 5.4 Text

- **FreeType + HarfBuzz** (`FreeTypeSharp`, `HarfBuzzSharp`; MIT, own natives),
  host only (`HostCore/Text`). Regular text is Source Sans 3 at weight 400,
  bold the same file at 700; mono is JetBrains Mono.
- `HostText` owns the font files, faces and a glyph cache keyed by (face, size,
  glyph, look). `FontFace` shapes with HarfBuzz and renders with FreeType at
  light hinting on whole-pixel pens; shaped runs are cached per (text, size).
  `FreeTypeVariations` P/Invokes the variable-font calls; set the weight on
  both libraries or advances and outlines disagree.
- `TextAtlas`: 1024² RGBA pages, white with coverage in alpha, so glyphs draw
  through the canvas pipeline tinted by vertex colour; shelf-packed, never
  evicted; changed rows upload once a frame. Each string is one triangle array
  per page, added **in command order**.
- Outline: FreeType's stroker, outer border, radius = outline size / 2. Glow:
  `GlowBlur` on the CPU, per glyph, cached in the atlas (where halos overlap
  between letters the glow is slightly fainter than a whole-string blur).
  `HostShapeGlow` does the same for polygon and polyline glows.
- `GlyphStrips` builds a `GlyphAtlas` texture with mipmaps for materials
  (note glyphs, particles).
- **No font fallback:** glyphs missing from Source Sans (CJK, emoji) are
  skipped. Fontconfig or a bundled Noto is the user's call.

### 5.5 Shaders

- **Compile service** (`HostCore/Rendering/SlangShaders.cs`): Slang with
  `-reflection-json`, which fills the `ShaderCreateInfo` counts, sampler names,
  and values' offsets, sizes and `[Default]`s. Disk cache in
  `user://shader_cache_slang/` (SPIR-V + reflection), keyed by the source,
  entry and flags, every library module, and the Slangc.NET version. Shipped
  effects compile at start (`WarmShipped`); a level's slots when it appears
  (`WarmLevelShaders`). Blend variants are pipeline state, made on first use.
- **Shipped effects** are `.slang` files under `Assets/Shaders`, each a
  fragment entry point `shade` (note background, flare, rainbow, frosted
  glass, text gradient, colour picker, editor waveform).
- **Level shader slots** hold an effect body: global `uniform`s (tagged
  `[Instance]` when a group's item-shader track may key them, with
  `[Default(...)]`s) and `float4 shade(ItemInput i)`. `SlangShaders.Wrap` puts
  it in a namespace, hoists its imports, adds `import xanadu.stage` and an
  entry point, and uses `#line` so errors land on the author's lines.
  `-fvk-bind-globals 1 3` makes the globals uniform buffer 1.
  `ShaderSourceRules` checks the shape; `ShaderSourceUniforms` reads the
  `[Instance]` uniforms; `LevelSlotMaterials` makes one material per slot and
  blend.
- **F2 tuner:** reads the compiled effect's values through
  `HostPlatform.DescribeMaterial`. A four-channel value named like a colour or
  tint gets a colour picker; slider ranges are guessed (no `[Range]` yet).
- **Shipped library** (`Assets/Shaders/Library`), struct-based since
  2026-11-10 (no compatibility with the legacy GLSL names). Shapes are values:
  make one, change it, draw it. Everything that isn't a type lives under its
  own namespace, so a shader editor's global names stay few.

  | Module | Contents |
  |---|---|
  | `xanadu.math` | `PI`, `TAU`, GLSL-style `mod`, `rotate`, `random` |
  | `xanadu.shape` | `IShape` (`distance(p)`); `Circle`, `Box` (rounded), `CornerBox`, `ChamferBox`, `Cross`, `Arc`, `Triangle`, `Parallelogram`, `Star6`, `Moon`; on every shape `.turn` `.move` `.scale` `.repeat` `.onion` `.or` `.and` `.minus` `.melt`, and `.fill` `.stroke` `.glow` |
  | `xanadu.color` | `color.hsv`, `color.hue`, `color.over`, and the X11/web colours as `color.gold`, `color.midnightBlue`, … |
  | `xanadu.ease` | `ease.inOutSine(t)` and the other 30 Penner easings, `ease.by(n, t)` |
  | `xanadu.blend` | `blend.overlay(under, over)` and 11 more, `blend.by(blend.Mode.X, …)`, `blend.composite` |
  | `xanadu.pattern` | `pattern.columns`, `.rows`, `.checker`, `.checkerSplit`, `.tile` (→ `Tile`) |
  | `xanadu.gradient` | `gradient.linear`, `.radial`, `.conical`, `.square` |
  | `xanadu.curve` | `Bezier(a, b, c, d)` with `.at(t)` and `.tangent(t)` |
  | `xanadu` | re-exports all of the above, plus the stage |

  ```slang
  let petals = Box(float2(28.0), 10.0).repeat(8.0, 160.0).turn(time);
  float3 c = lerp(color.midnightBlue.rgb, color.gold.rgb, petals.fill(i.centred));
  ```

  Slang has no named arguments; the shader editor shows parameter names as
  inlay hints instead.

- **Named defaults:** `[Default(color.gold)]` or `[Default(PI, 2)]` work.
  Slang attributes take only literals, so `ShaderConstants` (Core, loaded by
  the host from the library's `public static const`s) swaps names for their
  numbers in `SlangShaders.Wrap`, and `ShaderSourceUniforms` reads them the
  same way. Slang's own messages then quote the numbers.
- **Style:** `docs/shader-style.md` collects the house style as numbered
  rules (whole numbers without `.0`, whole-number division, …) for the
  shader editor to warn about later. None are checked yet.

### 5.6 Particles

- **Spawn maths is shared and tested:** `Core/Particles/ParticleSpawn.cs` turns
  an `XpfParticle` into `ParticleSeed`s (explosiveness as birth delays,
  randomness as shorter lifetimes, spread cone, heading, box, upright angle,
  note cell, scale curve, colour ramp), and `ParticleSpawn.Stream` for flows.
- **Seam:** `IParticleServer.Emit(item, effect, seeds, strip, cells)` via
  `HostPlatform.Particles`; `CanvasParticleEmitter` on the playfield;
  `ParticleStream` (`IParticleFlow`) for rig streams and trails.
- **Host** (`HostCore/Rendering/CanvasParticles.cs`, `HostCore/Shaders/particles.slang`):
  an item gets a `ParticleField` on its first burst, a ring of 8,192 slots in
  a storage buffer (a full ring cuts off the oldest). Written slots upload
  each frame; each live field gets one compute dispatch and is drawn as one
  instanced draw right after its item's own draws, so z, groups and layers
  order it like any item. Per-effect gravity, ramp and scale curve live in one
  table of 32 samples each.
- A buffered group's composite bounds ignore particles (nothing relies on it).

### 5.7 Textures, SVG and rigs

- `HostTextures` uploads RGBA8 (mipmaps made on the GPU), RGBA32F data, and
  images through IRO. `.svg` is rasterised at scale 1.
- `Core/Rendering/SvgRaster.cs` (Svg.Skia + SkiaSharp natives) renders an SVG
  to straight-alpha RGBA (`RasterImage`); skin coverage reads the kept image.
- Rig art (`CharacterRigDrawSystem`, `SvgAttachmentVisuals`,
  `CharacterRigGhosts`) draws through the canvas; effect polygon shaders go
  through `CanvasServer.SetMaterial`.
- UI icons: `UiImage` / `UiIcons` carry a texture id from `TextureServer`.

### 5.8 Window, input, editor overlay

- **VSync on:** `PresentMode.VSync`, one update per display refresh. **Off:**
  mailbox (or immediate) and up to 1000 updates a second. `UpdateTick` gets
  real elapsed time. Window mode (windowed, fullscreen, borderless) is a
  setting applied through `HostPlatform.ApplyWindowMode`.
- **Text input:** `HostTextInput` (SDL text input, so IME and layouts work)
  types into any focused `TextField`; while a field has focus only Escape
  reaches the shortcut queue.
- **File dialogs:** `HostFileDialog` (SDL's native dialog; the desktop portal
  on Linux), for adding songs and importing `.slang` shaders. SDL may call back
  off the main thread, so picks are queued and handled in `Update`.
- **Editor overlay:** `EditorOverlay` is engine-free; `HostGame` ticks it once
  a frame, creates it when the editor opens and closes it when another scene
  opens.
- **F12** saves a PNG to `user://screenshots` and shows a toast.
- The window isn't resizable, and the pointer scale is fixed from the start
  size.
- Synthetic input for manual checks: `SDL_VIDEODRIVER=x11` plus `xdotool`
  (closing the window that way ends with a harmless X `BadWindow`).

### 5.9 Tools

- **Animator** (`Tools/Animator`): `AnimatorTool` derives from `HostApp`.
  `res://` is the game folder here too; its content lives under
  `res://Tools/Animator/...` (`Files/AnimatorPaths.cs`). Flags: `--shot`,
  `--shot-frames`, `--shot-seconds`, `--flow`, `--clip` and more. The
  metronome writes its ticks once to `user://animator/*.wav` and plays them
  through miniaudio.
- **UI Showcase** (`UI/Showcase`): a `HostApp`.
- **Visual tests** (`Tests/VisualTests`): see its README. Every test starts
  with the test user folder's recovery files cleared.
- **Xanadu Labs** (`Tools/Labs`): tabs, an ECS side panel per experiment, and
  a stage. Experiments implement `ILabExperiment`; stages that render on their
  own get `ILabStage.Render` (called from `HostApp.RenderOffscreen`) and show
  the result through `LabContext.Show`. Panel rows live in `LabPanel`.
  - **Shader playground:** watches `Tools/Labs/Playground/playground.slang`
    (gitignored, made from the template) or `--shader=<file>`, recompiles off
    the frame on save and swaps it in; errors show in mono in the panel and the
    last good version stays. The format is a level slot, so anything made here
    pastes into a level. Sets `mouse` and `resolution`.
  - **Shader editor** (tab 1; the start of the in-game shader editor): a
    level slot typed in the lab (`Tools/Labs/Editor/`: `CodeBuffer` text and
    caret, `CodeView` drawing, `EditorChrome` round it, `SlangProblems`,
    `SlangHints`), compiled 0.3 s after typing stops, saved to the git-ignored
    `Playground/editor.slang` (started from `editor.template.slang`). It uses
    the whole body (`ILabExperiment.FullWidth`, no side panel): code left with
    a status bar under it, a 16:9 preview top right (the playfield's shape;
    the pointer counts only over it), problems and a key guide under that.
    Linting is Slang's own errors: the line tinted, the word underlined, the
    message on a row under the line, listed under the preview (a click jumps).
    Under the problems, a slider per declared value and channel
    (`EditorTuning`), except the ones the lab fills; ranges are guessed as the
    F2 tuner does (`Core/Rendering/UniformRanges`), tuning survives recompiles.
    Inlay hints show library parameter names (`Box(size: …, radius: …)`)
    without them being in the text; they come from the library's `public`
    signatures, matched by name and argument count (no hint when ambiguous).
    It claims the keyboard through `HostTextInput.Claimed`. No selection,
    undo or horizontal scroll yet.
  - **Particles:** bursts every library effect once a second.
  - **Blend:** two quads with the whole fixed-function blend open
    (`CanvasMaterial.Custom`); the renderer sets the blend constant per draw.
  - **Depth + stencil:** quads into the lab's own colour and depth-stencil
    target, each writing depth per pixel through `SV_Depth`; depth and stencil
    state all open.
  - **Transparency:** order-dependent against order-independent transparency.
  - **Indirect draw:** up to a million GPU-only shapes.
  - **Pointer history:** the pointer's path as a ring of keys on the GPU.
  - **RTS:** an RTS that lives on the GPU.
  - **Radiance cascades:** 2D GI, GPU-bound at ~22 ms with the bilinear fix.
  - `dotnet run --project Tools/Labs -- --tab=<n>` opens a tab.

### 5.10 Perf tooling

- **GPU times** (`HostCore/Native/gpu_times_layer.c`, manifest
  `xanadu_gpu_times.json`, reader `HostCore/Rendering/GpuTimes.cs`): a Vulkan
  layer that puts timestamps around every SDL debug group and command buffer.
  HostCore's csproj builds it with gcc against headers from
  `External/fetch-vulkan-headers.sh` (gitignored); without them it's skipped.
  On by default when built; `XANADU_GPU_TIMES=0` turns it off, `=1` also prints
  a tree each second. Name GPU work with `commands.Scope("name")` (§5.3).
- **Autopsy of a Frame** (`HostCore/Autopsy`, `Tools/Autopsy`):
  - Every host exe records each launch: the CPU frame split into update (every
    Friflo system, nested), draw (wait for swapchain, record + submit) and
    idle, plus the GPU groups of the same frame. Saved on close or crash as
    `user://autopsy/<app>.xfa` (versioned chunked binary, `FrameAutopsy.cs`),
    owned by `AutopsyStore`. Keeps the last 1800 frames, the 16 worst CPU and
    GPU frames, and a mean frame. `--shot` runs don't save unless
    `XANADU_AUTOPSY=1`.
  - `XANADU_PROFILE=1` adds .NET CPU sampling (EventPipe on the own process) to
    `user://autopsy/<app>.nettrace`; the METHODS view reads it with TraceEvent.
    Sample bias: time can land on the next allocation; trust *total* over
    *self* for small methods.
  - Views: FRAME (recent frames, mean / latest / worst, CPU and GPU split bars
    to drill into) and METHODS.
  - No window: `dotnet run --project Tools/Autopsy -- --dump[=app]` (text
    trees) and `--dump=app --under=<method>`.
  - `./run-autopsy.sh game|editor|labs|animator` runs that exe with profiling,
    then opens the tool.
- **F3 overlay:** per-system times, and GPU memory (this app, budget, card) from
  `VulkanMemoryBudget`.
- **Query cache** (`UI/Ecs/QueryCache.cs`): `store.Cached<T>()` and
  `store.ToggleOn<TTag>(none, hidden)`; see `docs/ecs/README.md`.

---

## 6. Later

No hurry; none of this is parity.

- [ ] Shader slot model (§3): vertex modifiers, fragment chains (the level
  format must hold more than one effect per slot), the blend dropdown with
  exotic modes, a pipeline cache keyed by (vertex, chain, blend, state).
- [ ] Per-item data (instance values) in a storage buffer per frame. Instance
  values draw right today, one batch per item that has them; moving them
  breaks `xanadu.stage` v1's "values are uniform buffer 1". Revisit if those
  draws show up as a cost.
- [ ] 2D depth buffer: per-item depth, per-pixel sprite depth maps, sampled
  depth (fog, outlines, soft particles).
- [ ] MSAA (decide how it interacts with depth sampling, §4).
- [ ] Photoshop-style blend modes via group composite.
- [ ] 2D lighting: point and directional lights, normal maps, 1D shadow maps
  with PCF, light masks. Later: SDF or radiance-cascade GI (the radiance lab's
  next step is 2.5D, depth as occluder height).
- [ ] Font fallback (§5.4).
- [ ] Resizable window.
- [ ] Perf, when it matters: GPU group tracks by song time, then tight hulls
  for overdraw hogs. `store.Query<…>().ToEntityList()` every frame is still
  common; convert hot ones to `Cached` as they show up in Autopsy.
- [ ] Play a level start to end by hand on the host (never done end to end
  during the port).

---

## 7. Verification

- **Build:** `dotnet build` on each project touched.
- **Tests:** `dotnet test Tests/Xanadu.Tests.csproj` (~2300) and
  `dotnet test UI/Tests`. Check for "Test Run Aborted"; `--blame-crash` finds
  the culprit. Known failures that predate the port: friction, LayerPass,
  SelfModulate, XmfBinary, key binding, `EveryButtonActionHasAKey`, two
  `EditorPlayfieldDrawSystemTests`, `MapOffsetSettingsTests.ZeroLeavesNoEntry`.
  Don't count them as regressions or fix them unasked.
- **Visual:** one screenshot from `--shot` (or a visual test's
  `--visual-test-shot`), saved under `screenshots/` (gitignored) or the
  scratchpad. Never commit images outside `Assets/`.
