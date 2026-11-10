# Handoff: perf tooling, editor draw cost, next steps (2026-11-10)

Written to start a clean session. Nothing from this work is committed; the working
tree also holds the user's own uncommitted MoonWorks-port changes, so commit only
when asked, and only these files.

## Built this session

**Radiance cascades lab** (`Tools/Labs/Experiments/RadianceLab.cs`,
`Tools/Labs/Shaders/radiance_lab.slang`): 2D GI tab in Labs (tab 4). Plain 2D for
now; the 2.5D variant (depth as occluder height) is the next step there. GPU-bound
at ~22 ms with the bilinear fix on.

**GPU timing layer** (`HostCore/Native/gpu_times_layer.c`, manifest
`xanadu_gpu_times.json`, reader `HostCore/Rendering/GpuTimes.cs`): a Vulkan layer
that puts timestamps around every SDL debug group and command buffer, because
SDL_GPU has no timestamp queries. Built by HostCore's csproj with gcc against
headers from `External/fetch-vulkan-headers.sh` (gitignored). On by default when
built; `XANADU_GPU_TIMES=0` off, `=1` also prints a tree each second. Name GPU work
with `using (commands.Scope("name"))`, opened and closed on the same side of a
render pass. Named so far: frame/offscreen/canvas (upload, particles step, main /
group / offscreen target, screen copy + mips)/blit, and the radiance lab passes.

**Autopsy of a Frame** (`Tools/Autopsy`, `./run-autopsy.sh`, also in `./run.sh`):
- Every host exe records each launch (`HostCore/Autopsy/AutopsyRecorder.cs`): CPU
  frame split into update (every Friflo system, nested), draw (wait for swapchain,
  record + submit) and idle; plus the GPU groups of the same frame (matched by the
  "frame N" group name). Saved on close or crash as `user://autopsy/<app>.xfa`
  (versioned chunked binary, `FrameAutopsy.cs`, v0.2), owned by `AutopsyStore`.
  Keeps the last 1800 frames, the 16 worst CPU and GPU frames, and a mean frame.
  `--shot` runs don't save unless `XANADU_AUTOPSY=1` (so visual tests don't clobber).
- `XANADU_PROFILE=1` adds .NET CPU sampling (`CpuSampler.cs`, EventPipe on the own
  process) to `user://autopsy/<app>.nettrace` (~0.7 MB/s); the tool reads it with
  TraceEvent (`MethodProfile.cs`) for the METHODS view (self/total per method,
  whole launch or one frame; GC counts). Sample bias: time can land on the next
  allocation (`Box`, `.ctor`); trust *total* over *self* for small methods.
- Tool views: FRAME (strip of recent frames, mean/latest/worst picks, CPU and GPU
  split bars you drill into, tables) and METHODS.
- No window: `dotnet run --project Tools/Autopsy -- --dump[=app]` (text trees,
  slowest worst cases) and `--dump=app --under=<method>` (methods beneath it).
- `./run-autopsy.sh game|editor|labs|animator` runs that with profiling, then opens the tool.

**Perf fixes**
- `Core/Maths/Transform2D.cs`: `Tdotx`/`Tdoty` read fields directly (every canvas vertex).
- `HostCore/Text/FontFace.cs`: shaped runs cached per (text, size), cap 4096.
- `UI/Ecs/QueryCache.cs`: `store.Cached<T>()` / `Cached<T1,T2>()` keep one query per
  store; `store.ToggleOn<TTag>(none, hidden)` reads a tagged Toggle. Never add
  filters to a cached query (shared). Used in the editor draw path, `IsPlaying`,
  `EditorButton.All`, GroupTags / AutoAdvance / MusicSnap / ViewportGrid toggles.
  The pattern `store.Query<…>().ToEntityList()` every frame is still all over the
  codebase; convert hot ones as they show up in Autopsy.
- `Systems/Editor/EditorPlayfieldDrawSystem.cs` is now a SystemGroup
  ("EditorPlayfieldDraw"): a `Frame` system then one system per drawer in stacking
  order (Field, Items, SelectionRegions, Ghost, GroupMoves, SelectionOutlines,
  GroupTarget, SelectionHandles, GroupTags, Chrome, Timeline, SelectionBox), so F3
  and Autopsy time each part. Same class name and constructor.

Tests: 14 failures in `Tests/Xanadu.Tests.csproj`, identical with and without
this session's changes (keybinds, slide physics, level format, 3 editor draw
tests): they come from the in-progress port, not from here.

## Next: the waveform on the GPU (user approved; keep it contained)

The editor timeline is the culprit the user sees, and it's the **waveform**
(`Systems/Editor/EditorWaveformDrawer.cs`), not the notes. It only draws with a song
loaded, so the default editor launch doesn't show it: measure with a real level
(ask the user how to open one; don't browse their level files). Today it draws a
column every 2 px, up to 3 `DrawRect` per column, every frame (~2,500 rects); the
data side (`WaveformData.Average`, running sums in `Systems/Audio/WaveformAnalyzer.cs`)
is already constant time.

Plan: upload the three running-sum curves once per song as a float texture and
draw the waveform as one quad whose shader computes each column's start/end time
and samples the two sums (same maths as `Average`, including
`ChartTiming.AudioLagMs`, the 2 px columns fixed to multiples of time, the band
scales 1 / 0.62 / 0.34 and tallest-first overlap order with `Palette.WaveLow/Mid/High`).

Constraint from the user: keep the GPU draw contained. Found so far:
- `ITextureServer.Create` (`UI/Systems/Canvas/TextureServer.cs`, host side
  `HostCore/Rendering/HostTextures.cs`) only takes RGBA8 bytes. Running sums need
  floats (R32G32B32A32 or R32 per band): add one float-texture call there, or pack
  per-step levels into RGBA8 and sum in the shader (simpler format, more taps).
- Canvas effects bind extra samplers by name from `CanvasMaterial.Textures` and
  params via `PushParams` (`HostCore/Rendering/CanvasRenderer.cs`); the editor draws
  through `ShaderRoutingCanvas`. Find how an editor drawer draws one item with a
  material, and give the waveform its own effect file.
- Measure before and after with Autopsy (Editor / EditorPlayfieldDraw / Timeline).

## Then: docs cleanup (user asked)

The MoonWorks port is done (Godot removed 2026-11-10). Go through `docs/`
(`moonworks-port.md`, `render-architecture.md`, `handoff-render.md`, `design.md`,
`ui-fluent-architecture.md`, `xif-editor.md`, `ecs/`): drop or rewrite
transition-era material, Godot references that no longer hold, and finished
step lists; keep the decisions and the Later list. Mention the new tooling
(GPU times, Autopsy, `./run-autopsy.sh`) where the host is described. Delete
this handoff when its items are done.
