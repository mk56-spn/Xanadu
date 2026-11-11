# Render architecture: layers, groups, shaders, buffers

Status: **built** (2026-11-06; on the MoonWorks host since 2026-11-10).
Layering in the level editor and play, sharing a core with the animator's
layering: the decisions, the model, how it maps onto the host renderer
(`Docs/host.md` §5.3), and design notes for each part as it was built.

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
| D8 | A layer pass is **either/or**: a layer picks *background reader* (reads everything drawn so far) **or** *self-contained reader* (reads only that layer's own content), or none. Never both on one layer. A self-contained layer renders into its **own offscreen target**, not a buffered group (§5.4). |
| D9 | A layer pass is a **draw boundary**: at that point the screen read is guaranteed up to date. |
| D10 | **Shaders may read the screen.** A placeable **screen-read region** forces a fresh screen copy where it sits in the draw order. *Since the host (2026-11-10):* the renderer refreshes the copy whenever a read would otherwise be stale, so reads are always current and regions are no longer needed for correctness; they are kept as forced copies. |
| D11 | **Shaders are keyable.** Layer shader settings are keyed on the layer's own track. Item shader settings are keyed through **groups**: a group owns an item-shader track, and each item picks a **shader source**, either *own* (its static shader, fixed settings) or **one** of its groups. It can only be one, because an item draws with a single material. |
| D12 | **Buffered groups** (optional, per group): the group's members on one layer draw into an offscreen buffer, which is then drawn into the layer as one image. That gives a uniform fade with no darkening where members overlap. |
| D13 | A buffered group sits at the **lowest z of its members** on that layer. |
| D14 | Group colour effects are applied **per item**, except a buffered group's **alpha**: that goes on the buffer, so the group fades as one image (changed 2026-11-06, the user's call; its members keep only the colour). A buffered group has its **own, separate** settings applied to the buffer: buffer tint, buffer alpha (a static base, multiplied by the group's fade), buffer shader (with its own keyed settings) and buffer blend mode. The group's item-shader track and its buffer shader are separate settings. |
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
    if pass == self-contained: the layer draws into its own offscreen target (everything below goes there)

    for each slot in the layer, in z order (ties by list order):
        plain item       → draw (§4) into the current target
        screen-read rect → refresh the back buffer for its rect (boundary)
        buffered group G → at G's slot (lowest member z):
                              draw each member (§4) into G's buffer
                              draw the buffer into the layer with
                                 G buffer tint × G buffer alpha, G buffer shader, G buffer blend

    if pass == self-contained: full-screen quad samples the target's texture through the pass
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

## 5. How it maps onto the host

The renderer itself is described in `Docs/host.md` §5.3; this is how layering
uses it. (Until 2026-11-10 this mapped onto Godot's RenderingServer; the old
version is in git history.)

### 5.1 Canvas item tree

Order comes from the **tree order** of canvas items under the playfield root,
kept by draw index; nothing inside a layer uses z. Each layer and the wedge is
its own child item, so reordering layers is reordering siblings.

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
layer container             (self-contained → a parentless item drawn into the layer's own target)
├─ draw runs                ShaderRoutingCanvas output, issued in (z, list) order
├─ buffered group G         a canvas-group item at G's lowest member z
│   └─ G's member runs      drawn into G's image; composited with the buffer shader,
│                           buffer tint × alpha and buffer blend
├─ screen-read region       an item that forces a fresh screen copy before what follows
└─ …
background reader → after the container: a fresh screen copy, then a
    full-screen quad with the pass shader reading it (the boundary of D9)
```

- **Runs.** `ShaderRoutingCanvas` starts a new child item whenever the shader
  changes; the run key is **(shader slot, blend, shader-source group)**, so
  items following different groups never share a run.
- **Buffered groups** render into a pooled frame-sized texture and composite
  back as one premultiplied quad (`Docs/host.md` §5.3). `xanadu.stage` hands
  effects straight colour, so any slot shader works as a buffer shader.
- **Screen reads** inside a buffered group sample the group's own image so far,
  not the screen behind it (Godot read the image from before the group). Rare
  in levels; revisit if one needs it.

### 5.3 Instance values

One compiled material per (shader slot, blend), not per group. A group's keyed
item-shader values are written as **instance values** on the runs that follow
that group, once per frame; the same goes for a layer pass quad and a buffered
group's composite. Two layers using the same blur share a material.

- A level shader marks its keyable settings `[Instance]` (`Docs/host.md` §5.5);
  plain uniforms stay tuned constants. The editor's key track lists only the
  `[Instance]` ones.
- Values are up to `float4` each. The host packs them into the effect's
  uniform buffer per batch, so there is no 16-value limit as in Godot; an
  item with instance values is a batch of its own.

### 5.4 Self-contained layers have their own target (decided)

If a self-contained layer were a buffered group, a buffered group on it would
nest. Instead each self-contained layer draws into its **own offscreen
target** (`HostPassSurfaces`):

- Buffered groups inside it are ordinary, un-nested groups inside that target.
- A screen read inside it means "this layer so far", which is what
  self-contained should mean.
- The layer's slot in the main tree holds one full-screen quad that samples the
  target through the pass shader (with the pass's instance values), drawn with
  the premultiplied blend.
- The target follows the playfield: `ShaderRoutingCanvas` puts the root's
  (camera) transform on the surface, and the quad covers the screen in the
  root's own coordinates. Made when the pass is turned on and freed when it is
  turned off, so a layer without one costs nothing (D7).

### 5.5 Blend modes

Blend is pipeline state: one compiled effect, a pipeline per blend, made on
first use (`CanvasPipelines`). Plain items use the plain canvas effect at that
blend. The animator's contained path (`DrawOrder.Banded`) is the one to build
a buffered character on (step 12).

## 6. Shared core with the animator

Before step 1 the animator had this (and the editor had none of it):
- `XafAttachment.ZOrder` as the layer, `MinLayer = -4096`,
  `MaxLayer = 4096` (`Game/Animation/XafPackage.cs`), with list order as the
  tiebreak.
- `ArtDrawOrder.Of(layer, order)`: the banded draw index. Gone since step 1;
  `DrawOrder.Banded` (`Core/Layering/`) replaced it.
- `AnimatorLayerCommands.Rows` / `Move` (`Tools/Animator/Commands/AnimatorLayerCommands.cs`):
  the front-first row list, and moving forward/back (swap layers with a
  neighbour on another layer, step past one on the same layer).
- Per-pose layer keys (`XafPose.LayerOf`). These stay **animator-only** (D4).

The shared core (built in step 1 under `Core/Layering/`):
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

## 7. How each part was built

Each part as it was planned and then built, with the design choices made on
the way. Code comments cite these by step number. Written while Godot was the
engine: where the drawing side has moved to the host, §5 and `Docs/host.md`
are current. Data files use the versioned binary formats (`Game/Levels/XmfBinary.cs`
and friends), never JSON.

Step 0 was a spike of the Godot primitives (CanvasGroup fades, z escaping
groups, back-buffer copies, SubViewports, blend variants, instance uniforms);
its results shaped §5 and are in git history.

### Step 1: Shared ordering core
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
  `Tests/Layering/DrawStackTests.cs`.

### Step 2: Layer and z on every level item
- Add `Layer` and `Z` to every item component (`Game/Ecs/Components/PlayfieldComponents.cs`,
  `GrassComponents.cs`, …), their documents (`Game/Levels/LevelDocument.cs`), the
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

**Done 2026-11-05.**
- Layer and z are one component, `ItemLayer(Layer, Z)`
  (`Game/Ecs/Components/LayerComponents.cs`), not fields on each kind's struct. It
  sits beside `ItemGroups` and `ItemShader` and holds the kind defaults
  (`ItemLayer.Polygons` −3, `Terrain` −2, `Gameplay` −1, `Grass` 1, `Texts` 2).
- `EditorItems.Create` attaches the kind's default layer at z 0 to every
  item, so setting one later (`ItemLayer.Put`) never makes a structural change
  inside an editor command.
- `EditorItems.Place` = create + `ItemLayer.OnTop` (z one above the highest on
  that layer). The placing tools use it (placement creator, paint, polygon, form
  shape, rails). Loading and undo use `Create` and then put the saved layer.
- **Layer 0 means "the kind's default"** wherever a layer is read (documents,
  snapshots, `Put`), because there is no layer 0. It's also what a file without
  layers gives. Layers are clamped to ±200.
- Documents: the eight item documents implement `ILayeredDocument` (`Layer`, `Z`).
- XMF: optional chunk `ItemLayers` (17) in maps and (12) in mapsets, written
  by `Game/Levels/XmfItemLayers.cs`: each kind in its own chunk's order, (layer, z)
  per item. It's left out when every item is on 0/0. Minor versions are now
  2.1. Older files still read; their items take their defaults.
- Undo: every snapshot record has `ItemLayer Layer`, so changing only a layer
  is an undo step.
- Copy/paste: `EditorClipboardItem.Layer`. A paste keeps each copy's layer and
  the copies' order among themselves, and puts them in front of what's on
  those layers (`ItemLayer.OnTop(store, items)`).
- Play loads through `LevelDocumentMapper.Apply`, so play items carry layers too.
- Tests: `Tests/Editor/ItemLayerTests.cs` (defaults, placing on top, paste
  order, layer 0 and clamping, mapper both ways, map and mapset round trips,
  no chunk when unused, undo of a layer change, paste through the commands).
- Not done yet: z ties (e.g. older files where everything is z 0, or different
  kinds on one layer) need a deterministic tiebreak in step 3's sort.

### Step 3: Sorted drawing
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

**Done 2026-11-05.**
- `PlayfieldDrawList` (`Game/Systems/Playfields/PlayfieldDrawList.cs`) gathers every
  item and effect as `PlayfieldDrawEntry(Layer, Z, Kind, Entity)` and sorts
  back to front by layer, z, **kind**, then entity id (list order). The kind
  order (`PlayfieldDrawKind`: polygons, blocks, spikes, grass, texts, notes,
  rail notes, mines, start points, then effects) is the z tiebreak step 2
  left open, so old files where everything is z 0 keep the old stacking
  within a layer.
- `PlayfieldRenderer.DrawLayers` (`PlayfieldRendererLayers.cs`) draws the list
  for both play (`DrawPlay`) and the editor (`EditorItemsDrawer.Draw`), taking
  a `PlayfieldItemFrame` (view, playhead, seconds, editor flag, selection).
  The wedge is a callback, drawn as layer 0 before the first positive layer
  (or at the end). Play's wedge is hit ghosts, player and splitline balls
  under `PlayerShader`. **The editor's wedge is the player path**, since it
  stands in for the player, so the path now draws over notes on −1.
- Each kind's list draw became a single-item draw (`DrawPolygon`,
  `DrawBlock`, `DrawSpike`, `DrawGrass`, `DrawLevelText`, `DrawNote`,
  `DrawRailNote`, `DrawMine`, `DrawStartPoint`, `DrawNotePop`,
  `DrawDashLinkFade`, `DrawDashTrail`, `DrawHitTrail`). The old list versions
  are gone; `EditorGroupPreviewSystem` keeps its own and goes in step 3a.
- Things that span several items get one entry each:
  - **Note guides** (`NoteGuides` kind) sit just under the backmost note.
  - **Note glows** are drawn per *stretch*: consecutive notes on one layer get
    all their glows before any of their bodies, so the glow shader stays one
    run in the common case.
  - **Rail lanes** (`RailLanes` kind: both lanes, their judgement lines, the
    active `RailHold`s and the lane labels) sit at the very back of the
    backmost rail note's layer, or the gameplay layer with no rail notes. Each
    rail note draws at its own slot.
- **Effects:** a new `EffectLayer(Layer, Z)` component
  (`Game/Ecs/Components/LayerComponents.cs`) is copied from the source item when
  the effect spawns (`EffectLayer.From`), since the source is deleted right
  after. Effects sort after every item on the same layer and z, which is
  "just above it". Without a source (`EffectLayer.Loose`) they go in front of
  the gameplay layer. It's deliberately not an `ItemLayer`, so effects never
  count in `ItemLayer.OnTop`. `HitEffects.Spawn` now takes the source note's
  entity. Hit trails used to draw over the player and are now on their
  note's layer (D18a). Particles and the character rig draw elsewhere and
  are untouched.
- **Canvas tree:** `IRectCanvas` gains `BeginLayer(int)` / `EndLayers()` (no-op
  defaults). `ShaderRoutingCanvas` gives each layer a container item under the
  root, kept across frames, with its shader runs as the container's children.
  The root's children (its own runs and the containers) get a draw index
  (`IShaderCanvasItems.SetDrawIndex`) in the order the frame used them, so a
  layer made late still stacks where it was begun. Root draws after
  `EndLayers` go to a fresh run over every layer. Nothing uses `z_index`.
- Tests: `Tests/Playfields/PlayfieldDrawListTests.cs` (sort, kind tiebreak,
  defaults, guides, lanes, effects, wedge position) and new layer cases in
  `Tests/Shaders/ShaderRoutingCanvasTests.cs`. Visual test
  `play-layers` (`PlayLayersSceneTest`) → `screenshots/play-layers.png`.

### Step 3a: Live editor, no separate preview
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

**Done 2026-11-05.**
- `GroupOffset` (`Game/Ecs/Components/GroupComponents.cs`) replaces `GroupBase`: how
  far the groups move an item at the playhead. `GroupModulationSystem` now runs
  for play **and** the editor and only writes `GroupOffset`, `GroupColor` and
  `GroupOutlineColor`; it never touches an item's position, and removes all
  three when the item leaves its groups. In the editor a soloed group
  (`EditorGroupState.SoloGroup`) is the only one applied.
- Helpers: `GroupOffset.Of`, `GroupOffset.Shown(entity, placed)` and
  `GroupOffset.View(entity, view)`, a view shifted so an item's placed geometry
  draws where it's shown. `PlayfieldRenderer.DrawEntry` draws every item
  through its shifted view; effects already spawn at the shown position. Rail
  notes keep the plain view (their lane doesn't move, as before). Note guides
  use shown positions.
- Play reads shown positions everywhere it hit-tests: note judging, hits,
  misses, movement notes, contact barriers, mines, spikes, block bodies
  (`BlockBodySystem` moves the body whenever there's a `GroupOffset`), the
  one-way block check in `CharacterMotionSystem`, construction pops and the
  F6 hitboxes.
- Editor: `EditorItemPicker` tests points and boxes in each item's placed
  frame, so items are picked where they're shown. Drags were already relative
  (start position + delta, snapped on the placed position), so nothing changed
  there. Selection outlines, resize nubs and the rotate handle draw with the
  primary item's offset, and the resize and rotate tools read the pointer
  through the same shifted view.
- **Mark:** `EditorGroupsDrawer.DrawMoved` draws a faint line in the group
  badge colour from each moved item's placed anchor to where it's shown, with
  a small ring at the placed end. The item's existing group badge rides along
  with it.
- Removed: `EditorGroupPreviewSystem`, the overlay's preview panel
  (`EditorGroupPart.PreviewPanel`, `EditorGroupLayout.PreviewArea`,
  `UiLayers.EditorGroupsPreview`), and the sidebar's PREVIEW ON/OFF toggle
  with its ghost drawing (`EditorGroupState.Preview`,
  `EditorGroupGrid.Preview`), which the live view replaces. The overlay's
  hint line now runs under the rows. The easing curve and palette frame moved
  to `EditorGroupOverlayDrawSystem`.
- **Group panel redone** (same day, the user's call): the overlay is no
  longer a centred modal over a 70% black scrim. It's docked to the top of the
  editor viewport (`EditorGroupLayout.PanelArea`, between the tool strip and
  the right panel) with a 10%-opaque backing and translucent tracks, so the
  live field reads through and below it. The scrim is still there but clear,
  so presses outside the panel behave as before. Buttons sit in two toolbar
  rows laid out from runs in `EditorGroupLayout`: the group (name, members,
  + / − SELECTED, SELECT, INTERIOR, OUTLINE … SOLO, DONE), then its effects
  (+ MOVE, + COLOR, the selected effect's tools and curve … PLAY, SNAP,
  scroll, zoom) over the timeline. Toggles are bright blue only while on. The
  left tool panel and the note-elements bar hide while the panel is open,
  since they float in the same place.
- The timeline is only white timing lines (beats brighter) and the white
  playhead; the blue track rows are gone. Effects draw the way the animator
  draws a span between keys: a band whose thickness follows the easing
  (`TimelineEaseBand`, moved into the UI project so both tools share it;
  `AnimatorEaseBand` keeps the animator's step and segment rules on top), with
  a pin at each end, a diamond when linear and a circle when eased, white
  when the effect is selected. `EditorGroupOverlayDrawSystem` draws them;
  hit-testing uses the shared `EditorGroupLayout.BarRect`.
- **Opening and assigning split:** the sidebar grid is smaller (32×28 cells,
  3 px gaps) and now assigns directly: a cell puts the selection into that
  group, or takes it out when it's all in already (one undo step). Cells are
  bright blue when the whole selection is in the group, teal when part of it
  is. Groups are opened from a bar pinned at the top centre of the view
  (`EditorGroupBar`: GROUP n, −, +, EDIT; `EditorGroupLayout.BarArea`), and
  the panel docks under it. Stepping while the panel is open switches it to
  that group.
- The bar grew into the editor's view toolbar, sections split by thin
  separators: group (GROUP n, −, +, EDIT) │ grid icon ▾ (GRID ON/OFF, GRID SNAP)
  │ music icon ▾ (MUSIC SNAP, 1/2 · 1/4 · 1/8) and ADV │ PATH. GRID, PATH, ADV
  and SNAP left the header. Music snap off is new (`Playhead.SnapOff`, read
  by `EditorTiming.Snap`). The menus are run by `EditorViewBarSystem`; the
  shared toggle lookups (`ViewportGrid`, `ViewportSnap`) now include disabled
  entities, since their buttons sit in closed menus. The panel's drawn
  timeline has its own layer, `UiLayers.EditorGroupsCanvas`, under the bar.
- Tests: `GroupModulationSystemTests` rewritten for offsets (placed positions
  untouched, editor live, solo, leaving a group clears), and an
  `EditorPlayfieldDrawSystemTests` case for drawing moved items and the mark.
  `editor-groups` now checks the live view at 5 s →
  `screenshots/editor-groups-live.png`; `play-groups` reads shown positions.

### Step 4: Editor layering UI
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

**Done 2026-11-05.**
- Editor-only state is one component on the editor entity, `EditorLayers`
  (`Game/Ecs/Components/EditorLayerComponents.cs`): open, current row, solo,
  scroll, and the hidden / locked / kept (empty but added) sets and names.
  `Shows(layer)` (not hidden, and the soloed one if any; the wedge always) and
  `Picks(layer)` (shown and not locked) are what drawing and picking ask.
- The edits are in `Game/Systems/Editor/EditorLayerEdits.cs`: `Rows` (layers in
  use, kept or named, plus the wedge as layer 0, front first, with item
  counts), `Insert`, `StepLayer`, `PutOnLayer`, `StepZ`, `Move` and
  `FrontFirst`.
- **Adding a layer** beside a row uses a free number between it and its
  neighbour if there is one. Otherwise the new layer takes whichever of the
  two numbers is further from the wedge, and every layer from there outwards
  (with its items, names and hide/lock state) steps one further out. Beside
  the wedge it makes ±1. Refused if it would push a layer past ±200. When
  items move, it's one undo step.
- **Layers panel:** a LAYERS button at the end of the view bar (now 588 px
  wide) opens it, docked to the view's right edge under the bar, or under the
  group panel while that is open (`EditorLayersLayout`, `UiLayers.EditorLayers`
  983). The note-elements box hides while it's open, since it floats in the
  same corner. Ten row slots are built once (`EditorSceneLayers.BuildPanel`)
  and `EditorLayersPanelSystem` fills them; ▲ ▼ show only with more layers
  than rows. A row is its label (`+2  NAME · count`, teal when current), then
  H, L, S and SEL. The wedge row is just `PLAYER (fixed)`. Under the rows:
  + FRONT, + BACK and a NAME field for the current layer. Hiding, locking or
  soloing drops items that can no longer be picked from the selection.
- **Inspector:** LAYER and Z stepper rows in the sidebar above GROUPS
  (`EditorSceneLayers.BuildInspector`, `EditorLayerInspectorSystem`), shown
  while anything is selected and reading the first selected item. LAYER puts
  the whole selection on the next layer back or forward from the first item's
  (stepping over the wedge), in front of what's there and in its own order.
  Z changes every selected item's z. Each press is one undo step.
- **Move forward/back:** Ctrl+Shift+] / Ctrl+Shift+[ (the animator's keys),
  `EditorCommand.MoveInLayer` → `EditorLayerCommands`, one undo step. It runs
  `DrawStack.TryMove` with `ZInLayer` over the draw list's real order (layer,
  z, kind, id), so it never leaves the layer. A selection moves together,
  movers never pass each other, and the front-most goes first when moving
  forward.
- Drawing: `PlayfieldItemFrame.ShowsLayer` skips hidden layers; only the editor
  passes it. Picking: `EditorItemPicker` skips items whose layer can't be
  picked, both for clicks and for box selection.
- Names were editor state here; step 5 moved them into the level's layer
  table (saved, and in undo). Kept empty layers and hide/lock/solo stay editor
  state.
- Tests: `Tests/Editor/EditorLayersTests.cs` (rows, inserting in a gap and with
  no room, beside the wedge, range end, stepping over 0, moving within a layer
  and as a selection, the move command's undo, both steppers, hide, lock
  against clicks and boxes, solo, SEL, + FRONT with undo and NAME, the wedge
  row). Visual test `editor-layers` (`EditorLayersSceneTest`)
  → `screenshots/editor-layers.png`.

**Follow-up 2026-11-06.** LAYER steps each selected item from its own layer
(`EditorLayerEdits.StepLayers`) and reads LAYER MIXED when the selection spans
layers. The panel has MOVE ▲ / MOVE ▼ (`MoveLayer`: swap with the neighbour;
beside the player, hop over it to ±1 and push that side out) and DELETE
(`DeleteLayer`, empty layers only), each one undo step. Rows show `[BG]` or
`[SELF]` for a layer with a pass.

### Step 5: Layer table in the level
- `LevelLayer` entries (number, name, pass kind, pass shader slot, keyed pass
  settings) in `LevelDocument` and the XMF binary.
- Only layers with settings are stored.
- Tests: round trip.

**Done 2026-11-05.**
- Runtime: `LevelLayers` (`Game/Ecs/Components/LevelLayerComponents.cs`) on the
  level's entity beside `LevelGroups`: a `Dictionary<int, LevelLayer>`. A
  `LevelLayer` has `Name`, `Pass` (`LayerPassKind`: None, BackgroundReader,
  SelfContained), `PassShader` (slot, 0 for none) and `PassKeys`, a list of
  `ShaderKey(Param, Time, Value, Size, Ease)`: an instance uniform's value up
  to a `Vector4` (`Size` = components used, §5.3) at a time in seconds, eased
  with `GroupEase`. `ShaderKey` is meant to be reused by the group
  item-shader tracks in step 8. An entry exists only while `HasSettings`
  (name, pass, shader or keys); `SetName` with a blank name takes it out
  (`Prune`). The editor scene always has the component.
- Documents: `LayerDocument` (Number, Name, Pass, PassShader, PassKeys) and
  `ShaderKeyDocument` (Param, Time, `float[]` Value, Ease) in
  `Game/Levels/LevelDocument.cs`. `IChartItems.Layers` is on both `LevelDocument`
  and `XmfVariant`, and `LevelStore` copies it both ways between them.
- XMF: optional chunk `LayerTable` (18) in maps and (13) in mapsets (per
  variant), written by `Game/Levels/XmfLayerTable.cs` in layer order. Strings are
  inline, like texts (names and params clipped to 256 characters). Each key
  stores 0–4 floats; more is a format error, and so is an unknown pass kind.
  The chunk is left out when no layer has settings. Minor versions are now
  2.2 for both; older files read with every layer plain.
- Mapper (`LevelDocumentMapper.Layers.cs`): capture writes every entry with
  settings, back to front. Apply drops entries for layer 0, out of range, or
  without settings. A level with no table adds no component, so applying one
  inside an editor command makes no structural change. Play gets the table
  through the same `Apply`, ready for step 9.
- Undo: `EditorSnapshot.Layers` holds a clone of the table. It's compared,
  restored, and so carried by every edit's undo step. Adding a layer in the
  panel renumbers the table entries with the items (`LevelLayers.Renumber`),
  so undoing it now puts the names back too. The panel's NAME field renames
  as it's typed in, and leaving the field records one undo step.
- `EditorLayers.Names` is gone; the panel's rows and labels read the table
  (`EditorLayerEdits.Rows(store, layers, table)`,
  `EditorLayersPanelSystem.Label(row, name)`).
- No UI for passes yet; that's step 9.
- Tests: `Tests/Editor/LevelLayerTableTests.cs` (map round trip with keys,
  no chunk when unused, mapset round trip per variant and through
  `ToLevelDocument`, mapper both ways, bad entries dropped, blank names, undo
  of a rename, an insert moving the table and undo moving it back), plus undo
  of a NAME rename in `EditorLayersTests`. `editor-layers`
  still passes.

### Step 6: Alpha rule
- Change `GroupModulation.TryColor` so RGB keeps the latest-wins crossfade and
  alpha is worked out per group (latest wins inside it) and then multiplied
  across groups (D6).
- Tests: group A fades out, group B colours later → stays hidden; one group
  fades out then in → visible; the existing colour tests still pass.

**Done 2026-11-05.** `TryColor` gained a third result, `alpha`: the colour mix
and `coverage` are unchanged (one crossfade over every group's effects in start
order, so the latest start wins the colour), while each group's own alpha is
that group's crossfade alone, eased the same way, and the groups' alphas
multiply. A group with no colour effect started contributes 1. Running a
group's own effects through the crossfade (rather than taking only its latest
effect's tint outright) is what keeps a fade back in smooth: at the second
effect's first frame the group is still where the fade out left it.
- `GroupColor` and `GroupOutlineColor` carry the product as `Alpha` (default 1,
  so hand-built ones still work). `Apply` blends the colour by `Coverage` and
  applies `basis.A * Alpha` separately; it no longer folds the tint's alpha into
  the colour lerp, which was how one group's colour could undo another's fade.
- Tests: `Tests/Groups/GroupModulationTests.cs` gains a fade-out that survives
  a later opaque colour in another group, a group fading out then in (hidden at
  the end of the fade, half way back at the midpoint, visible at the end), two
  groups' alphas multiplying, and a group with no colour effect leaving the
  alpha alone.

### Step 7: Static blend modes
- A `BlendMode` enum (mix, add, sub, mul, premultiplied?) on every item and in
  the binary.
- Shader variant cache keyed by (shader slot, blend), built on first use; plain
  items use the plain material at each blend.
- Inspector row for the blend mode.
- Check: one screenshot of add/multiply items over a background.

**Done 2026-11-05.** The enum lives in the shared code (`Core/Rendering/BlendMode.cs`,
namespace `Xanadu.Rendering`) so the canvas interface can name it: `Mix`, `Add`,
`Sub`, `Mul`, `Premultiplied`, with `BlendModes.All` / `Step` / `Name` for the
inspector.
- **Data:** `ItemBlend(BlendMode)` (`Game/Ecs/Components/BlendComponents.cs`) is on
  every item from creation (`EditorItems.Create`, so an edit never adds a
  component), carried by the item documents through `IBlendedDocument`, the
  mapper, undo snapshots and the clipboard. XMF has an optional `ItemBlends`
  chunk (19 in maps, 14 in mapsets), one byte per item in the same kind order
  as `ItemLayers`; it is left out when every item is on mix. Minor versions are
  now 2.3 for both.
- **Drawing:** `IRectCanvas.SelectShader` / `SelectCommonShader` take the blend
  with the shader, so the existing trailing `SelectShader(0)` resets both. The
  routing canvas puts the blend in the run key, so items with different blends
  never share a run. *On the host* the blend is pipeline state: one compiled
  effect, a pipeline per blend, made on first use (`CanvasPipelines`; under
  Godot it was a `render_mode` injected into each shader variant). Every item draw passes its own blend: notes, blocks,
  spikes, grass, texts, mines, polygons and start points. Glows and non-item
  effects stay on mix; layer and pass shaders come in step 9.
- **Editor:** BLEND is a third stepper row in the sidebar under LAYER and Z
  (`EditorButtonKind.BlendStep`, `EditorLayerEdits.StepBlend`), stepping every
  selected item, wrapping, one undo step per press.
- Tests: `Tests/Editor/ItemBlendTests.cs` (defaults, stepping, mapper both
  ways, map and mapset round trips, no chunk when unused, undo, paste),
  blend-run cases in `Tests/Shaders/ShaderRoutingCanvasTests.cs`, and
  `Tests/Shaders/ShaderBlendTests.cs` for the injection and the material mapping.
  Visual test `play-blends` (`PlayBlendsSceneTest`): three pairs of overlapping
  blocks on mix, add and multiply over one background →
  `screenshots/play-blends.png`.

### Step 8: Group item-shader track and the item shader source
- A group gains an item-shader track: shader slot plus keyed uniform values over
  time. Keys use the shared timeline (the animator and editor timelines share
  behaviour through one group).
- An item gains `ShaderSource`: `Own` or a group id that **must be in its
  mask**. If the group is removed from the mask, or its track is cleared, the
  item falls back to `Own`.
- One material per (slot, blend). The group's keyed values are written each
  frame as instance uniforms on the runs that follow it (§5.3). Runs are
  keyed by (slot, blend, source group).
- Only a shader's instance values (`[Instance]` uniforms in Slang) are
  keyable; plain uniforms stay tuned constants.
- Editor: the group panel gets the shader track; the item inspector gets a
  "shader from" picker (own, or the item's groups).
- Tests: fallback rules; evaluating keyed values; members that follow the same
  group share one run material and get the same instance values.

**Done 2026-11-05.**
- **Track data:** `GroupDefinition` gains `Shader` and `ShaderKeys`, a list of
  the existing `ShaderKey` (param, time, up-to-vec4 value, size, easing), so a
  group track and a layer pass track are the same thing. `IsEmpty`, `Clone`
  and `SameAs` include it, so undo's `LevelGroups` clone carries the track.
  Documents: `GroupDocument.Shader` and `ShaderKeys`.
- **Item source:** `ItemShaderSource(int Group)` on every item from creation
  (`EditorItems.Create`), with `Own` = -1. The eight item documents implement
  `IShaderSourcedDocument`, and the mapper, undo snapshots and the clipboard
  carry it.
- **XMF:** optional `GroupShaders` chunk (20 in maps, 15 in mapsets) and
  `ItemShaderSources` chunk (21 and 16), both left out when unused. The key
  encoding moved into `XmfShaderKeys` and is shared with the layer table's pass
  track (same bytes as before). Minor versions are now 2.4 for both.
- **Evaluation:** `ShaderKeyTrack.ValueAt` holds before the first key, eases
  between keys with the earlier key's curve, and holds after the last;
  `Evaluate` lists each keyed setting once. `GroupModulation.TryItemShader`
  resolves the source: it must be one of the item's groups and its track must
  have a slot, otherwise the item keeps its own shader (a group with keys but
  no slot also falls back). `PlayfieldRenderer.SelectItemShader` does that for
  every item draw, writing the evaluated values as the run's instance
  uniforms.
- **Runs:** the routing canvas keys runs by (shader slot, blend, source
  group); the values are written once per run-selection per frame as the run's
  instance values (`IShaderCanvasItems.SetInstanceUniform`), so one material serves every
  group. Nodes' `SelectShader` gained `sourceGroup`, and `SetShaderValue` adds
  a value for the run picked next.
- **Only instance values are keyable:** `ShaderSourceUniforms` reads the
  `[Instance]` uniform declarations out of a slot's source (comments stripped), and that list is what the editor keys. Plain
  uniforms stay the slot's tuned constants.
- **Editor, group panel:** a third toolbar row — `SHADER n · k KEYS`, `SHD`
  (cycles the slot; changing it clears the keys), `PARAM` (cycles the declared
  instance values), `CH i/n` (the component the buttons change), `−` / `+`
  (change that component at the snapped playhead and write a key there,
  replacing one at the same moment) and `CLEAR` (drops that uniform's keys).
  It is display-only when the group has no track; a slot with no instance
  uniforms says so.
- **Editor, item inspector:** a `SHADER OWN` / `SHADER G n` stepper under BLEND
  (`EditorButtonKind.ShaderSourceStep`, `EditorLayerEdits.StepShaderSource`)
  steps through own and the first selected item's groups, wrapping; items
  without the chosen group are left on own, since a source must be in the mask.
  The four stepper rows are now 24 px with 4 px gaps (`UiMetrics.Control.Xs` /
  `Space.Xs`): that keeps the groups grid out from under the start point
  dialog, which the extra row had pushed it into.
- Tests: `Tests/Groups/GroupShaderTrackTests.cs` (holding before/after,
  easing, replacement at the same moment, evaluate order, fallback rules, map
  round trip, no chunks when unused, mapper, undo, source stepping),
  `Tests/Editor/ItemShaderSourceTests.cs` (defaults, every kind's map and
  mapset round trip, undo, paste), `ShaderSourceUniformTests` (parser) and the
  group-source cases in `ShaderRoutingCanvasTests` / `PlayfieldShaderRoutingTests`
  (per-source runs, shared run, instance values, an item keeping its own shader
  when the group is not in its mask). `editor-groups` now also steps a shader
  slot and keys a tint, so the panel's track is exercised by the visual harness.

**Follow-up 2026-11-06 (sources).** Leaving a group puts an item whose
shader came from it back on its own. The inspector's SHADER stepper offers
only groups with a shader track and names it (`SHADER G3 BLUR`).

**Follow-up 2026-11-06 (review fixes).**
- An unkeyed setting holds the shader's declared default
  (`[Instance] [Default(1)] uniform float x;` → `InstanceUniform.Default`), not zero,
  so a first key starts from it (`ShaderKeyTrack.ValueAt(..., unkeyed)`).
- **Key lane.** The group panel has a lane under the effect rows showing the
  selected setting's keys as pins with ease bands. A press on a pin selects
  it and moves the playhead there; dragging retimes it (it never lands on
  another key, so dragging over one can't delete it). The lane's cells type
  the key's moment and value (one number per channel, one number for all, or
  `#hex` for colours), cycle its easing and delete it. Keying with − / + at a
  key's moment keeps its easing. `EditorGroupSystem.Keys.cs`,
  `EditorShaderKeyEdits`.
- **SHD is a list by name** in the group panel and the layers panel
  (`ShaderSlotChoice`, on `UiLayers.EditorShaderChoice` so it takes presses
  over the panels). Changing the shader keeps keys of settings the new one
  still declares (`EditorShaderKeyEdits.KeepFor`). The layers panel's key row
  is now PARAM · CH · − value + · CLEAR, like the group panel's.
- Not done: the layers panel's pass track still has no timeline of its own.

### Step 9: Layer passes
- Pass kind on a layer: none / background reader / self-contained (D8).
- Background reader: a back-buffer refresh after the layer's last item, then a
  full-screen quad with the pass shader reading the screen.
- Self-contained: the layer's items draw into the layer's own target
  (§5.4). A full-screen quad in the layer's slot samples its texture through
  the pass shader. The target is made when the pass is turned on and freed
  when it's turned off.
- Pass settings are keyed on the layer's own track (shared timeline).
- Off unless used: a layer with no pass costs nothing extra.
- Editor: pass settings in the layers panel.
- Check: one screenshot each of a blur on a self-contained back layer and a
  distortion as a background reader.

**Done 2026-11-05.** No new level data was needed: step 5 had already put the
pass kind, pass shader and keyed pass settings in the layer table, its document
and the XMF `LayerTable` chunk.
- **Surfaces:** `IPassSurfaces` / `IPassSurface` hand out one surface per
  self-contained layer and free it when the pass stops being self-contained
  (and when the editor is left). On the host that is `HostPassSurfaces`: a
  parentless canvas item drawn into its own frame-sized texture
  (`IPassSurface.TextureId`). `XanaduSystemRoot` makes one service for play and
  one for the editor, so a layer number means different surfaces in each.
- **Canvas:** `IRectCanvas.BeginLayer(layer, target)` puts a layer's container
  under a given canvas item, and `CopyScreen()` makes the next shaded run ask
  for a full back-buffer copy just before it draws. `ShaderRoutingCanvas` draws
  a self-contained layer's items straight into its surface's item (with their
  shader runs as children), and `IShaderCanvasItems` gained `CopyScreen` and
  `Reparent`.
- **Drawing:** `PlayfieldRenderer.DrawLayers` starts and finishes each layer's
  pass around its items. A self-contained layer composites its surface texture
  as a full-screen quad with the pass shader and `BlendMode.Premultiplied` (the
  §5.4 premultiplied composite; a pass with no shader still composites that
  way). A background reader calls `CopyScreen()` and draws a white full-screen
  quad through the pass shader, sampling the screen (`sampleScreen`). Both write the
  layer's `PassKeys` as instance uniforms at the playhead with
  `ShaderKeyTrack.Evaluate`. The quad is drawn after `EndLayers()`, so it stacks
  exactly where the layer sat; a layer with no pass does neither and makes no
  surface.
- **Editor:** the layers panel has a pass row: `PASS NONE/BG/SELF` cycles the
  current layer's kind, `SHD n` cycles the pass shader slot, and a second row
  keys that shader's instance uniforms (`PARAM`, `CH i/n`, `−` / `+` at the
  playhead, `CLEAR`). It shares the key editing with the group panel
  (`EditorShaderKeyEdits`). Each press is one undo step; a pass of none drops
  the layer's table entry again (`Prune`).
- Tests: `Tests/Playfields/LayerPassTests.cs` (a self-contained layer's items go
  into its own canvas and are composited once with the premultiplied blend and
  its keyed values; a background reader copies the screen before its quad; a
  plain layer does neither) and a pass-row case in `EditorLayersTests`.
  Screenshots: `play-pass-blur` (`PlayPassBlurSceneTest`, a blurred
  self-contained back layer with a sharp text in front →
  `screenshots/play-pass-blur.png`) and `play-pass-distort`
  (`PlayPassDistortSceneTest`, a background-reader wave over the field and a
  sharp layer in front → `screenshots/play-pass-distort.png`). `play-layers`,
  `play-groups`, `play-blends`, `editor-layers` and `editor-groups` still pass.

### Step 10: Buffered groups
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
- Drawing: a canvas-group item at the lowest member z on the layer (D13); members draw
  into it in their own z order; group colour still applies per item (D14).
- Tests: the ban rules; slot placement. Check: one screenshot of overlapping
  members fading uniformly next to the same group unbuffered.

**Started 2026-11-05** (data, rules, drawing, the panel's toggle and the
screenshot landed; settings UI and edit guards still to do):
- `GroupDefinition` gained `Buffered`, `BufferTint`, `BufferAlpha`,
  `BufferShader`, `BufferKeys` (its own `ShaderKey` track) and `BufferBlend`;
  `Clone`, `SameAs` and `IsEmpty` include them, so undo carries the whole
  buffer.
- `GroupDocument` carries the same, and a new optional `GroupBuffers` chunk
  (22 in maps, 17 in mapsets) stores buffer tint, alpha, shader, blend and
  keys for every buffered group. Minor versions are now 2.5 for both. The
  mapper writes and reads the buffer both ways (`CaptureKeys` / `ApplyKeys`
  are shared with the item-shader track).
- `GroupBufferRules` (`Game/Ecs/GroupBufferRules.cs`): `Problem` returns
  `OneLayer` ("buffered groups must sit on one layer", D15) or `OneGroup`
  ("items in a buffered group can't be in other groups", D16); `CanBuffer`
  and `Broken` (the first buffered group an edit has broken) are what the
  editor's commands will call.
- Tests: `Tests/Groups/GroupBufferTests.cs` — the two rules, a group broken
  by a layer move, an empty buffered group staying valid, map and mapset
  round trips, no chunk when nothing is buffered, the mapper both ways, and
  undo of turning buffering on. `Groups` 88 pass.
- Drawing: `IRectCanvas.BeginBuffer(shader, blend, tint, alpha)` /
  `EndBuffer()`, and `ShaderRoutingCanvas` keeps a pool of grouped canvas items
  under its current layer. A grouped item is made with
  `SetCanvasGroup` and self modulate tint × alpha, its material is the buffer
  shader at the buffer blend, and its keyed values are written when the buffer
  ends. Buffered members never draw into the grouped item directly: a plain
  member still gets a child run, so the buffer shader applies to the composite
  only. `PlayfieldRenderer` detects members (items in exactly one group whose
  group is buffered), draws the first one's whole group at its lowest slot, and
  skips the members when their own entries come round; effects never join a
  buffer and note glows are left out of one. Test:
  `LayerPassTests.ABufferedGroupDrawsItsMembersIntoOneGroupedImage` (one
  grouped item, tint × alpha, buffer shader and blend, keyed value).
- Editor: the group panel's effects row has a `BUF` button. Ticking it asks
  `GroupBufferRules.Problem`; when the rules don't hold the tick is refused and
  the reason is posted as the editor's warning popup (`WarningRequest`),
  buffering never switches itself off, and unticking is always allowed. The
  button reads `BUF ON` in bright blue while on.
  Screenshot: `play-buffered-group` (`PlayBufferedGroupSceneTest`) — two
  clusters of three overlapping blocks faded the same amount, the left one
  per item (overlaps darken) and the right one buffered (the overlaps keep the
  members' colour) → `screenshots/play-buffered-group.png`.
- Still to do: buffer settings in the group panel (tint, alpha, buffer shader
  with its own keys, blend) and the edit guards (layer moves, membership,
  paste/duplicate) that refuse edits breaking a locked buffer.

**Done 2026-11-06.** The buffer settings row and the lock's edit guards are in.
- **Buffer settings row.** The shader row's `TRACK ITEM/BUFFER` button switches
  the slot, uniform, key and clear buttons between the group's item-shader
  track and its buffer track. `TINT`, `A−`/`A+` and `BLEND` edit the buffer's
  own tint, alpha and blend; the buffer shader's keys are written to
  `BufferKeys` and evaluated at the playhead. Every press is one undo step.
  The row was widened so its labels read in full (the old widths cut
  `TRACK ITEM`, `TINT #ffffff` and `A 1.0` short).
- **Edit guards (D16a).** `GroupBufferRules.Problem` / `Broken` take a predicted
  `(layer, mask)` per item (`BufferPreview`), and `Game/Systems/Editor/EditorBufferGuard`
  asks them before an edit: `MovingToLayer` refuses moving one member away from
  its buffered group's layer (moving every member together is allowed), and
  `AddingToGroup` refuses a member gaining a second group or joining a buffered
  group on another layer. The layer inspector's LAYER steppers, the sidebar
  group grid and the panel's `+ SELECTED` all refuse with the rule's message as
  a `WarningRequest` and record nothing. The scrim makes the group panel modal,
  so the grid and layer stepper are only reachable with the panel closed.
  Paste keeps a copied member's layer and mask, so it cannot break a lock.
- Tests: `GroupBufferTests` (one member moved vs the whole group, a new member
  on the group's layer vs another layer, a second group, a pasted member),
  `EditorLayersTests.TheLayerStepperRefusesToBreakABufferedGroup`, and the
  `editor-groups` visual test, which refuses an across-layers tick, buffers the
  group once its block moves down, refuses a second group and a layer move, and
  drives the whole buffer row. Screenshots: `editor-groups` (the buffer row)
  and the refreshed `play-buffered-group`.

**Follow-up 2026-11-06 (second pass).**
- **Buffered fades (D14 changed).** `GroupModulationSystem` leaves a buffered
  member's `GroupColor` / `GroupOutlineColor` alpha at 1, and
  `DrawBufferedGroup` multiplies the buffer alpha by
  `GroupModulation.BufferAlpha` (the group's own D6 alpha at the playhead), so
  COLOR fades on a buffered group fade the image, with no darker overlaps.
  `GroupModulation.BufferedGroup` is the one membership test both use.
- **The lock in one place.** `EditorHistory.Record` refuses any edit that
  newly breaks a buffered group (it compares `GroupBufferRules.BrokenGroups`
  at capture and at record): the level goes back to before, nothing is
  recorded, and the rule is posted as a `WarningRequest`. A level that was
  already broken stays editable. `EditorBufferGuard` still refuses up front
  where a command can ask, so the usual path never rebuilds.
- **Panel.** The shader row has ITEM SHADER / BUFFER tabs instead of the TRACK
  toggle; BUFFER needs the group buffered, and the buffer's TINT, alpha and
  BLEND show only there. TINT opens the swatch palette (no alpha steps; the
  buffer alpha is its own label − +), BLEND is a list. Values read label then
  − +. The scrim no longer covers the sidebar, so the groups grid and layer
  inspector work with the panel open.

**Follow-up 2026-11-06 (review fixes).** A buffered group is drawn after
its layer has begun and after the player wedge, so a group that opens its
layer no longer lands in the previous layer (or its pass surface), and one on
layer +1 no longer draws behind the player. Each layer's slice of a group is
its own buffer, so members a file left on another layer still draw instead of
vanishing.

### Step 11: Screen-read regions
- A new placeable item: a rect with a layer and z. When reached in draw order it
  refreshes the back buffer for its rect (D10).
- Editor: a tool for it (in a tool-strip flyout), drawn as an outline in the
  editor only.
- Check: one screenshot of a screen-reading item before and after a region.

**Done 2026-11-06.**
- **Item.** `ScreenReadRegion(Position, Size)` (`Game/Ecs/Components/ScreenReadComponents.cs`)
  is a placed rect with an `ItemLayer` like every other item. It draws no
  body. The editor item plumbing carries it: `EditorItems` position read/write,
  `GridPlacement` bounds (so the resize handles work), picking and marquee,
  snapshots, copy/paste, the level document (`ScreenReadDocument`), the mapper
  both ways, and the binary formats.
- **Draw boundary.** `PlayfieldDrawKind.ScreenRead` sorts it by layer and z.
  `DrawScreenRead` calls `IRectCanvas.CopyScreen(rect)` where the order reaches
  it. A region is never a buffered-group member: it is skipped by the buffered
  entries and by the buffer's member loop.
- **Canvas.** `ShaderRoutingCanvas.CopyScreen(Rect2)` keeps a pool of copy
  items on the current layer, puts each after the draws already made and starts
  a fresh run for the draws after it, so the boundary is in the right place.
  On the host a copy item calls `SetCopyScreen`, and the renderer copies the
  whole target (a rect copy is a screen copy). The rect is still scaled to
  render-target pixels (threaded through the system groups) for hosts that copy
  rects. Unused copy
  items are disabled each frame, so a removed or hidden region cannot fire the
  last frame's copy again.
- **Editor.** The tool lives in the `Text and screen reads` flyout (a lone tool
  cannot be a flyout). A click places a default-sized region on the editor's
  **current** layer, in front of what is there (step 2's default table). The
  ghost draws the outline under the pointer, and `EditorSelectionDrawer`
  outlines every region over the items, with `SCREEN READ` and the selection's
  resize handles. Outlines are drawn after the whole item pass, so a region's
  own mark can never feed a later screen read. The editor draws live, so the
  boundary also works there.
- **XMF.** Optional chunk 23 (maps) / 18 (mapsets), minor versions 2.6. Each
  region stores its own layer and z inside the chunk, so the shared item-layer
  chunk keeps its older layout and old files still read. Strings, `IChartItems`,
  `XmfVariant` and normalization carry the list.
- Tests: `Tests/Playfields/ScreenReadTests.cs` (sorted by layer/z, the copy
  before the draw after it, placement on the current layer, map and mapset
  round trips, mapper both ways, undo), `ShaderRoutingCanvasTests` (a rect copy
  gets its own item, the pixel-scale converter, unused copies disabled), and two
  visual tests: `play-screen-read` (a reader before a region shows the plain
  field; the same reader after one shows the coloured block under it) and
  `editor-screen-read` (the tool places, outlines and resizes a region).
  Screenshots: `screenshots/play-screen-read.png` and
  `screenshots/editor-screen-read.png`.

**Follow-up 2026-11-06.** Regions can join groups (their moves carry the
copy rect with what it reads under). The region chunk sets the count's top bit
when each region also stores its group mask, so 2.6 files still read. A new
region starts at the default size or a third of the view, whichever is
smaller, so it fits where it's placed.

### Step 12: Animator on the shared render path
- Attachments, cloth, chains and effects are sorted through `DrawOrder`.
- Optional: draw the whole character as a buffered group for uniform fades,
  using the step 10 code.
- Tests: the animator layer tests stay green.

**Done 2026-11-06.** The animator now states its order once, in the shared vocabulary.
- **One order rule.** `Game/Animation/AnimatorDrawOrder` gives every piece its draw
  index from the shared `DrawOrder`: an attachment's place in the attachment
  list, then cloth, chains and effects in bands after it on the same layer. It
  also owns the layer panel's row slot, so `AnimatorLayerCommands` and
  `SvgAttachmentVisuals` cannot disagree. When layers are contained
  (`containLayers`, used by ghosts and the animator tool) the item gets the
  banded `DrawOrder.Banded` index at the canvas's own z, which is what a
  buffered group needs; otherwise the layer stays a relative `z_index` with the
  same order as its draw index.
- **What changed in drawing.** Attachments used to keep order 0 and rely on
  tree order; they now carry their list index. Cloth, chain segments and effect
  primitives use the same band constants as before, moved into
  `AnimatorDrawOrder` and clamped inside their band (effects are no longer
  ordered by their creation count).
- Tests: `Tests/Animation/AnimatorDrawOrderTests` (attachment list order, the
  cloth/chain/effect bands, a later layer always winning, band clamping, and the
  panel's row order matching the canvas order).
- **Not built (optional in the plan):** wrapping a whole character in a
  buffered group for a uniform fade. The contained path is already the one to
  build it on, since z escapes a buffered group; the step 10 buffer code is
  level-side (`IRectCanvas.BeginBuffer`), so the animator would need a small
  buffered-art wrapper of its own.

---

## 8. Open questions

- ~~Q1. The player's layer.~~ Settled: the fixed player wedge (D1a).
- ~~Q2. Rails and notes with long bodies.~~ Settled for now: the whole rail
  draws at its note's layer and z. Rails are still early, so revisit when they
  firm up.
- ~~Q3. Breaking a buffered-group rule after the fact.~~ Settled: buffering is
  a lock (D16a).
- ~~Q4. Screen reads inside a buffered group.~~ Settled under Godot (members
  read the image from before the group). On the host they read the group's own
  image so far (§5.2); revisit if a level needs the old behaviour.
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
- ~~Q12. Nesting buffers.~~ Settled: self-contained layers have their own
  targets (§5.4), so buffered groups never nest.

## 9. Not now

Parallax (D18), feedback passes (Q6: a texture kept between frames, for trails, echoes and smearing), keyable blend modes (D17), passes or buffering on the player wedge, performance warnings and budgets
(D19), lifting the buffered-group bans (D15/D16), nested buffered groups.
