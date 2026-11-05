# Render architecture: layers, groups, shaders, buffers

Status: **planned, nothing built yet** (written 2026-11-05).
This is the hand-off doc for layering in the level editor and play, sharing a
core with the animator's layering. Each step below is meant to stand alone, so
work can stop after any of them with the build green. Tick the box when a step
lands and add a line under it noting anything that changed from the plan.

Pre-alpha: **no migration**. Bump the file format version and break old levels
freely unless keeping them working is trivial.

---

## 1. Decisions (locked)

These were agreed with the user. Don't revisit them without asking.

| # | Decision |
|---|---|
| D1 | A layer is one **signed integer**. Notes, colliders, deco and everything else are items on layers. Readability is the mapper's problem. |
| D1a | The **player wedge** is fixed and is not a layer. It sits between the layers like a wedge: you add layers before it (behind, negative numbers) or after it (in front, positive numbers). The player can never be moved to another layer. **There is no layer 0**: the order is …, −2, −1, wedge, 1, 2, …. The wedge holds the player and nothing else, with only `PlayerShader`: no layer pass, no buffering (for now). |
| D2 | Inside a layer, items sort by **z**, then by list order (same rule as the animator: later entries draw in front). |
| D3 | **Groups are tags, not places in the draw order.** A group can have members on any number of layers, so one colour effect changes things at different depths together. Groups stay the 16-slot bitmask (`ItemGroups.Mask`, `GroupLimits.Count`). |
| D4 | Level items have **no timeline of their own**. An item has a static colour (self modulate); every dynamic colour change comes through groups. The animator is the only place with per-item keys. |
| D5 | **Colour:** group colour effects fade over the item's own colour, and the latest to start wins (today's `GroupModulation.TryColor`). |
| D6 | **Alpha:** each group works out its own alpha (latest wins *inside* the group), and the alphas of all the item's groups **multiply**. A colour effect in one group can never undo a fade in another. |
| D7 | Two separate shader concepts: **(a) per-item shaders** applied while an item draws, and **(b) a layer pass**: a full-screen quad over a layer, off unless used. |
| D8 | A layer pass is **either/or**: a layer picks *background reader* (reads everything drawn so far, Godot `CanvasLayer`/screen-texture style) **or** *self-contained reader* (reads only that layer's own content), or none. Never both on one layer. A self-contained layer renders into its **own SubViewport** (decided 2026-11-05), not a CanvasGroup. |
| D9 | A layer pass is a **draw boundary**: at that point the screen read is guaranteed up to date. |
| D10 | **Shaders may read the screen.** A placeable **screen-read region** (Godot's `BackBufferCopy`) refreshes the back buffer for its rect, so screen-reading draws after it see current pixels. Without one, a screen read sees the snapshot from the last boundary. |
| D11 | **Shaders are keyable.** Layer shader settings are keyed on the layer's own track. Item shader settings are keyed through **groups**: a group owns an item-shader track, and each item picks a **shader source**, either *own* (its static shader, fixed settings) or **one** of its groups. It can only be one, because an item draws with a single material. |
| D12 | **Buffered groups** (optional, per group): the group's members on one layer draw into an offscreen buffer, which is then drawn into the layer as one image. That gives a uniform fade with no darkening where members overlap. |
| D13 | A buffered group sits at the **lowest z of its members** on that layer. |
| D14 | Group colour effects are always applied **per item**. A buffered group has its **own, separate** settings applied to the buffer: buffer tint, buffer alpha, buffer shader (with its own keyed settings) and buffer blend mode. The group's item-shader track and its buffer shader are separate settings. |
| D15 | Ban buffering when the group has members on **more than one layer**. The editor refuses and shows a toast saying why. |
| D16 | Ban buffering when any member is in **more than one group**. The editor refuses and shows a toast saying why. |
| D16a | **Buffering is a lock.** Ticking it is refused unless the rules hold. While it's ticked, any edit that would break the rules is refused with a toast: moving a member to another layer, adding a member on another layer, giving a member a second group. Buffering never switches itself off; it stays locked until the user unticks it. |
| D17 | **Blend modes are static** (mix, add, multiply, …), on items and on buffered groups. Not keyable, to avoid an explosion of shader combinations. Shader variants for a (shader, blend) pair are built **only when first needed**. |
| D18 | Parallax: **later.** Don't build it now, but keep layers first-class so it can become a layer property. |
| D18a | Effects that come from an item (note pops, dash links, mine fades, hit trails, dash trails) draw **on their parent item's layer**, just above it. |
| D18b | **The editor shows everything live, natively**: group moves and colours, item shaders and group shader tracks, blend modes, buffered groups and layer passes, all at the playhead, in the main editor view. The separate group preview (`EditorGroupPreviewSystem`, the preview area in the group overlay) is **removed**. |
| D19 | Performance warnings and budgets: **later.** UGC vetting will be a fixed-hardware FPS requirement. Don't add limits or warnings now. |

---

## 2. Concepts

- **Item.** Anything placed in a level: notes, rails, mines, blocks, spikes,
  texts, polygons, grass, start points, and the new screen-read region. Each
  has `Layer` (int), `Z` (int), a static colour, a static shader slot, a static
  blend mode, a shader source, and its group mask.
- **Layer.** An integer. The level holds a **layer table** with an entry for
  each layer that has settings: name, pass kind (none / background reader /
  self-contained), pass shader slot, and the keyed pass settings. Editor-only
  state (hide, lock, solo) is **not** in the level file. A layer number with no
  table entry is a plain layer with no name and no pass.
- **Group.** Today: name plus timed effects (move, colour). Adds: item-shader
  track (shader slot plus keyed settings), a buffered flag, and buffer settings
  (tint, alpha, shader slot plus keyed settings, blend).
- **Draw boundary.** Where the back buffer is refreshed: a layer pass, a
  screen-read region, or a buffered-group/self-contained composite.

---

## 3. Render model

```
field background and frame (FieldShader)

for each layer that has items, back → front (lowest number first),
with the player wedge (D1a) drawn between the last negative layer and the first positive one:
    if pass == self-contained: the layer draws into its own SubViewport (everything below goes there)

    for each slot in the layer, in z order (ties by list order):
        plain item       → draw (§4) into the current target
        screen-read rect → refresh the back buffer for its rect (boundary)
        buffered group G → at G's slot (lowest member z):
                              draw each member (§4) into G's buffer
                              draw the buffer into the layer with
                                 G buffer tint × G buffer alpha, G buffer shader, G buffer blend

    if pass == self-contained: full-screen quad samples the viewport's texture through the pass
                               shader, reading only this layer → composite onto the screen
    if pass == background reader: refresh the back buffer (boundary) → full-screen quad with the
                               pass shader, reading everything drawn so far → it becomes the screen

editor overlays (grid, selection, ghosts, labels) draw on top, outside layering
```

### 4. Drawing one item

```
self modulate (static colour)
  → group colour effects: fade over it, latest start wins            (D5)
  → alpha × product over the item's groups of each group's alpha     (D6)
  → shader: own static shader, or the chosen group's item-shader track (D11)
  → static blend mode → shader variant (shader, blend) made on first use (D17)
  → target: the current layer, or G's buffer if the item is in a buffered group
```

---

## 5. How it maps onto Godot (4.8-dev, `Godot.NET.Sdk/4.8.0-dev.7`)

Checked against the GodotSharp 4.8.0-dev.7 API docs. Anything marked
**spike** still needs proving in step 0.

### 5.1 Canvas item tree

Ordering comes from the **tree order** of canvas items under the playfield
root, not from `z_index`. `z_index` jumps out of `CanvasGroup` parents and
would break buffers. Each layer and the wedge is its own child item, so
reordering layers is reordering siblings.

```
playfield root
├─ field                    background + frame, FieldShader
├─ layer -N … layer -1      one container item per layer in use (§5.2); a self-contained
│                             layer's slot holds only its composite quad
├─ player wedge             player, hit ghosts, splitline balls, PlayerShader; never moves
├─ layer 1 … layer N        one container item per layer in use (§5.2)
└─ transport, key trail     always on top
editor overlays             separate root, above everything
```

### 5.2 One layer

```
layer container
│   pass == self-contained → the container lives in the layer's own SubViewport
│     (its own canvas, transparent clear, same size and camera transform as the
│     playfield). Its slot in the main tree holds one full-screen quad that
│     samples the viewport texture through the pass shader.
├─ draw runs                 ShaderRoutingCanvas output, issued in (z, list) order
├─ buffered group G          child item in CanvasGroup mode, at G's lowest member z
│   └─ G's member runs       material = buffer shader + buffer tint/alpha, blend via render_mode
├─ screen-read region        child item with CanvasItemSetCopyToBackbuffer(rect), at its z;
│                            rect in render-target pixels (step 0, panel E)
└─ …
pass == background reader → a sibling item right after the container:
    CanvasItemSetCopyToBackbuffer(item, true, new Rect2()) (an empty rect = full copy)
    + a full-screen quad with the pass shader
    (the copy happens just before it draws, which is the boundary of D9)
```

- **Runs.** `ShaderRoutingCanvas` already starts a new child item when the
  shader changes. The run key becomes **(shader slot, blend, shader-source
  group)**, so items following different groups never share a run.
- **CanvasGroup facts from the docs.** Its children draw as one object into the
  back buffer. A custom material overrides the built-in shader, so the buffer
  shader wrapper must repeat the built-in un-premultiply step
  (`c.rgb /= c.a; COLOR *= c;`). Children that use `clip_children` don't work
  inside one.
- **No nesting.** Because self-contained layers are viewports, a buffered
  group is never a CanvasGroup inside another CanvasGroup (§5.6).
- **Confirmed (step 0):** a screen read inside a buffered group's CanvasGroup
  reads the image from before the group (Q4). Inside a self-contained layer's
  viewport, `screen_texture` is that viewport's own content so far.
- **Confirmed (step 0):** `z_index` escapes CanvasGroups, so nothing inside a
  layer uses it.
- **Rect copies:** `CanvasItemSetCopyToBackbuffer` takes its rect in
  **render-target pixels** (`GetViewport().GetFinalTransform() *
  GetGlobalTransformWithCanvas() * localRect`), not local space, and `BackBufferCopy`'s Rect mode is wrong under
  stretch for that reason. Screen-read regions compute the pixel rect
  themselves every frame (it moves with the camera and editor zoom). Don't use
  the `BackBufferCopy` node.

### 5.3 Instance uniforms (2D has them now)

`RenderingServer.CanvasItemSetInstanceShaderParameter(item, name, value)`
sets an `instance uniform` per canvas item. This changes the plan:

- **One compiled material per (shader slot, blend)**, not per group. A group's
  keyed item-shader values are written as instance parameters on the runs
  that follow that group, once per frame.
- The same goes for a layer pass quad and a buffered group's buffer shader. Each
  is one item with its own instance values, so two layers using the same blur
  share a material.
- Level shaders mark their keyable settings as `instance uniform`, and plain
  `uniform`s stay as tuned constants (`ShaderDocument.Params`). The editor's
  key track lists only the instance uniforms.
- **Limit (confirmed by the user, 2026-11-05):** up to 16 instance uniforms
  per shader, each at most `vec4` in size (so scalars, vec2–vec4 and colours
  are fine). That's plenty if values are packed (16 × vec4 = 64 floats). The
  editor packs each keyable setting into a component slot, so a shader with
  more than 16 scalar settings still fits. For anything larger, an instance
  uniform holds an **index into a data texture** that stores the rest (one
  texture for each level or group track, one row per group or item).

### 5.4 DrawableTexture2D (new in 4.8)

A GPU texture changed by **blit** calls: `BlitRect(rect, source texture,
modulate, mip, material)`, `BlitRectMulti` (up to 4 sources into up to 4
targets of the same size and format), `GenerateMipmaps`. The material is a
`texture_blit` shader. Formats: RGBA8, RGBA8 sRGB, RGBA half, RGBA float.

What it is good for here:
- **Keeping things between frames.** A drawable keeps its contents until you
  blit over it. That allows feedback effects such as trails, echoes, ink that
  stays and smearing, which canvas items can't do because they redraw every
  frame (Q6).
- **Cheap wide blur** via `GenerateMipmaps` plus sampling a lower mip.
- **Several outputs at once** (`BlitRectMulti`), e.g. one pass writing colour and
  a mask.

What it does **not** do: canvas draw calls can't target it. Its inputs are
textures, so layer content would first have to come from a CanvasGroup or a
SubViewport (a self-contained layer's viewport texture is the natural input
for a later feedback pass). So it **doesn't replace** CanvasGroup (buffered groups) or SubViewport
(self-contained layers). It's an extra stage on top of them, if feedback passes
are wanted.

### 5.5 Blend modes and the rest

- Custom shaders get `render_mode blend_add` / `blend_mul` / … injected into
  the source for each variant, made the first time a (slot, blend) pair is
  needed. Plain (unshaded) items use a `CanvasItemMaterial` with that blend.
- Existing pieces to build on, not duplicate: `ShaderDocument` (slot, name,
  source, params), the per-item `Shader` slot, `PlayerShader`, `FieldShader`,
  `CommonShaderMaterials`, `ShaderRoutingCanvas`, `GodotShaderCanvasItems`.
- The animator: `SvgAttachmentVisuals.SetLayer` uses
  `CanvasItemSetZIndex`/`CanvasItemSetDrawIndex`, or the banded
  `DrawOrder.Banded` (was `ArtDrawOrder.Of`) when layers are contained. If characters become buffered
  (step 12), the contained/banded path is the one to keep, because z_index
  escapes CanvasGroups.

### 5.6 Self-contained layers are SubViewports (decided)

If a self-contained layer were a CanvasGroup, a buffered group on it would be
a CanvasGroup inside a CanvasGroup. Both work through the one screen back
buffer, so the inner group could overwrite what the outer group had drawn, or
take the outer group's pixels for its own. To avoid that, a self-contained
layer renders into its **own SubViewport**:

- Buffered groups inside it are ordinary, un-nested CanvasGroups inside that
  viewport.
- `screen_texture` inside it means "this layer so far", which is what
  self-contained should mean.
- The layer's slot in the main tree holds one full-screen quad that samples the
  viewport's texture through the pass shader (with the pass's instance
  uniforms), drawn with the layer's composite.
- The viewport follows the playfield: the same size (it resizes with the
  window or editor view), the same camera and canvas transform, a transparent
  clear, and an update mode that renders every frame while the layer is
  self-contained. Make it when the pass is turned on and free it when the pass
  is turned off, so a layer without one costs nothing (D7).
- **Confirmed (step 0):** the viewport's pixels are **premultiplied**, so the
  composite (pass shader or plain) uses `render_mode blend_premul_alpha`. No
  frame of lag. Size the viewport in screen pixels (panel size × screen
  scale), scale its canvas transform by the screen scale, put the camera / editor
  zoom and pan on the content, and scale the composite back by 1 / screen scale;
  it then lines up exactly with the main canvas.

## 6. Shared core with the animator

Today the animator has this (and the editor has none of it):
- `XafAttachment.ZOrder` as the layer, `MinLayer = -4096`,
  `MaxLayer = 4096` (`Animation/XafPackage.cs`), with list order as the
  tiebreak.
- `ArtDrawOrder.Of(layer, order)` (`Animation/ArtDrawOrder.cs`): the banded
  draw index. (Replaced in step 1 by `DrawOrder.Banded`.)
- `AnimatorLayerCommands.Rows` / `Move` (`Tools/Animator/Commands/AnimatorLayerCommands.cs`):
  the front-first row list, and moving forward/back (swap layers with a
  neighbour on another layer, step past one on the same layer).
- Per-pose layer keys (`XafPose.LayerOf`). These stay **animator-only** (D4).

The shared core (new, under `Core/` or a `Rendering/` folder, wherever
fits the existing layout):
- `DrawOrder`: (layer, z, list index) → sort key, and the banded index. This
  replaces `ArtDrawOrder`.
- Layer limits (one min/max for both tools).
- Ordering rules: the row list from any source of (id, layer, z), and
  move forward/back over it. This is pulled out of `AnimatorLayerCommands` so
  both tools call it.
- The alpha/colour rule (D5/D6) is level-only. The animator's equivalent is
  self modulate plus its future colour keys (see the self-modulate memory).
- Later (step 12): the animator can draw a whole character as a buffered group
  so it fades uniformly (death, teleport), using the same buffer code as levels.

---

## 7. Steps

Every step: it builds, the relevant tests pass, and visual checks are one
screenshot. ECS rules from `agents.md` apply throughout. Data files use the
versioned binary formats (`Levels/XmfBinary.cs` and friends), never JSON. UI
uses theme-role variants and stepper rows.

### Step 0: Spike the Godot primitives  ☑
Make a throwaway scene or test case that proves each item in §5: a
CanvasGroup with three overlapping rects fading uniformly, children with mixed
z inside a group, `CanvasItemSetCopyToBackbuffer` giving a fresh screen read
mid-frame, a self-contained layer as a SubViewport with a composite quad (the
§5.6 spike points), and
a `blend_add` variant of a level shader, an `instance uniform` on a
2D shader set through `CanvasItemSetInstanceShaderParameter` (limit already confirmed, §5.3),
and what `screen_texture` reads inside a CanvasGroup (Q4).
Done when: every assumption in §5 is confirmed or this doc is updated with the
real approach.

**Done 2026-11-05.** The lab tab `RENDER SPIKE` (`Tools/Lab/Experiments/RenderSpike/`,
`./run-lab.sh -- --tab=11 --shot=screenshots/render-spike.png`) has one panel
per assumption. Results (all at 1920×1080 design size drawn into a 1600×900
window, i.e. under `canvas_items` stretch like the game):

| Panel | Question | Result |
|---|---|---|
| A | CanvasGroup fades uniformly | **Yes.** Overlaps measure the same as single boxes (137,80,25 both); plain translucent boxes darken where they overlap. |
| B | `z_index` inside a CanvasGroup | **Escapes the group.** The z 1 child draws on top at full strength, outside the group's fade. Order inside groups must come from tree order (§5.1 already says so). |
| C | Screen read inside a buffered group (Q4) | **Sees the image from before the group**, as wanted: wavy stripes, none of the group's own orange member. |
| D | Full back-buffer copy between members | **Harmless.** The group's buffer survives, and members still read the image from before the group. So a screen-read region inside a buffered group does nothing for its members. |
| E | Fresh screen read mid-frame | Without a copy, a later screen read is **stale**. A full copy (`BackBufferCopy` Viewport mode, or `CanvasItemSetCopyToBackbuffer(item, true, new Rect2())`) makes it **fresh**. **Rect copies take render-target pixels, not the item's local space** (contrary to the docs): `BackBufferCopy` Rect mode and local or canvas-space rects all came out stale. `GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas() * localRect` works. |
| F | Self-contained layer as a SubViewport | **Works.** No frame of lag, and fast zoom/pan stays aligned with outlines drawn in the main canvas when the viewport is sized in screen pixels, its canvas transform is scaled by the screen scale and the composite is scaled back down. The viewport's pixels are **premultiplied**: compositing with a straight-alpha shader darkens them (172 vs 236). With `render_mode blend_premul_alpha` on the pass shader it matches the direct draw exactly. A screen read inside the viewport sees only that layer. A CanvasGroup inside the viewport fades uniformly. |
| G | Blend variants by injecting `render_mode` | **Works** for `blend_mix`, `blend_add`, `blend_mul`. |
| H | 2D instance uniforms | **Works.** Three items sharing one `ShaderMaterial` each show their own `instance uniform` set via `RenderingServer.CanvasItemSetInstanceShaderParameter`. |

### Step 1: Shared ordering core  ☑
- Add `DrawOrder` and the shared layer limits (±200, Q5); move `ArtDrawOrder`
  and the animator's ±4096 onto them.
- Pull the row list and move forward/back out of `AnimatorLayerCommands` into
  the shared code; the animator calls it. Animator behaviour stays the same.
- Tests: the existing `AnimatorLayerPanelTests` stay green; new unit tests for
  the shared ordering (sort, swap across layers, step within a layer, ends of
  the list).

**Done 2026-11-05.** `Core/Layering/` (namespace `Xanadu.Layering`):
- `DrawSlot(Layer, Z, Index)`: compares back to front by layer, then z, then
  list index.
- `DrawOrder`: `MinLayer`/`MaxLayer` = ±200, `ClampLayer`, and `Banded(layer,
  order)`, which replaces `ArtDrawOrder.Of`. `XafAttachment.MinLayer`/`MaxLayer`
  are gone; every animator clamp uses `DrawOrder.ClampLayer`.
- `DrawStack`: `SortFrontFirst`, `Neighbour`, and `TryMove` with two rules.
  `Layers` is the animator's existing behaviour (step a layer past a same-layer
  neighbour, swap layers with one on another layer). `ZInLayer` is for levels:
  swap z with a same-layer neighbour, or step past it when tied, and **never
  cross into another layer**. Changing layer is a deliberate edit in the
  inspector, not a nudge.
