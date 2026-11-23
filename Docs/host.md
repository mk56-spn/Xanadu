# The host: Xanadu on MoonWorks

Xanadu runs on [MoonWorks](https://github.com/MoonsideGames/MoonWorks), a small
C# framework over SDL3 (SDL_GPU on Vulkan / D3D12 / Metal). Every exe is a
plain `dotnet run`; there is no engine. This doc holds the decisions behind the
host, how it is built, the tooling around it, and what is left for later.

Godot was removed on 2026-11-10. The port's step-by-step log is in git history
(`Docs/moonworks-port.md` up to commit `337ff7a0`).

---

## 1. Why MoonWorks

- Plain .NET: every tool is a normal exe, with no per-assembly engine project,
  no asset root per tool, no symlinked assets.
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
| P7 | **Shaders are [Slang](https://shader-slang.org)**, compiled to SPIR-V **at runtime** by `libslang`, called directly (`Slang/SlangNative.cs`; the `Slangc.NET` NuGet package only ships the native library), because players write shaders and will get shader libraries. Slang's real modules replace GLSL's text-pasting `#include`. |
| P8 | **Maths types:** vectors are `System.Numerics` (what MoonWorks and Friflo use), aliased project-wide by `Core/Maths/Maths.props`; Godot's vector methods live on them as C# 14 extension members (`Core/Maths/VectorExtensions.cs`). Scalar maths is .NET's (`Math`, `MathF`, `float.*`). `Color` (float, so HDR and fades stay exact; MoonWorks' byte `Color` only at the GPU edge), `Rect2`, `Transform2D` and a slim `Mathf` of Godot-only helpers are our own types in `Xanadu.Maths`, copied from Godot 4.4 (MIT) and trimmed to what is used (2026-11-20). Canvas ids are `Handle`. |
| P10 | **No mesh shaders** (SDL_GPU doesn't expose them). GPU-made geometry = compute into a storage buffer + vertex pulling + indirect draws. |
| P11 | **Shaders are exposed to players as slots, not whole shaders** (§3). |

(P2 "hard switch" and P9 "order of work" belonged to the port and are done.)

---

## 3. Shader slot model (P11)

| Slot | Default | Player can |
|---|---|---|
| Vertex | the host's standard quad | pick a library modifier (wobble, perspective tilt, squash) or write `ItemVertex deform(ItemVertex v)` |
| Fragment | plain colour or texture | pick library effects or write `float4 shade(Canvas canvas)` (it may take `ItemInput i` too); several in a row form a **chain** |
| Blend | mix | dropdown: hardware modes (mix, add, subtract, multiply, screen, min, max, premultiplied) are free; "exotic" modes (overlay, soft and hard light, dodge, burn, difference, exclusion, HSL) go through a group buffer and the composite shader |
| State (later) | 2D defaults | depth test and write once 2D depth exists |

Built today: the fragment slot with one effect, a body's own `deform` as its
vertex stage (Docs/shaders.md §15: it compiles beside the fragment as vertex
set 0/1, its values are vertex uniform buffer 1 laid out as the fragment's, the
frame is vertex buffer 2, the depth vertex sampler 0 and the globals vertex
storage buffers 1 on; no other textures), and the blend modes Godot had. No library modifiers yet.
The rest is under "Later" (§6).

- **The contract** is the Slang module `xanadu.stage`
  (`Assets/Shaders/Library/xanadu/stage.slang`), **versioned** (v1). Only ever
  add to it. It defines `ItemVaryings`, `ItemInput` (uv, colour, vertex colour,
  local position, screen UV, pixel, time, and
  `canvas`: the quad being drawn as it lands on the target, in pixels: `size`,
  `position`, `corner`, `rotation`, `shear`, `xAxis`, `yAxis`, `centred`, `aspect`,
  `uvAt(pixel)`; worked out from uv derivatives, exact for any 0..1 quad), `input(v)`,
  `sampleItem`, `sampleScreen(uv, lod)`, `sampleDepth(uv)`, `[Default(...)]`,
  `[Instance]` and `[Global]`. Nothing in it (or anywhere in `xanadu`) knows
  the game: the game's data comes in as globals (Docs/shader-pipeline.md §0).
- **Bindings:** the host owns sampler 0 (the item's texture), sampler 1 (the
  screen copy), sampler 2 (the depth) and uniform buffer 0 (the frame: size,
  time, beat). An effect's own textures start at sampler 3 (by name, from
  `CanvasMaterial.Textures`) and its values are uniform buffer 1, a struct of
  any name. Then come the **globals**: every `[Global] public
  StructuredBuffer<T> name;` in the library, in path then declaration order
  (`SlangShaders.Globals`), as storage buffers right after the last sampler.
  `SlangModules` rewrites each marker to a `vk::binding` on
  `XANADU_GLOBAL_<name>`, and `SlangShaders` sets those per stage (after the
  samplers in a fragment, from 1 in a deform's vertex stage, from 0 in
  `update`). The C# side is `ShaderGlobals` (Core/Rendering): whoever owns
  the data declares it by name and writes it; `CanvasRenderer` runs the
  per-frame writers, uploads what changed and binds every global to every
  effect stage (zeros for one with no data). A compile that reads a global
  nothing declared is an error. The game's globals (`playFrame`, `chart`:
  `rhythm.play`) are declared by `RhythmGlobals` in the game, the shader tool,
  the labs and the dev tools.
- **Depth reads** (`sampleDepth`): before a job whose effects read the depth
  (`CompiledShader.ReadsDepth`, from the SPIR-V), `CanvasRenderer` draws the
  job's opaque pre-pass again, depth only, into a single-sampled sampleable
  depth texture (MSAA depth can't be resolved), and binds that as fragment
  sampler 2 and deform vertex sampler 0, point sampled.
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
- **Slang usage:** `SlangShaders.Compile(path, <name>, compute|vertex|fragment)`: a `slangc` command line (`path -target spirv -entry <name> -stage <stage>`) run in process.
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

- **`Slang`** (library, `Xanadu.Slang`): the systemic shader layer, no GPU,
  UI or game. Compiling effect bodies and reflecting them (`SlangShaders`),
  the source rewrites and scans (`ShaderChains`, `ShaderConstants`,
  `ShadeSignature`, `ShaderSourceUniforms`), and the code editor's language
  smarts (`SlangCompletions`, `SlangHints`, `SlangLint`, `SlangRename`,
  `SlangProblems`, `SlangSyntax`, `SlangBrackets`). Tested on its own by
  `Slang/Tests` (`dotnet test Slang/Tests`), which may reach Core and UI but
  never the game. The shaders themselves stay in `Assets/Shaders`.
- **`HostCore`** (library): what every exe shares. `HostApp` is an abstract
  MoonWorks `Game` that owns the window (and its icon), the 1920×1080 target,
  fonts, keyboard, pointer and text input, `HostPlatform` wiring, vsync and
  window mode, F12, `--shot`, the GPU timing layer and the frame autopsy. An
  exe derives from it, builds its ECS world on `Canvas`, and runs it in `Tick`.
  It copies the moonlibs, `HostCore/Shaders/*` and the timing layer next to
  every exe.
- **`Game`**: the game's code (`Game/Xanadu.csproj`: Animation, Ecs, Editor,
  Levels, Scenes, Systems and the rest) and its exe, **`Game/Host`**
  (`HostGame`: the world, the add-song dialog, the editor overlay, level
  shader warm-up), `HostPassSurfaces` and `DemoLevel`. The repo's top level is
  the engine (`Core`, `UI`, `HostCore`), `Game`, `Tools`, `Tests`, `External`,
  `Assets`, `docs` and the `run-*.sh` launchers.
- **Tools:** `Tools/Animator`, `UI/Showcase`, `Tools/Labs`, `Tools/DevTools` and
  `Tests/VisualTests` are `HostApp`s. Labs and the dev tools share the tabbed
  tool shell; the dev tools hold Atlas (the type chart) and Autopsy (frame
  captures) as two tabs.
- **Who owns which systems:** the host owns the UI, `UiRoot` (`UI/Systems/Ui`),
  on `HostApp.Entities`. Each frame it runs UI input, then the exe's own root,
  then layout, drawing and the F1/F3/F4 overlays. An exe owns its root (the
  game's `XanaduSystemRoot`, the animator's) on the same store and hands it over
  with `UseSystems`; it never boots UI itself. F3 and the frame autopsy show the
  exe's root nested under the UI's step for it. Input the exe feeds its systems
  goes in `BeforeSystems`. Only the visual-test harness opts out (`HostUi.None`):
  it makes a store and a `UiRoot` per scene.
- **Launcher:** `./run.sh <target> [args]`, one script for every exe: `game`,
  `editor`, `labs`, `animator`, `showcase`, `visual-tests`, `atlas`, `autopsy`.
  With no target it asks which in the terminal.
- **Game flags:** `--scene=<name>` (any `SceneKind`), `--demo` (a level made in
  memory with a field shader, a buffered group and a self-contained layer),
  `--play[=<name or file>]`, `--editor`, `--shot=<png>` (`--shot-frame=<n>`,
  default 60), `--resolution=WxH`.
- `GamePaths.RootMarker` (`Xanadu.sln`) marks the game folder, which every
  relative path is under (`Assets/Shaders/flare.slang`); `user://` is `~/.local/share/xanadu` (`$XDG_DATA_HOME/xanadu`; `%APPDATA%\xanadu` on Windows).

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
  SVG by its path in the game folder) and `Free`.
- **`GamePaths`** (`Core/Storage/GamePaths.cs`) is the only place game-folder
  and `user://` paths become real ones (`GamePaths.Full`; no IO spread).
- Draw-system tests pass `Tests/Support/Fakes/FakeShaderCanvasItems`.

### 5.3 Canvas tree and renderer

Rebuilt 2026-11-20 off the Godot canvas model: primitives, a GPU pool, a
compute expand stage and a depth buffer.

- **Recordings.** A canvas item (`CanvasTree`, `HostCore/Rendering`) records
  *primitives* (`Primitive.cs`), not triangles: boxes (rect, rounded, stroked,
  two-colour ramp), discs and rings, textured quads (one per glyph, one per
  sprite) and free triangles (polygons, mitred polylines, meshes). Lines are
  boxes along them. Nothing is tessellated on the CPU unless an effect needs a
  mesh.
- **Mounts, not mirrors.** `ICanvasServer.Mount(item, ICanvasMount)` hangs a
  source under an item; each frame the renderer asks it what to draw there.
  The UI is one: `UiCanvasMount` walks layers and the entity tree, working out
  transform, `LayerOpacity`, `LocalZIndex`, container clips and visibility
  (`Enabled`, the `CanvasHidden` tag) from components on the spot. UI entities
  keep only their own recording (`VisualItem`); there are no parenting,
  transform, visibility or opacity sync systems. A rig's particle
  streams (`RigParticleStreams`) are a mount too. Game groups and tools still hang items under the root with a z.
- **Pool.** Every recording's primitives live on the GPU in its own space, in a
  range that stays while it fits (`Resident`). A recording goes up again only
  when its content changes: a hash of the primitives means a drawer that
  clears and redraws the same picture every frame sends nothing. Settled
  screens send 0 primitives a frame. A full pool starts a bigger empty one and
  everything drawn is sent once.
- **Instances and the expand stage.** Per frame the CPU walks the tree in
  order and sends one `GpuInstance` per drawn range: transform, tint, clip,
  depth base, its place in the output and its texture slots.
  `primitive_expand.slang` (compute, one workgroup an instance) writes each
  primitive in pixels, in order: placed, tinted, clipped, its depth, its run
  slot. A primitive outside its clip becomes a skip (folded away by the vertex
  stage), so order needs no scan.
- **Depth.** Each primitive has the depth of its place in the frame. Plain
  opaque rects (square, solid, whole-pixel) leave the ordered list for an
  opaque pre-pass, cut to their clip, counted into an indirect draw with an
  atomic add: drawn first with depth writes, they hide what they cover from
  everything after. Everything else draws in order with the depth test on.
  Effects (on the standard vertex stage) and particles test the depth of their
  place, never writing it. An effect with its own `deform` takes a slice of
  2^14 steps below its place and writes depth within it (`ItemVertex.depth`
  squashed into the slice, LessOrEqual so flat ties keep their order), so its
  nearest triangle wins per pixel and it still layers among the rest; rects
  after it may join the pre-pass.
- **Drawing.** One pipeline (`primitives.slang`) pulls records by vertex id
  (six a record) and shapes them by signed distance (`xanadu.shape`), so edges
  are smooth at any turn or scale; up to eight textures a draw (glyph pages,
  images). The editor is two draw calls; HEAD before the rebuild was 55.
- **Material runs.** `SelectCommonShader` / `SetShaderValue` / `NewRun` on a
  canvas start a run in the item (`ICanvasServer.SelectRun`); the renderer cuts
  the item into plain ranges (instances) and shaded ranges (effect steps), in
  order. An item with a material (`SetMaterial`) is shaded whole. Shaded
  draws keep the effect path: primitives cut into `CanvasVertex` triangles
  (`Primitive.Tessellate`), values packed per run (`[Default]`s, material,
  run). Level shader slots went with v2; `SelectShader(0)` is plain.
- **Screen reads:** before the first effect in a pass whose SPIR-V samples the
  screen (`CompiledShader.ReadsScreen`), the pass ends, the target is blitted
  into the screen texture with mips (`ScreenMips`), and the pass resumes
  (colour and depth loaded); later reads in the pass share that copy.
- **Gone:** canvas groups, `SetCopyScreen`, per-item instance values, and the
  layering surface on `IRectCanvas` (`BeginLayer`, `BeginBuffer`, `CopyScreen`,
  `ToLocal`), unused since the v2 rework.
- **Offscreen roots** (`AddOffscreen`, the shader tool's stills) draw into their
  own target, single-sampled, before the frame.
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

- **Compile service** (`Slang/SlangShaders.cs`): Slang with
  `-reflection-json`, which fills the `ShaderCreateInfo` counts, sampler names,
  and values' offsets, sizes and `[Default]`s. Disk cache in
  `user://shader_cache_slang/` (SPIR-V + reflection), keyed by the source,
  entry and flags, every library module, and the Slangc.NET version. Shipped
  effects compile at start (`WarmShipped`); a level's slots when it appears
  (`WarmLevelShaders`). Blend variants are pipeline state, made on first use.
- **Slang only starts on a cache miss.** Its session costs about 200 ms and
  200 MB and never gives the memory back, so there is one for the process
  (`SlangNative`), made on first use, every call taking a turn on it; a second
  one to compile in parallel isn't worth its memory. The host's own shaders
  (particles, screen mips) go through the cache too, so a launch with a warm
  cache never makes it. The shader editor starts it in the background as it
  opens (`SlangShaders.Prepare`).
- **The library is precompiled** to `.slang-module` files (`SlangModules`),
  one folder per library state and set of defines under
  `shader_cache_slang/modules/`. Each module is also kept in `modules/store/`
  by what it was built from (its text, its imports, and the defines only if
  its own text reads one), so an edit rebuilds the edited module and what
  imports it, and another set of global bindings rebuilds only the modules
  that declare globals (and `xanadu.stage` for the frame and depth slots). The
  folder holds the prepared sources too (globals bound), so a module Slang
  refuses still compiles from there and reports its error.
- **Shipped effects** are `.slang` files under `Assets/Shaders` (note
  background, flare, rainbow, frosted glass, text gradient, colour picker,
  editor waveform), written exactly as level shader slots are (below) and
  compiled through the same wrapper, so the lab, the shader tool and the game
  read one form.
- **Level shader slots** hold an effect body: global `uniform`s (tagged
  `[Instance]` when a group's item-shader track may key them, with
  `[Default(...)]`s) and a `float4 shade(…)` taking any of `Canvas` and
  `ItemInput` (`ShadeSignature`, Core, reads which; the wrapper passes
  `i.canvas` and/or `i`). `SlangShaders.Wrap` puts
  it in a namespace, hoists its imports, adds `import xanadu.stage` and an
  entry point, and uses `#line` so errors land on the author's lines.
  `-fvk-bind-globals 1 3` makes the globals uniform buffer 1.
  `ShaderChains` (Core) rewrites chains of the library's in-place methods,
  which Slang can't compile, into their copying twins: `c.move(a).rotate(b);`
  → `c = c.moved(a).rotated(b);`, `Box(s).move(o)` → `Box(s).moved(o)`. The
  pairs are read from the library (`[mutating]` methods with a -d/-ed twin)
  at start, like `ShaderConstants`.
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
  | `xanadu.math` | `PI`, `TAU`, GLSL-style `mod`, `rotate`, `random(seed)` / `random2` / `random3` (0..1, PCG hashes of the seed's bits, so large or fractional seeds stay random) |
  | `xanadu.shape` | `IShape`, the shapes (`Circle`, `Line`, `Box`, …), placement (`rotate`/`rotated`, `move`/`moved`, `scale`/`scaled`, `anchor`/`anchored`), combining (`or`, `and`, `minus`, `melted`, `meltedAnd`, `meltedMinus`, `repeated`, `hollowed`), drawing (`filled`, `stroked`, `haloed`, `outlined` inside the edge, on a `Box` by `side.` flags), `IMeasurable` (shapes and `Measured` alike: drawing, painting and joins in both directions, a `Measured` on either side giving a `Measured`; `grown`/`shrunk` on a `Measured`), and `sdf.` for distances built by hand |
  | `xanadu.colour` | the `colour` type (an alias for `float4`, straight alpha, so `.rgb` and the maths work as ever; `uniform colour tint;` is fine), `top.over(bottom)` and `c.withAlpha(a)` on any colour, and under `colours.`: `hsv`, `hue` (both give an opaque `colour`) and the X11/web colours as `colours.gold`, `colours.midnightBlue`, … |
  | `xanadu.ease` | `ease.inOutSine(t)` and the other 30 Penner easings, `ease.by(n, t)` |
  | `xanadu.blend` | `blend.Blend` (a `blend.Mode` for the colours, a `blend.Alpha` for the alpha) with presets `blend.screen` and 11 more, `blend.behind`, `blend.erase`, and `.behind` / `.erasing` / `.over`; `blend.composite`; the bare colour formulas under `blend.colours.` |
  | `xanadu.pattern` | the shapes `Grid`, `Columns`, `Rows`, `Stripes`, `Checker`; `pattern.tile` (→ `Tile`) |
  | `xanadu.gradient` | `IGradient` and `Linear`, `Reflected`, `Radial`, `Focal`, `Conical`, `Diamond`, `Square`, `Spiral`; `repeated` / `mirrored` spreads; `gradient.stops`, `gradient.corners` |
  | `xanadu.curve` | `Bezier(a, b, c, d)` with `.at(t)` and `.tangent(t)` |
  | `xanadu.paint` | `c.paint(shape or coverage, colour, blend)` / `c.painted(…)`, `c.erase(shape or coverage)` / `c.erased(…)`, `c.paint(shape, Look(fill, outline, width))`: a picture in layers |
  | `xanadu` | re-exports all of the above, plus the stage |

  ```slang
  let petals = Box(float2(56), 10).repeated(8, 160).rotated(time);
  colour c = lerp(colours.midnightBlue, colours.gold, petals.filled());
  ```

  Slang has no named arguments; the shader editor shows parameter names as
  inlay hints instead.

  **`Docs/shaders.md` is the full guide to the library**: every call, the
  coordinates, painting, pitfalls.

- **Named defaults:** `[Default(colours.gold)]` or `[Default(PI, 2)]` work.
  Slang attributes take only literals, so `ShaderConstants` (Core, loaded by
  the host from the library's `public static const`s) swaps names for their
  numbers in `SlangShaders.Wrap`, and `ShaderSourceUniforms` reads them the
  same way. Slang's own messages then quote the numbers.
- **Style:** `Docs/shader-style.md` collects the house style as numbered
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
- Every Xif, rig art included, draws through `XifDrawer` (Model/Animation):
  triangles into an `IRectCanvas` each frame, back to front by layer, a node
  with a common shader in a run of its own (`SelectCommonShader`).
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
- **Terminal (Ctrl+`):** every host exe has it. The drop-down and its line
  editing are UI's (`UI/Terminal`: `TerminalCommands` parses a line once into
  typed arguments, `TerminalSystem` draws and edits it, last in `UiRoot`).
  The host owns the registry (`HostApp.Terminal`) and adds `vsync`, `maxfps`,
  `fps`, `window`, `screenshot` and `quit` (`HostTerminal`); `TabbedToolApp`
  adds `tab`; any exe adds more. While it's down it has the keyboard: it takes
  focus from fields and editors, and exes gate their shortcuts on
  `TerminalOpen`. `XANADU_TERMINAL="vsync off;fps"` runs lines at launch and
  leaves it down. Commands act on the running window; the settings screen still
  saves choices. Plain ` stays the game's docs.
- The window isn't resizable, and the pointer scale is fixed from the start
  size.
- Synthetic input for manual checks: `SDL_VIDEODRIVER=x11` plus `xdotool`
  (closing the window that way ends with a harmless X `BadWindow`).

### 5.9 Tools

- **Animator** (`Tools/Animator`): `AnimatorTool` derives from `HostApp`.
  The exe is wiring only (tool, system group, scripted pointer, metronome);
  the animator itself lives in the role projects under an `Animator` owner
  folder: `Model/Animator` (session components, selection, modes, timeline
  model, handle geometry), `Logic/Animator` (commands, file access, input and
  playback systems), `Drawers/Animator` (preview, timeline, grid, overlays)
  and `Screens/Animator` (layout, panels, inspectors, menus). It does not
  reference the game's wiring. Paths are relative to the game folder here
  too; its content lives under `Tools/Animator/...`
  (`Logic/Animator/AnimatorPaths.cs`). Flags: `--shot`,
  `--shot-frames`, `--shot-seconds`, `--flow`, `--clip` and more. The
  metronome writes its ticks once to `user://animator/*.wav` and plays them
  through miniaudio.
- **UI Showcase** (`UI/Showcase`): a `HostApp`.
- **Visual tests** (`Tests/VisualTests`): see its README. Every test starts
  with the test user folder's recovery files cleared.
- **Tabbed tools** (`HostCore/Tools/TabbedToolApp.cs`): the shell Labs and the
  dev tools share. A strip of tabs, the open tab's notes and controls in an ECS
  side panel (`IToolTab`), and its drawing on a stage (`IToolStage`), built
  when the tab opens and freed when another does. The shell is the one place
  that reads the pointer for its tabs: each stage gets it in a `ToolFrame`.
  A tool derives from `TabbedToolApp`, lists its tabs and makes a tab's stage.
- **Dev tools** (`Tools/DevTools`): a `TabbedToolApp` with two tabs, ATLAS and
  AUTOPSY (`./run.sh devtools [atlas|autopsy] [flags]`). Both are still their
  own canvases laid out for 1920×1080 (`CanvasToolStage`): the stage scales
  them into the tab's area and maps the pointer back. Atlas reads the builds
  once, the first time its tab opens.
- **Xanadu Labs** (`Tools/Labs`): a `TabbedToolApp`, one tab per experiment.
  Experiments implement `ILabExperiment` (an `IToolTab` that makes its stage
  from a `LabContext`); stages that render on their own get
  `IToolStage.Render` (called from `HostApp.RenderOffscreen`) and show the
  result through `LabContext.Show`. Panel rows live in `LabPanel`.
  - **Kept state** (lab only): an effect body with `void update(inout State s,
    float dt)` gets a 1 KB storage buffer per material. `CanvasRenderer`
    runs `update` once a frame as a one-thread compute pass
    (`SlangShaders.WrapUpdate`), and the fragment reads it as `state`, its
    storage buffer placed after the globals. In `update` the globals are
    compute's read-only buffers from 0. `CanvasMaterial.KeepsState`
    gates it; level materials leave it off, so `update` is a compile error
    there.
  - **Shader editor** (`ShaderCodeEditor`; the start of the in-game shader
    editor): lives in the shader tool (`./run.sh shaders`, where NEW makes an
    effect from a template under `Assets/Shaders/` and opens it) and the lab's
    Spawner tab. Compiled 0.3 s after typing stops. Code left with
    a status bar under it, a 16:9 preview top right (the playfield's shape;
    the pointer counts only over it), problems and a key guide under that.
    Linting is Slang's own errors: the line tinted, the word underlined, the
    message on a row under the line, listed under the preview (a click jumps).
    Under the problems, a slider per declared value and channel
    (`EditorTuning`), except the ones the preview fills; ranges are guessed as the
    F2 tuner does (`Core/Rendering/UniformRanges`), tuning survives recompiles.
    Inlay hints show library parameter names (`Box(size: …, radius: …)`)
    without them being in the text; they come from the library's `public`
    signatures, matched by name and argument count (no hint when ambiguous).
    Completion after a dot (`SlangCompletions`, `CompletionPopup`): a
    namespace's members (`colours.` with swatches), a struct's fields and
    methods (`canvas.`), the shape methods every `IShape` gets, enum values
    (`blend.Mode.`); a name's type comes from a light scan of the code
    (`Canvas canvas`, `ItemInput i`, `let b = Box(…)`). Up/Down, Tab/Enter,
    Esc, click. No calls in the chain, no inference past that. While typing
    a word it offers the code's own names first (uniforms, locals, functions
    and the preview's provided values), then Slang's types, keywords and common
    intrinsics and the library's top-level names (shapes, `colour`, `rotate`,
    namespaces, which take their dot). It stays shut where a new name is being
    given (`float4 |`, `let |`) and once the word is already a whole name, so
    Enter there is still a new line.
    Besides Slang's own problems it shows its own warnings
    (`SlangLint`, the checked rules in `Docs/shader-style.md`: so far XS002
    whole-number division, XS003 a local read before it has a value and XS004
    mouse given to anchor); warnings tint amber and never stale
    the preview.
    Built in layers. **Text editor** (UI, ECS): a `TextEditor` component
    (`UI/Ecs/Ui/TextEditorComponents.cs`) holding a `TextBuffer` and its
    `TextHistory` (`UI/Editing/`); `TextEditorInputSystem` edits the focused
    one (`TextEditorFocused`: a press inside focuses, outside unfocuses) and
    `TextEditorDrawSystem` draws them all, in `UiRoot`'s input and draw phases. Typing
    and the clipboard come through `HostPlatform.TextEntry` (the host's
    `HostTextInput`, claimed while an editor has focus). It knows nothing
    of code: its owner's `ITextEditorHost` gives colour runs, inlays, line
    marks with notes and boxed cells, and takes keys, typing and presses
    first. Shift+moves, drag, Shift+click and double-click select;
    Ctrl+A/C/X/V; Ctrl+Z and Ctrl+Y / Ctrl+Shift+Z undo and redo on the
    shared `UndoHistory`: a run of plain typing is one step until the caret
    moves or typing pauses for a second; Enter, paste, cut, anything the owner
    does or typing over a selection is a step of its own. No horizontal scroll
    yet. **Shader** (`Tools/Labs/Editor/`): `SlangEditorHost` is the owner
    (tokens, bracket colours, hints, problems, the completion list's keys,
    Enter after `{` and `}` indenting); `EditorChrome`, `EditorTuning`, the
    popups and the previews (`IEditorPreview`) stay in Labs. The language
    smarts it drives (completion, hints, lint, rename, problems, syntax,
    brackets) live in the `Slang` module. A code editor layer between the
    two (any language) is next.
  - **Particles:** bursts every library effect once a second.
  - **Blend:** two quads with the whole fixed-function blend open
    (`CanvasMaterial.Custom`); the renderer sets the blend constant per draw.
  - **Depth + stencil:** quads into the lab's own colour and depth-stencil
    target, each writing depth per pixel through `SV_Depth`; depth and stencil
    state all open.
  - **Transparency:** order-dependent against order-independent transparency.
  - **Indirect draw:** up to a million GPU-only shapes.
  - **Spawner:** the shader editor with a different preview: `count` and
    `Thing spawn(uint i)` (optionally `shade(Thing t, Canvas canvas)`) become a
    compute pass that spawns, culls and packs, plus one indirect draw.
  - **Pointer history:** the pointer's path as a ring of keys on the GPU.
  - **RTS:** an RTS that lives on the GPU.
  - **Radiance cascades:** 2D GI with wall bounce: pre-averaged, direction-first cascades, the bilinear fix on cascades 0–1 (MERGE picks plain, low or all) and a jump flood capped at 64 px; ~2 ms GPU on its 1208×990 stage (was 12.6 ms with the fix everywhere).
  - `dotnet run --project Tools/Labs -- --tab=<n>` opens a tab.

### 5.10 Perf tooling

- **GPU times** (`HostCore/Native/gpu_times_layer.c`, manifest
  `xanadu_gpu_times.json`, reader `HostCore/Rendering/GpuTimes.cs`): a Vulkan
  layer that puts timestamps around every SDL debug group and command buffer.
  HostCore's csproj builds it with gcc against headers from
  `External/fetch-vulkan-headers.sh` (gitignored); without them it's skipped.
  On by default when built; `XANADU_GPU_TIMES=0` turns it off, `=1` also prints
  a tree each second. Name GPU work with `commands.Scope("name")` (§5.3).
- **Autopsy of a Frame** (`HostCore/Autopsy`, the AUTOPSY tab of `Tools/DevTools`):
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
  - No window: `dotnet run --project Tools/DevTools -- autopsy --dump[=app]`
    (text trees) and `autopsy --dump=app --under=<method>`.
  - `./run.sh devtools autopsy game|editor|labs|animator` runs that exe with
    profiling, then opens the tool on METHODS.
- **F3 overlay:** per-system times, and GPU memory (this app, budget, card) from
  `VulkanMemoryBudget`.
- **Query cache** (`UI/Ecs/QueryCache.cs`): `store.Cached<T>()` and
  `store.ToggleOn<TTag>(none, hidden)`; see `Docs/ecs/README.md`.

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
- [x] MSAA: 4× (or the most the GPU allows) on the main frame only, resolved at every pass end so screen copies read the resolve; offscreen roots and buffered groups stay single-sampled. Pipelines are cached per sample count. Still open: how it meets sampled depth (§4).
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
- **Tests:** `dotnet test Tests/Xanadu.Tests.csproj` (~2300),
  `dotnet test UI/Tests` and `dotnet test Slang/Tests`. Check for "Test Run
  Aborted"; `--blame-crash` finds the culprit. Known failures (nine, checked
  2026-11-14): `EveryButtonActionHasAKey`, two `EditorPlayfieldDrawSystemTests`,
  `EditorToolSectionSystemTests.BlocksShowTheirSizeAndShape`,
  `XmfBinaryTests.ALevelWithoutItemGeometryIsWrittenAsBefore`, LayerPass,
  `PlayKeyTrailSystemTests.PressedKeyLightsAndReleasedKeyDecays`,
  `SceneSystemTests.SettingsKeepsBackgroundBelowContent` and the key-binding
  button test. Don't count them as regressions or fix them unasked.
- **Visual:** one screenshot from `--shot` (or a visual test's
  `--visual-test-shot`), saved outside the repo (the session's scratchpad or a
  temp folder). Never commit images outside `Assets/`.
