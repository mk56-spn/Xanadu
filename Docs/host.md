# The host: Xanadu on MoonWorks

Xanadu runs on [MoonWorks](https://github.com/MoonsideGames/MoonWorks), a small
C# framework over SDL3 (SDL_GPU on Vulkan / D3D12 / Metal). Every exe is a
plain `dotnet run`; there is no engine. This doc holds the decisions behind the
host, how it is built, the tooling around it, and what is left for later.

---

## 1. Why MoonWorks

- Plain .NET: every tool is a normal exe, with no engine project, asset root
  or symlinks per tool.
- The GPU directly: 2D depth buffers, any blend state, compute, storage
  buffers, indirect draws, and control over present mode and frame latency.

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
| P8 | **Maths types:** vectors are `System.Numerics` (what MoonWorks and Friflo use), aliased project-wide by `Core/Maths/Maths.props`; Godot's vector methods live on them as C# 14 extension members (`Core/Maths/VectorExtensions.cs`). Scalar maths is .NET's (`Math`, `MathF`, `float.*`). `Color` (float, so HDR and fades stay exact; MoonWorks' byte `Color` only at the GPU edge), `Rect2`, `Transform2D` and a slim `Mathf` of Godot-only helpers are our own types in `Xanadu.Maths`, copied from Godot 4.4 (MIT) and trimmed to what is used. Canvas ids are `Handle`. |
| P10 | **No mesh shaders** (SDL_GPU doesn't expose them). GPU-made geometry = compute into a storage buffer + vertex pulling + indirect draws. |
| P11 | **Shaders are exposed to players as slots, not whole shaders** (§3). |

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
storage buffers 1 on; no other textures), and the hardware blend modes. No library modifiers yet.
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
  It copies the moonlibs and the timing layer next to every exe.
- **`Rendering`** (library, level 3: Core, Slang, UI): drawing on the GPU. The
  canvas tree and `CanvasRenderer`, which runs the frame as passes over one
  `CanvasFrame` (`Frame/`: collect and build steps; `Passes/`: expand, grids,
  stacks, trace, draw; `Gpu/`: growable buffers, targets, primitive pipelines,
  the primitive pool; `Kept/`: what stays between frames, as entities in one
  store: materials, kept meshes, resident trees, globals). Also compute
  operations, particles, text (`Text/`) and the host's shaders (`Shaders/*`,
  copied next to every exe).
- **The game** is spread over the role projects (`Model`, `Serialisation`,
  `Storage`, `Drawers`, `Logic`, `Screens`). `Game/Xanadu.csproj` is the
  wiring (`Game/Wiring`: the system root, scenes, groups) and **`Game/Host`**
  is its exe (`HostGame`: the world, the add-song dialog, the editor overlay,
  level shader warm-up), with `HostPassSurfaces` and `DemoLevel`.
- **Tools:** `Tools/Animator`, `Tools/Shaders`, `UI/Showcase`, `Tools/Labs`,
  `Tools/DevTools` and `Tests/VisualTests` are `HostApp`s. Labs and the dev
  tools share the tabbed tool shell.
- **Who owns which systems:** the host owns the UI, `UiRoot` (`UI/Systems/Ui`),
  on `HostApp.Entities`. Each frame it runs UI input, then the exe's own root,
  then layout, drawing and the F1/F3/F4 overlays. An exe owns its root (the
  game's `XanaduSystemRoot`, the animator's) on the same store and hands it over
  with `UseSystems`; it never boots UI itself. F3 and the frame autopsy show the
  exe's root nested under the UI's step for it. Input the exe feeds its systems
  goes in `BeforeSystems`. Only the visual-test harness opts out (`HostUi.None`):
  it makes a store and a `UiRoot` per scene.
- **Launcher:** `./run.sh <target> [args]`, one script for every exe: `game`,
  `editor`, `labs`, `animator`, `shaders`, `showcase`, `visual-tests`,
  `devtools`. With no target it asks which in the terminal. `--release` (or
  `-r`) before the target builds and runs it in Release: Debug isn't
  optimised by the JIT, so read CPU timings (F3, Autopsy) from a Release run.
- **Game flags:** `--scene=<name>` (any `SceneKind`), `--demo` (a level made in
  memory with a field shader, a buffered group and a self-contained layer),
  `--play[=<name or file>]`, `--editor`, `--shot=<png>` (`--shot-frame=<n>`,
  default 60), `--resolution=WxH`.
- `GamePaths.RootMarker` (`Xanadu.sln`) marks the game folder, which every
  relative path is under (`Assets/Shaders/Game/flare`); `user://` is `~/.local/share/xanadu` (`$XDG_DATA_HOME/xanadu`; `%APPDATA%\xanadu` on Windows).

### 5.2 Seams the game draws through

Game, UI and Core code never touch MoonWorks. They go through static seams the
host fills; tests leave the headless defaults.

- **`CanvasServer`** (`UI/Systems/Canvas/CanvasServer.cs`): a retained canvas
  item API (create, free, parent, z, draw index, transform, modulate, clip,
  material, material runs, mounts, draw commands). Default
  `HeadlessCanvasServer`.
- **`IRectCanvas`**: the drawing contract every draw system uses (shapes, text,
  glows, shader selection and runs), keyed by a canvas `Handle`.
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

### 5.3 Canvas tree and renderer

- **Recordings.** A canvas item (`CanvasTree`, `Rendering`) records
  *primitives* (`Primitive.cs`), not triangles: boxes (rect, rounded, stroked,
  two-colour ramp), discs and rings, textured quads (one per glyph, one per
  sprite) and free triangles (polygons, mitred polylines, meshes). Lines are
  boxes along them. Nothing is tessellated on the CPU unless an effect needs a
  mesh.
- **Mounts, not mirrors.** `ICanvasServer.Mount(item, ICanvasMount)` hangs a
  source under an item; each frame the renderer asks it what to draw there,
  item by item (`ICanvasCollector.Item`) or as a resident tree (below). A rig's
  particle streams (`RigParticleStreams`) list items; the UI keeps a tree. UI
  entities keep only their own recording (`VisualItem`); there are no
  parenting, transform, visibility or opacity sync systems. Game groups and
  tools still hang items under the root with a z.
- **Resident UI.** The UI's drawn entities live on the GPU between frames
  (`IResidentTree`, `Rendering/ResidentTree.cs`): a slot per entity
  holding its node (parent slot, local transform, opacity, the clip it gives
  its children) and its item's own transform, tint, clip and pool range.
  - *The UI side* (`UiResidentSync`): **structure** (which entities draw,
    parents, z, order) changes only through Friflo events (tags, components
    added or removed, children, entities made or deleted, with the drawn
    count as a net) and triggers a **restructure**: one walk over layers and
    the tree, working out `LocalZIndex`, layer and loose entities, visibility
    (`Enabled`, `CanvasHidden`), the order. **Values** are written through
    `ref` by layout and tweens, which raises no event, so each frame a flat
    chunk scan compares positions (and, for posed or tweened entities, the
    whole local place and fade; layer fades; container clips) with what was
    last sent and sends only the differences. A quiet frame walks nothing.
  - *The host side*: canvas items tell their tree when they change
    (`CanvasItem.Touch`). The order is cut into **pieces**: plain items in
    segments by z, and **foreign** items (a material, shaded runs, canvas
    children, mounts, particles) that the CPU places itself from its
    ancestors' nodes, at its walk position, so sorting the frame by z draws
    as the walk did. Segments join the frame as one entry each; their runs
    and each entry's place are kept and worked out again only when a size,
    texture set or the order moves.
  - *The GPU side* is the canvas stack (`Assets/Shaders/Engine/canvas`,
    run by the render graph, Docs/render-graph.md "The resident UI on the
    graph"): every tree is a range of its shared lists; `patchNodes` writes
    the changed slots; `resolve` walks each drawn slot's ancestors
    (transform, fade, clips, the mount's place) and writes its instance after
    the CPU's; `expand` reads a tree's run slots through its instance flag.
  - On the 20,000-cell CJK lab (2026-11-24): draw 18.7 ms → 0.5 ms still,
    collect and build 0.13 ms; churn costs what changes.
- **Pool.** Every recording's primitives live on the GPU in its own space, in a
  range that stays while it fits (`Resident`). A recording goes up again only
  when its content changes: a hash of the primitives means a drawer that
  clears and redraws the same picture every frame sends nothing. Settled
  screens send 0 primitives a frame. A full pool starts a bigger empty one and
  everything drawn is sent once. The pool is the canvas stack's `pool` list,
  written in ranges (`HostLists.Write`).
- **Instances and the expand stage.** Per frame the CPU walks the canvas tree, puts
  the entries in order (`SortByZ`: a counting sort on z, ties in walk order)
  and sends one `GpuInstance` per drawn range: transform, tint, clip,
  depth base, its place in the output and its texture slots.
  The canvas stack's `expand` pass (64 threads an instance) writes each
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
- **Drawing.** The canvas stack's two draws (`Assets/Shaders/Engine/canvas/draws.slang`:
  `prepass` and `ordered`, exits drawn over a range of copies) pull records by copy
  (six vertices a record) and shape them by signed distance (`xanadu.shape`), so edges
  are smooth at any turn or scale; up to eight textures a draw (glyph pages,
  images). The editor is two draw calls.
- **Material runs.** `SelectCommonShader` / `SetShaderValue` / `NewRun` on a
  canvas start a run in the item (`ICanvasServer.SelectRun`); the renderer cuts
  the item into plain ranges (instances) and shaded ranges (effect steps), in
  order. An item with a material (`SetMaterial`) is shaded whole. Shaded
  draws keep the effect path: primitives cut into `CanvasVertex` triangles
  (`Primitive.Tessellate`), values packed per run (`[Default]`s, material,
  run). `SelectShader(0)` is plain.
  - Every effect range's vertices are kept on the GPU (`KeptMesh`) and cut
    again only when its primitives, place or tint change.
  - A deform's `[Grid]` boxes are cut on the GPU instead (`grid_cut.slang`, a
    compute operation, §5.6): a job per box, every frame, into one vertex
    buffer, so a grid resized every frame costs the CPU nothing. Triangles and
    discs on a grid still cut on the CPU.
- **Screen reads:** before the first effect in a pass whose SPIR-V samples the
  screen (`CompiledShader.ReadsScreen`), the pass ends, the target is blitted
  into the screen texture with mips (`ScreenMips`), and the pass resumes
  (colour and depth loaded); later reads in the pass share that copy.
- **MSAA:** 4× (or the most the GPU allows) on the main frame only, resolved
  at every pass end so screen copies read the resolve; offscreen roots stay
  single-sampled. Pipelines are cached per sample count.
- **Offscreen roots** (`AddOffscreen`, the shader tool's stills) draw into their
  own target, single-sampled, before the frame.
- **GPU scopes:** `using (commands.Scope("name"))` names GPU work for the
  timing layer (§5.10). Open and close it on the same side of a render pass.

### 5.4 Text

- **FreeType + HarfBuzz** (`FreeTypeSharp`, `HarfBuzzSharp`; MIT, own natives),
  host only (`Rendering/Text`). Regular text is Source Sans 3 at weight 400,
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
- Every page is kept twice on the GPU: its own texture (what effects and labs
  sample) and a layer of one array (`TextAtlas.Layers`, regrown by doubling).
  The plain primitive pipeline reads glyphs from the array (fragment sampler 8;
  a texture slot with bit `0x8000` is a layer), so glyphs take no run slot and
  a glyph moving page never moves a resident layout.
- Outline: FreeType's stroker, outer border, radius = outline size / 2. Glow:
  `GlowBlur` on the CPU, per glyph, cached in the atlas (where halos overlap
  between letters the glow is slightly fainter than a whole-string blur).
  `HostShapeGlow` does the same for polygon and polyline glows.
- `GlyphStrips` builds a `GlyphAtlas` texture with mipmaps for materials
  (note glyphs, particles).
- **Fallback:** characters the main faces lack go, in order, to the bundled
  Noto Sans Symbols 2, Noto Sans Symbols and Noto Emoji (one colour), then a
  system CJK font (`HostText.SystemCjkPaths`, Linux then Windows), then
  JetBrains Mono. No CJK font is bundled, so a machine without one draws CJK
  as missing boxes.

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
- **Shipped effects** are shader stacks under `Assets/Shaders` (note
  background, flare, rainbow, frosted glass, text gradient, colour picker,
  editor waveform): a folder each, one file per brick (`state`, `pool`,
  `vertex`, `fragment`.slang; `Slang/ShaderStack.cs`). `ShaderStack.Compose`
  joins the bricks in pipeline order, each under its own `#line` label, into
  one body written exactly as level shader slots are (below) and compiled
  through the same wrapper, so the lab, the shader tool and the game read one
  form, and Slang's errors land on the brick's own lines. A material names the
  folder (`Assets/Shaders/Game/orb`).
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
- **Shipped library** (`Assets/Shaders/Library`). Shapes are values:
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
- **Host** (`Rendering/CanvasParticles.cs`, `Rendering/Shaders/particles.slang`):
  an item gets a `ParticleField` on its first burst, a ring of 8,192 slots:
  its own range of one particle arena (a full ring cuts off the oldest).
  Written slots upload each frame; every live field is a job of one compute
  operation, so all of them step in a single dispatch. Each is drawn as one
  instanced draw (reading from its range's first slot) right after its item's
  own draws, so z, groups and layers order it like any item. Per-effect
  gravity, ramp and scale curve live in one table of 32 samples each.
- **Compute operations** (`Rendering/Compute`): the model all GPU
  compute is moving to, the engine's first so players' later can be the same.
  A `ComputeOperation<TJob>` is a pipeline plus a job list: jobs submitted in
  a frame run in one dispatch, (groups per job, jobs, 1), the shader taking
  its job from `SV_GroupID.y` (jobs at read-only binding 0). SDL_GPU fixes a
  pass's writable buffers when it begins, so an operation's jobs share one
  `ComputeArena<T>`: a growable buffer of fixed-size ranges, copied over on
  growth so ranges keep their offsets. Users: particles (one arena) and
  `[Grid]` cutting (jobs of different sizes write a frame vertex buffer, the
  run's groups sized to the biggest), and a stack's STEP brick
  (`Rendering/Passes/GraphRunner.cs`), the player-shaped one: the
  brick's `start`/`step` wrapped into the operation's contract
  (`SlangShaders.Pools.cs`), each material its own pool buffer stepped once a
  frame, then its draw instanced by the item count, the vertex and fragment
  entries reading `item` by the instance. Each slot is the item and an
  `alive` flag. A pool born at a rate (`rate`, `bool step`) has two slots more,
  the births' counters, used on alternate frames so the one in use was zeroed
  the frame before; the CPU carries the births owed (`PoolRun.Owed`). Lab only,
  as kept state is. The
  resident UI's passes, screen mips and the labs still dispatch directly.

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

- **GPU validation:** `XANADU_GPU_DEBUG=1` creates the device in SDL_GPU's debug mode, so bad bindings and limits assert with a message instead of failing silently.
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
- **Dev tools** (`Tools/DevTools`): a `TabbedToolApp` with ATLAS (the type
  chart), AUTOPSY (frame captures), ARCH, CLOCK and DEPS
  (`./run.sh devtools [tab] [flags]`). ATLAS and AUTOPSY are still their
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
    editor): lives in the shader tool (`./run.sh shaders`, where NEW makes a
    stack, a shade brick from its template, under `Assets/Shaders/` and
    opens it). It edits a stack: the
    pipeline strip over the code (`BrickStrip`: UPDATE → STEP → DEFORM →
    SHADE → blend) opens a brick, adds a missing one from
    `ShaderStack.Template` and removes the open one, each brick with its
    problem count; the editor composes the bricks for every compile, and each
    problem goes to its brick by label (other bricks' are listed named by
    theirs). Compiled 0.3 s after typing stops. Code left with
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
    yet. **Shader** (`HostCore/Tools/ShaderEditing/`): `SlangEditorHost` is the owner
    (tokens, bracket colours, hints, problems, the completion list's keys,
    Enter after `{` and `}` indenting); `EditorChrome`, `EditorTuning`, the
    popups and the previews (`IEditorPreview`) sit beside it. The language
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
  - **Indirect draw** and **Spawner** are gone (2026-11-26): a stack does both, a `[Pass]` culling into a `Gathered`
    list and a `[Draw]` counted by it (`Lab/copies`, `Lab/culled_meshes`). The Labs shader examples went too: plain
    shades, which any stack is.
  - **Pointer history:** the pointer's path as a ring of keys on the GPU.
  - **RTS:** an RTS that lives on the GPU.
  - **BVH march** (raymarcher four; the stacks in `Assets/Shaders/Lab/raymarch_*` are one to three) is no longer a Labs tab: it is the stack `Assets/Shaders/Lab/bvh_march` in the shader tool (moved 2026-11-24). Up to 262,144 melting blobs. BUILD animates them, radix sorts their Morton codes, builds a Karras LBVH and refits it with atomics, and fills a sun-space grid (count, scan, fill). TRACE (compute, 8×8 tiles) lists what each tile sees (thread 0 opens the BVH into up to 64 subtrees, every thread walks one, inside the tile's side planes and nearer than the floor), bitonic sorts the list in groupshared memory, and each ray gathers the 8 blobs it enters first, bounded by the first plain blob it hits, and marches only those; floor and pillars are analytic. Soft shadows read the sun grid or walk the BVH. DEFORM/SHADE draw 6,144 `[Translucent]` sparks after it, depth-tested against the march. The lab's timings (~3.4 ms GPU at 16k blobs, ~9.4 ms at 262k, RTX 4060 laptop, vsync off) haven't been taken again on the stack.
  - **Radiance cascades:** 2D GI with wall bounce: pre-averaged, direction-first cascades, the bilinear fix on cascades 0–1 (MERGE picks plain, low or all) and a jump flood capped at 64 px; ~2 ms GPU on its 1208×990 stage.
  - `dotnet run --project Tools/Labs -- --tab=<n>` opens a tab.

### 5.10 Perf tooling

- **GPU times** (`HostCore/Native/gpu_times_layer.c`, manifest
  `xanadu_gpu_times.json`, reader `Rendering/GpuTimes.cs`): a Vulkan
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
  - Memory, per frame (`AutopsyMemory`, side `Memory`): KB every thread
    allocated, the managed heap, collections by generation and GC pause, from
    the runtime's counters (`UI/Systems/Debug/GcReading.cs`); process RSS and
    GPU memory read twice a second and carried between. The last 64 frames
    that collected are kept too.
  - Views: FRAME (recent frames, mean / latest / worst, CPU and GPU split bars
    to drill into), METHODS and MEMORY (heap, process and GPU lanes each on
    its own range, allocations per frame with GC ticks: cyan gen0, amber
    gen1, red gen2; GC FRAMES steps through the collecting frames).
    `--memory` opens on it; `--dump` prints a MEMORY block and marks hitches
    that collected.
  - No window: `dotnet run --project Tools/DevTools -- autopsy --dump[=app]`
    (text trees) and `autopsy --dump=app --under=<method>`.
  - `./run.sh devtools autopsy game|editor|labs|animator` runs that exe with
    profiling, then opens the tool on METHODS.
- **F3 overlay:** per-system times; MEMORY (heap, process RSS, every
  thread's KB per frame; "systems alloc KB" under FRAME is only what Friflo
  systems allocated), GC since launch by generation with the pause share of
  the last 0.1 s, and GPU memory (this app, budget, card) from
  `VulkanMemoryBudget`.
- **Query cache** (`UI/Ecs/QueryCache.cs`): `store.Cached<T>()` and
  `store.ToggleOn<TTag>(none, hidden)`; see `Docs/ecs/README.md`.
- **Collect split** (F3, under "collect items"): the walk is timed exclusively
  (one clock, each stretch charged to the part running, so nesting never counts
  twice) into canvas items, the UI's restructure and value scan
  (`ICanvasCollector.Begin` with a `MountStep`) and other mounts; then the sort.

---

## 6. Later

### The device layer: the Vulkan SDL_GPU leaves out (built 2026-11-26)

**Why.** SDL_GPU exposes only what every backend (Metal, D3D12, consoles) can do: upstream adds a missing GPU feature
only if all backends can (libsdl-org/SDL#11148, bindless closed as not planned). We are Vulkan only, so that policy
costs us long-standard Vulkan for nothing. A fork of SDL was planned first; the user chose a layer under SDL instead
(2026-11-26): no SDL build to keep, and SDL still tracks everything it binds.

**What it does** (`HostCore/Native/device_layer.c`, read by `Rendering/DeviceLayer.cs`, built like the GPU timing layer;
both go in the chain through `VulkanLayerChain`, since .NET doesn't see what libc's `setenv` wrote):

- `vkCreateInstance`: API version raised to the loader's, up to 1.4 (SDL asks for 1.0).
- `vkCreateDevice`: SDL's `pEnabledFeatures` becomes a `Features2` chain with Vulkan 1.1–1.4 structs holding every
  feature the device reports, bar robustness, sparse memory and capture/replay; plus these extensions with all their
  features: atomic float (1 and 2), shader clock, image atomic int64, compute shader derivatives, mesh shader,
  device-generated commands. `XANADU_DEVICE_LAYER=1` prints what came on (RTX 4060: all but atomic float 2).
- Buffer addresses: storage buffers get `SHADER_DEVICE_ADDRESS`, every allocation `DEVICE_ADDRESS`.
  `DeviceLayer.Address(buffer)` reads SDL 3.2's `VulkanBufferContainer` → active `VulkanBuffer` → `VkBuffer`, checks it
  against the buffers the layer saw made (0, so a throw, for anything else), and returns `vkGetBufferDeviceAddress`. Ask
  each frame: SDL swaps the `VkBuffer` when it cycles or defragments.
- Sync: SDL can't see what a shader reaches through a pointer, so once any address is handed out the layer puts a
  memory barrier (shader and transfer writes → shader, indirect and transfer reads) before every dispatch, render pass
  and buffer copy. `xdv:sync off` / `xdv:sync on` labels switch it from that point in recording.
- Commands as debug labels: `vkCmdInsertDebugUtilsLabelEXT` with a name starting `xdv:` goes to the layer, never the
  driver (`DeviceLayer.Barrier`, `DeviceLayer.AutoSync`). SDL forwards labels without debug mode.

**Checked:** song select draws as before with both layers in; Slang (2026.17) compiles `Ptr<T>` lists with 64-bit and
float atomics through them.

**Next, in order:**
1. Graph lists as pointers: `List<T>` becomes `Ptr<T>` under a define, its addresses pushed in one uniform block, so a
   pass binds no lists at all (ends the 8 storage buffers a pass cap the chess search hit). Open: a struct's layout
   through a pointer is natural (`float3` 12 bytes) but std430 in a bound buffer, so lists move to one layout first
   (scalar for both); coherence through pointers (`Volatile` on loads and stores in `SpirvCoherence`); which lists a
   pass uses is found by the bound compile, as now.
2. Device-generated commands with indirect execution sets, recorded by the layer from an `xdv:` label (it reads the
   `VkPipeline` out of SDL's compute pipeline as it reads buffers): the GPU writes which pass runs and how much. Test
   case: the chess search, each workgroup picking its next operation.
3. Later candidates: async compute queue, mesh/task shaders (xanadu.mesh), ray queries (SDF/mesh scenes, radiance).
4. Windows: build the layer there too (the graph will depend on it).

**Measuring the chess bot** (Labs CHESS, tab 16): `ChessMatch` plays it against a UCI engine when
`XANADU_CHESS_MATCH="engine path|elo|games|results file"` is set; run with `XANADU_SHOW_SHOTS=1 --shot=<p>
--shot-frame=100000000` so the window shows. Stockfish 19 is not in the repo: `stockfish-linux-x86-64-universal.tar.gz`
from github.com/official-stockfish/Stockfish/releases (sf_19). Results so far: 13-6-1 at UCI_Elo 1500 (fixed 4 plies),
9-9-2 at 2000 after PeSTO + iterative deepening (0.5 s) + shared transposition table. Bot ideas not done: root moves
sharing their best score, repetition / 50-move detection, recording the search pass many times a frame.


No hurry; none of this is parity.

- [ ] Shader slot model (§3): vertex modifiers, fragment chains (the level
  format must hold more than one effect per slot), the blend dropdown with
  exotic modes, a pipeline cache keyed by (vertex, chain, blend, state).
- [ ] Per-pixel sprite depth maps, and effects on sampled depth (fog,
  outlines, soft particles). Open: how MSAA meets sampled depth (§4).
- [ ] Photoshop-style blend modes via group composite.
- [ ] 2D lighting: point and directional lights, normal maps, 1D shadow maps
  with PCF, light masks. Later: SDF or radiance-cascade GI (the radiance lab's
  next step is 2.5D, depth as occluder height).
- [ ] A bundled CJK font (undecided; a full bundle means an editor character
  picker).
- [ ] Resizable window.
- [ ] Perf, when it matters: GPU group tracks by song time, then tight hulls
  for overdraw hogs.
- [ ] Resident UI, left over (§5.3):
  - Glyph pages once, not twice: `xanadu.stage` learns to sample a layer
    (add-only), and the plain page textures go.
  - Game drawers on the same records, or staying CPU: undecided.
  - Changes the events miss: `Layer.ZIndex` and `Drawable.ZIndex` written
    through `ref` don't restructure; two layers sharing a z draw their loose
    entities under the first only (the old walk drew them under each).
  - The slot lives in `UiResidentSync`'s arrays by entity id; a name, if it
    ever becomes data.
  - A canvas run's CPU cost since the draws became graph exits (2026-11-26):
    record + submit 1.38 → 1.55 ms on the 20,000-cell CJK lab, likely because
    each run binds as any graph draw does (`ImageDraws.Hosted` → `Bind`: frame
    and params pushes, globals, every sampler). Bind the canvas draw's fixed
    state once a job and push only the range and textures per run.

---

## 7. Verification

- **Build:** `dotnet build` on each project touched.
- **Tests:** `dotnet test Tests/Xanadu.Tests.csproj` (~2300),
  `dotnet test UI/Tests` and `dotnet test Slang/Tests`. Check for "Test Run
  Aborted"; `--blame-crash` finds the culprit. No known failures.
- **Visual:** one screenshot from `--shot` (or a visual test's
  `--visual-test-shot`), saved outside the repo (the session's scratchpad or a
  temp folder). Never commit images outside `Assets/`.