- The animator's `AnimatorLayerRow.Slot` is (layer, cloth ? 1 : 0, index), so
  cloth still draws over attachments on one layer. `AnimatorLayerCommands`
  sorts and moves through `DrawStack`, and only sets a layer that actually
  changed (setting a keyed layer adds a key).
- Tests: `Tests/Layering/DrawOrderTests.cs` (moved from `ArtDrawOrderTests`),
  `Tests/Layering/DrawStackTests.cs`. The Layering and Animation suites pass
  apart from 7 `AnimatorDrawTests` (bone kites), which fail the same way on
  the commit before this step.

### Step 2: Layer and z on every level item  ☐
- Add `Layer` and `Z` to every item component (`Ecs/Components/PlayfieldComponents.cs`,
  `GrassComponents.cs`, …), their documents (`Levels/LevelDocument.cs`), the
  mappers, and the XMF binary (bump the version, no migration).
- Default layer for new items depends on the kind (Q10 settled). Keep this
  in one table in code so it's easy to tune:

  | Layer | Kinds | Why |
  |---|---|---|
  | −3 | polygons | deco shapes, furthest back |
  | −2 | blocks, spikes | collidable terrain behind the gameplay |
  | −1 | notes, rails, mines, start points | behind the player, in front of terrain |
  | wedge | player | fixed |
  | 1 | grass | sways in front of the blocks it grows on |
  | 2 | texts | readable over everything |
  | current | screen-read regions | placed where you're working |

  z = one above the highest z on that layer, so newer items draw in front.
- Undo/redo and copy/paste carry them.
- Tests: round trip through the XMF binary; mapper tests.

### Step 3: Sorted drawing  ☐
- Replace the fixed kind order in `PlayfieldRenderer.Draw` (polygons → blocks
  → spikes → grass → texts → notes → …) with gather → sort → draw. Each kind's
  existing draw code becomes "draw one item".
- Effects that come from an item (note pops, dash links, mine fades, hit trails,
  dash trails) draw on their **source item's** layer, just above it.
- Build the canvas item tree of §5.1: one container per layer in use, plus
  the fixed player wedge between the negative and positive layers (D1a).
  Order comes from tree order, not `z_index`.
- Field background and frame stay at the very back; transport, key trail and
  editor overlays stay on top.
- The editor (`EditorPlayfieldDrawSystem`) and play use the same path.
- Check: a visual test with items of mixed kinds interleaved across layers; one
  screenshot.

### Step 3a: Live editor, no separate preview  ☐
- The editor draws group effects (moves, colours, and later shaders and
  buffers) at the playhead in the main view, using the same code as play (D18b).
- Effects are applied **at draw time** and never written into the item
  components. `GroupModulationSystem` overwrites positions in play; the editor
  must not, so the document, undo and saving stay on the placed positions.
  The cleanest route is for play to use the same draw-time offset too, with
  physics and hits reading a "shown position", which takes out the `BaseOf`
  bookkeeping.
- Picking and dragging (Q11 settled):
  - Items are picked where they're **shown** at the playhead.
  - Every edit is **relative**: dragging a shown item 20 px right moves its
    placed position 20 px right, so it's still 20 px off at any other time,
    before or after the group's moves. The same goes for nudges, rotation pivots
    and pasting at an offset.
  - Snapping applies to the **placed** position, so items sit on the grid when
    no group moves them.
  - An item that a group has moved at the playhead is **marked** in the editor
    (for example a group badge plus a faint line back to its placed position;
    pick the exact look when building, in the theme roles).
- Remove `EditorGroupPreviewSystem`, the preview area in the group overlay
  (`EditorGroupLayout.PreviewArea`), its wiring in `EditorSystemGroup`, and
  `EditorGroupsSceneTest`'s preview checks. Keep the easing curve drawing if it
  still has a home in the overlay.
- Check: one screenshot of the editor with a group moved and recoloured at the
  playhead.

### Step 4: Editor layering UI  ☐
- Item inspector: layer and z as stepper rows.
- Layers panel: the layers in use, front first, with the player wedge as a
  fixed row in the list that can't be moved; add a layer in front of or
  behind any row; rename; editor-only hide / lock /
  solo (bright blue only when toggled on); select all on a layer.
- Move forward/back shortcuts, the same actions as the animator, through the
  shared core.
- Locked layers can't be selected or picked on the canvas; hidden layers don't
  draw in the editor (play draws everything).
- Tests: ECS editor tests in the style of `AnimatorLayerPanelTests`.

### Step 5: Layer table in the level  ☐
- `LevelLayer` entries (number, name, pass kind, pass shader slot, keyed pass
  settings) in `LevelDocument` and the XMF binary.
- Only layers with settings are stored.
- Tests: round trip.

### Step 6: Alpha rule  ☐
- Change `GroupModulation.TryColor` so RGB keeps the latest-wins crossfade and
  alpha is worked out per group (latest wins inside it) and then multiplied
  across groups (D6).
- Tests: group A fades out, group B colours later → stays hidden; one group
  fades out then in → visible; the existing colour tests still pass.

### Step 7: Static blend modes  ☐
- A `BlendMode` enum (mix, add, sub, mul, premultiplied?) on every item and in
  the binary.
- Shader variant cache keyed by (shader slot, blend), built on first use; plain
  items use a `CanvasItemMaterial` for each blend.
- Inspector row for the blend mode.
- Check: one screenshot of add/multiply items over a background.

### Step 8: Group item-shader track and the item shader source  ☐
- A group gains an item-shader track: shader slot plus keyed uniform values over
  time. Keys use the shared timeline (the animator and editor timelines share
  behaviour through one group).
- An item gains `ShaderSource`: `Own` or a group id that **must be in its
  mask**. If the group is removed from the mask, or its track is cleared, the
  item falls back to `Own`.
- One material per (slot, blend). The group's keyed values are written each
  frame as instance uniforms on the runs that follow it (§5.3). Runs are
  keyed by (slot, blend, source group).
- Only a shader's `instance uniform`s are keyable; plain uniforms stay tuned
  constants.
- Editor: the group panel gets the shader track; the item inspector gets a
  "shader from" picker (own, or the item's groups).
- Tests: fallback rules; evaluating keyed values; members that follow the same
  group share one run material and get the same instance values.

### Step 9: Layer passes  ☐
- Pass kind on a layer: none / background reader / self-contained (D8).
- Background reader: a back-buffer refresh after the layer's last item, then a
  full-screen quad with the pass shader reading the screen.
- Self-contained: the layer's items draw into the layer's own SubViewport
  (§5.6). A full-screen quad in the layer's slot samples its texture through
  the pass shader. The viewport is made when the pass is turned on and freed
  when it's turned off.
- Pass settings are keyed on the layer's own track (shared timeline).
- Off unless used: a layer with no pass costs nothing extra.
- Editor: pass settings in the layers panel.
- Check: one screenshot each of a blur on a self-contained back layer and a
  distortion as a background reader.

### Step 10: Buffered groups  ☐
- A group gains `Buffered`, plus buffer tint, buffer alpha, buffer shader slot
  with its own keyed settings, and buffer blend (D14). The buffer shader is
  separate from the item-shader track.
- Buffering is a lock (D16a). Ticking it is refused unless the rules hold:
  - members on more than one layer → refuse, toast "buffered groups must sit on
    one layer" (D15)
  - a member in more than one group → refuse, toast "items in a buffered group
    can't be in other groups" (D16)
- While it's ticked, every edit that would break a rule is refused with the
  same toast: moving a member to another layer, adding a member on another
  layer, giving a member a second group, pasting or duplicating into a broken
  state. Check this in one place (a validator the command layer calls) so new
  commands can't skip it. Buffering never switches itself off.
- Drawing: a CanvasGroup at the lowest member z on the layer (D13); members draw
  into it in their own z order; group colour still applies per item (D14).
- Tests: the ban rules; slot placement. Check: one screenshot of overlapping
  members fading uniformly next to the same group unbuffered.

### Step 11: Screen-read regions  ☐
- A new placeable item: a rect with a layer and z. When reached in draw order it
  refreshes the back buffer for its rect (D10).
- Editor: a tool for it (in a tool-strip flyout), drawn as an outline in the
  editor only.
- Check: one screenshot of a screen-reading item before and after a region.

### Step 12: Animator on the shared render path  ☐
- Attachments, cloth, chains and effects are sorted through `DrawOrder`.
- Optional: draw the whole character as a buffered group for uniform fades,
  using the step 10 code.
- Tests: the animator layer tests stay green.

---

## 8. Open questions

- ~~Q1. The player's layer.~~ Settled: the fixed player wedge (D1a).
- ~~Q2. Rails and notes with long bodies.~~ Settled for now: the whole rail
  draws at its note's layer and z. Rails are still early, so revisit when they
  firm up.
- ~~Q3. Breaking a buffered-group rule after the fact.~~ Settled: buffering is
  a lock (D16a).
- ~~Q4. Screen reads inside a buffered group.~~ Settled (step 0, panels C and D):
  members read the image from before the group, as wanted, even with a full
  back-buffer copy between them. No lock needed.
- ~~Q5. Layer number range.~~ Settled: **±200**, one shared limit (−200…−1
  and 1…200 in levels, no 0). The animator's `XafAttachment.MinLayer` /
  `MaxLayer` (±4096) move onto the shared limit in step 1; no migration.
- ~~Q6. Feedback passes.~~ Later (§9).
- ~~Q7. Numbering around the wedge.~~ Settled: negatives, wedge, positives, no 0 (D1a).
- ~~Q8. Effects from items.~~ Settled: on the parent's layer (D18a).
- ~~Q9. Editor preview.~~ Settled: everything live in the editor (D18b).
- ~~Q10. Default layer for new items.~~ Settled: sensible defaults for each kind (step 2 table).
- ~~Q11. Editing items that groups have moved.~~ Settled: pick where they're shown,
  edits are relative to the placed position, and moved items are marked (step 3a).
- ~~Q12. Nesting buffers.~~ Settled: self-contained layers are SubViewports
  (§5.6), so CanvasGroups never nest.

## 9. Not now

Parallax (D18), feedback passes with DrawableTexture2D (§5.4, Q6), keyable blend modes (D17), passes or buffering on the player wedge, performance warnings and budgets
(D19), lifting the buffered-group bans (D15/D16), nested buffered groups.
