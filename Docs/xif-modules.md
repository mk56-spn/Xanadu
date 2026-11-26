# Everything is a Xif

Plan agreed 2026-11-22. One composable item format, `Xif`, for everything the
animator and the level editor make. A Xif is a node made of modules and may
hold child Xifs. SVG loading leaves the game; XAF goes away.

## The idea

- A **Xif** is anything composable: a note, a flare, a patch of grass, a body
  mesh, a dress, a detached eye, a hair chain, a whole character.
- What a Xif *is* comes from its **modules**, never from a per-kind type.
- A character is a Xif tree: the root carries a skeleton and clips; body
  meshes, dress, eyes and hair are child Xifs bound to its bones.
- An animation is a clips module on a Xif with a skeleton. There is no
  separate animation file.
- A chain is a child Xif with its own small skeleton plus physics, so bones
  enter through one door: the skeleton module.
- Any Xif is animatable, not only skeletons: clips key any module's values.

## Modules

| Module | Holds | Replaces |
| --- | --- | --- |
| Geometry | Polygons (points, fill). No textures. | `XifPrimitive` polygon, SVG art |
| Shader | Shader refs and their inputs: tuned params **and textures** | `Xif.Shaders`, `Xif.Params`, polygon texture `Ref` |
| Particles | The node's own particle settings, streamed from it (GPU sim) | `XafEffect.Particle` |
| Instancer | Count, area, size and turn jitter, seed; draws the node's geometry once per copy | — (new) |
| Text | Text drawn by the node | — (new) |
| Skeleton | Bones: rest, parent, z, translation, bend lock, limits, tip | `XafSkeleton` |
| Bind | Bone id on the nearest skeleton above, transform, z, self-modulate | `XafAttachment` / `XafEffect` bone + transform |
| Variants | Other geometry the node can show; the `Art` channel picks one | `XafAttachment.Alternates` |
| Skin | Bones by id, detail, blend, weights; bends the node's geometry | `XafMesh` / `XafSkinMesh` |
| Chain | Gravity, damping, lean, collider, sheet vs per-segment; generates the node's segment bones from a count and length | `XafChain` |
| Cloth | Grid, widths, length, stiffness, pins | `XafCloth` |
| Clips | Clips, events, limb targets, transitions | `XafClip`, `XafTransition` |

A texture is a shader input. A "sprite" is a quad with a shader that samples
one texture; that shader lives in the `xanadu.*` library.

## Instancer and the shader stack

Agreed 2026-11-22. Grass and particles are siblings: both are many copies of
one piece of geometry.

- **Instancer** (`XifInstancer`, built): how many copies of the node's
  geometry, spread over an area, each sized and turned a little differently,
  all from a seed. Copies are placed once and never die. Each copy has a
  number the shader gets, to seed its own look.
- **Grass** = Instancer + Geometry (one blade) + Shader (sway). The sway is a
  stateless shader function of time and the copy's seed.
- **Particles** = Instancer + a **Simulation** module (lifetime, velocity,
  forces: state over time). Step 1 built 2026-11-22: `XifParticles`
  (Core/Items) holds the node's own settings (an `XpfParticle`), not a
  library name; the host's GPU server still simulates them. Step 2 (not
  built): each particle drawn as a copy of the node's geometry through its
  shader, instanced on the GPU from the particle buffer.
- **Flare** = Geometry (a square) + the flare shader. No instancer.
- The rule: a shader never makes things, it only moves and colours them.
  Geometry and instancing are modules; anything with memory is a Simulation
  module.

**The shader is the GPU mirror of a Xif** (first cut built 2026-11-22). A Xif
puts data modules together on the CPU; a shader puts code modules together on
the GPU. `XifShader.Layers` stacks common shaders over the module's own shader,
bottom first. A layer is an ordinary effect, the same file that draws on its own:

- `Slang/ShaderLayers.Stitch` puts each layer's code in a namespace of its own
  (helpers never clash), lifts the uniforms out so params stay global by name
  (a uniform two layers declare, like `tint`, is shared), and writes one
  `deform` calling the layers' in order and one `shade` that hands each layer
  the colour so far as `i.colour`. `[Grid]` is the finest any layer asks for.
  A layer that keeps state can't be stacked.
- Layered shaders go by one name wherever a shader path would
  (`Core/Rendering/ShaderLayersKey`: `layers:a|b`); the pipelines
  stitch and compile them the first time they are drawn, and keep them.
- Starter layers in `Assets/Shaders/Layers/`: sway, gradient, tint, dissolve
  (`CommonShaders.Layers`). Any common shader can be a layer.
- Grass is now a stack (sway + gradient) over no shader; `Game/grass`
  is no longer used.
- The Xif editor's LAYERS list adds (+ LAYER), reorders and removes layers;
  the settings show every layer's params. `--edit-xif=part@base+layer+…` in
  the animator opens one with a stack.

Not yet: map-slot shaders as layers, the 32 vec4 varying budget check across a
stack, error lines pointing into the stitched text are per layer file but the
stitched header reads "stack".

**The seam** between them, as built for decos: the Xif hands its shader
geometry, uv, the copy's number and its own params, and gets back position
and colour only. uv runs 0..1 across the geometry's box (y down, 1 at its
bottom); uv.x's whole part is the copy's number and its fraction the place
across. `unit` (on the frame, and `i.unit`) is screen pixels per playfield unit, `xif_seed` the
instancer's seed. The node's shader params go in by name.

## Tree rules

- A node is either inline or a reference to a promoted Xif by its `Id`.
  Inline is the default; promoting makes it a standalone asset others reuse.
- Node ids are unique within their root. Channels target `(node id,
  property)`, so the `Bone / Attachment / Effect` channel target split goes.
- A bone id is looked up on the nearest skeleton at or above the node. A dress
  moved onto another rig with the same bone names still works.
- An effect is just a child that starts hidden; `Visible` and `Opacity`
  channels show it. Attachments and effects stop being two things.
- Draw order: z is one layer space per root, as now; the `Layer` channel
  still overrides it.
- Any Xif may carry a Clips module, not only the root. A Xif's clips key its
  own subtree only; a parent's clips reach everything below it.
- A child's clips layer on top of what its parent does to it: a lantern
  bound to the hip rides the run clip and sways by its own clip on top.
- A child loops its default clip by itself. A parent switches it with a
  keyed `Clip` channel on that child node (held, never blended), so the
  root's dash clip can flip the lantern to "jolt". The game only ever
  drives the root.
- A Xif with clips plays the same standalone: the lantern placed in a level
  with no rig still sways.

Example, the stand-in:

```
standin                      Skeleton · Clips (run, air, dash, hang)
├─ torso                     Skin (hips, spine, chest) · Geometry · Shader
├─ head                      Bind (head) · Geometry · Variants
├─ eye L/R                   Bind (head) · Geometry
├─ hand L/R                  Bind (hand) · Geometry · Variants (open, fist)
├─ leg L/R                   Skin (thigh, shin, foot) · Geometry
├─ hair                      Bind (head) · Skeleton (segments) · Chain · Geometry
├─ dress                     Bind (hips) · Cloth · Geometry · Shader
├─ lantern                   Bind (hips) · Clips (sway, flicker, jolt) · Geometry · Shader
│  └─ glow                   Geometry · Shader · Particles
└─ dash streak               Bind (chest) · Geometry · Shader · Particles, starts hidden
```

## Where the code lives

`Xif` stays in Core (level 1) because the UI's Xif editor uses it, and UI may
not reference Model. Core holds the node, the module base and the plain
modules (geometry, shader, bind). Rig modules (skeleton, skin, chain, cloth,
clips) live in `Model/Animation` and plug into the node as modules.

## File format

XIF 1.0, chunked binary like the other formats. The tree is stored flat:
a node list with parent indices (like bones today), each node followed by its
module chunks. A reference node stores the target `Id` instead of modules.
`.xaf` files stop loading; no migration.

## What gets deleted

- SVG art: `Model/Animation/SvgArt.cs`,
  `XafSvgDocument`, `XafSvgOutline`, `Storage/Animation/SvgArtFiles.cs`, the
  animator's SVG catalog, menu and watch system, `Tools/Animator/Assets/SVG`,
  the default hair segment SVG, `XifPrimitive.SvgKind`.
- Polygon textures: `XifPrimitive.Ref`.
- XAF: `XafPackage` and `Serialisation/Animation/*`; its pieces move into
  modules above. `Assets/Characters/standin.xaf` and the animator's
  `contact.xaf` go; the user redraws the stand-in with the polygon tool.
- `SvgAttachmentVisuals` becomes the Xif tree's drawer (`XifDrawer`).

## Order of the cuts

1. **Node and modules.** ✅ 2026-11-22. `Xif` is a node: `Modules`
   (`XifGeometry` polygons, `XifShader` ref + params + textures), `Children`,
   and `Ref` for a promoted Xif. `XifBinary` writes the tree flat with tagged
   modules. Polygon textures and per-polygon shaders are gone; the editor
   sets the node's shader.
2. **Rig modules.** ✅ 2026-11-22. `RigPackage` is now a thin rig over a
   root `Xif`: the root carries `RigSkeleton` and `XifClips`; parts, chains
   and cloths are child nodes carrying `RigAttachment` (the bind module),
   `RigChain`, `RigCloth`, plus `XifVariants`, `XifParticles` and `RigMesh`
   (skin). `Attachments`, `Chains` and `Cloths` are read-only flat views of
   the tree, rebuilt by `Add` / `Remove` / `Refresh`. Effects are gone as a
   kind: a streak or particle stream is a part with `Hidden` set, keyed by
   `Visible` and `Opacity` like any part. Channels target `Bone` or
   `Attachment` (a node id).
3. **XIF 1.0 binary.** ✅ `XifFile` (magic `XIF1`: strings + tree);
   `XifBinary` / `XifBinary.Rig` hold every module's codec. `RigFiles`
   loads and saves rigs through it. `Serialisation/Animation` is deleted.
4. **Runtime.** ✅ `RigPose` keeps one state per part (art, layer, hidden,
   opacity); `XifRigVisuals` draws parts, alternates, skins, chains (pieces
   and sheets) and cloths from polygons. A part with no bone rides the part
   above it. The puppet loads `Assets/Characters/standin.xif`.
5. **Animator.** ✅ `+ PART` adds a part with a square to the bone; the XIF
   panel draws the selected part, its showing alternate, or the selected
   chain's art. `+ ART` copies the part's own art to redraw. The parts list
   shows nesting and hidden parts; the EFFECTS list is folded into PARTS.
   The SV mode is PT (part placement).
6. **Delete.** ✅ SVG art (`SvgArt`, `XafSvgDocument`, `XafSvgOutline`,
   `SvgArtFiles`, the animator's picker, catalog, watcher and
   `Tools/Animator/Assets/SVG`), `Serialisation/Animation`, the `.xaf`
   files, and the `Xaf` prefix (types are `Rig*` now). The stand-in's and
   contact's skeleton, clips, chains and part placements were carried over
   by a one-off conversion; their art was not.
7. **Level items.** ✅ 2026-11-22. A placed Xif is a `Deco` (a broad
   "only looks" role, never a grass or flare component): an origin and its
   own Xif tree. The level keeps them in `Decos`, written into XMF chunk 15
   as an origin plus the XIF node list, sharing the map's string table.
   `XifInstancer` is the instancer module (XIF tag 11). The drawer
   (`XifDrawer.DrawNode`) triangulates the geometry once, draws it once
   per copy, and shades it with the node's common shader in a run of its
   own; editor and play draw the same, right after the floor.
8. **One drawer.** ✅ 2026-11-22. `XifDrawer` (Model/Animation) is the only
   way a Xif is drawn: immediate triangles into an `IRectCanvas` each frame,
   batches sorted by layer, then draw order, then the order they were made.
   Plain nodes (`DrawNode`: geometry, instancer, shader) and rigs
   (`DrawRig`: parts on bones with their pose's art, layer and opacity,
   skins bent, chains as pieces or sheets, cloths) go through it. Play's
   puppet, decos and the animator all use it; `XifRigVisuals` (retained
   canvas items) and `IAttachmentVisuals` are gone. A caller lays an
   `XifTint` over a draw (fade, hit flash). The stand-in's look is now in
   `standin.xif`: one part per bone (an edge block behind a fill block, far
   limbs darker), carried over from the old hand-drawn rectangles, so the
   play drawer no longer knows bone widths. Rig particle streams stay a
   side path (`RigParticleStreams`, animator only) until particles are an
   instancer and a simulation. Play passes no chain or cloth simulation
   yet; the stand-in has neither. Stock decos
   live in `Model/Playfields/Decos.cs`: grass (one blade, 40 copies,
   `Game/grass`, which bends tips by `looks.wind`) and a flare
   (a square, `flare.slang`). The editor's Decor tools place them (grass is
   key 0); they select, move, copy, undo and save like any item. The host
   demo has grass along its floor and a flare.
9. **Cloth look.** ✅ 2026-11-22. A cloth draws its node's geometry and
   shader like a chain does: the art's box is stretched over the whole
   grid (left edge on the first column, top edge on the top row), every
   triangle is cut along the grid's cells (`RigClothArt`), and each piece
   bends with its cell's four corners. uv runs 0..1 over the art's box and
   [Fit] gets its half size. `RigCloth.Fill` and the hand-made darker hem
   are gone; a new dress gets a purple sheet plus a darker hem band as
   two shapes. The cloth inspector's ART row opens the XIF editor on it.
10. **XIF editor box.** ✅ 2026-11-22. A host that draws items itself says
   `XifEditorFrame.DrawsItem`; the editor then draws no fills, only lines
   and points, and publishes where it shows the item (`XifEditor.Shown`).
   The animator draws chain art, cloth art and skinned parts in the box
   through `XifDrawer.DrawNode` with their shaders, on the held canvas
   (`AnimatorArt.DrawBoxed`). An alternate art shares its part's shader.
11. **Particles, step 1.** ✅ 2026-11-22. `XifParticles` carries the node's
   own settings, written in XIF with the XPF per-effect codec
   (`XpfBinary.WriteParticle`). The particle editor edits a host's settings
   through its undo (`IParticleEditorHost`: Use, Edit, gestures); the
   library is presets only (< and > load one, STORE puts a copy back, SAVE
   writes the library). `XpfParticle.Version` makes the GPU server resample
   an edited effect's row. A held part's stream draws over the held canvas,
   so it is no longer under the editor's dimming. Gameplay bursts still
   play library effects by name.
12. **One keyed model (Core/Keys).** ✅ 2026-11-22. The animator's keys
   moved into Core so every timeline keys the same thing: `Keyframe`
   (time, value, `KeyEase`, tangents), `KeyChannel` (a `KeyAddress` and its
   keys; `Sample` and `SampleHeld`), and `KeyClip` (duration, loop, channels,
   and a `KeyClock`: the clip's own seconds, or the song's). An address is
   node id (relative to the animated Xif, "" for itself), part (an element
   inside a module, such as a bone) and field name. Modules say what can be
   keyed through `IKeyable`: a `KeyField` is a name, a value kind (number,
   angle, switch, index) and how keys lay over the node's value (replace,
   add, multiply). `RigSkeleton` lists its bones' fields, `RigAttachment` a
   part's. `RigClip` is a `KeyClip` plus events and limb targets;
   `RigChannels` reads a channel as bone or part and rig property, for rig
   code. XIF writes clips as module tag 15 (`KeyedClips`: node, part and
   field as strings); tag 4 still reads, into the same addresses. Level
   groups will be `KeyClip`s on the song clock over the nodes they hold.
13. **Rows and groups.** ✅ 2026-11-22. `KeyRows` (Core/Keys) is how every
   key timeline reads a clip: which targets have keys, which declared
   fields, at what times; the animator's rows go through it, and field
   order and row labels come from `KeyField`. Level groups are back as
   data: `GroupDocument` (Model/Levels) holds item ids (decos' Xif ids,
   kept unique by `LevelItemIds`) and a `KeyClip` on the song's beats
   (`KeyClock.Song`; keys stay on the beat through timing edits). A
   channel to node "" keys every member, one to a member's id only that
   one. `LevelItemFields` declares what groups key on any placed item
   (move x/y and turn add, size and alpha multiply) and works out each
   item's `GroupLayer` at a beat; decos draw with it, in the editor and in
   play. XMF chunk 16 holds groups; `KeyBinary` is the one key-channel
   codec, used by XIF clips and XMF groups. The host demo has two groups.
14. **Group UI.** ✅ 2026-11-22. The editor's GROUPS card makes a group
   from the selected decos, adds and removes members, sets a loop, and keys
   each `LevelItemFields` field at the playhead in beats (one undo step per
   drag); a muted toggle keys one member only. The gold strip above the
   notes shows the picked group's keys (every field) and retimes them all.
   Field rows: over the footer's top edge, one row per field the group keys
   (`GroupKeys.FieldRows` = `KeyRows.Fields` over `LevelItemFields.All`, in
   declared order, labelled by `KeyField.Shown`); a row's diamonds are that
   field's keys alone, and dragging one retimes only that field
   (`KeyClip.MoveKeys` takes a field). In one-member mode the rows are the
   member's own, in orange.

### Not built yet

- Deco settings: no panel yet for a deco's instancer (count, area, seed) or
  shader params, and no way to place a promoted Xif or one drawn in the
  animator; the tools place the stock ones.
- Decos draw only their root node's geometry; children, clips and binds on
  a deco are kept and saved but not drawn.
- The Simulation module and particles as Instancer + Simulation.

- Clips on child nodes (the lantern's own sway): the runtime plays only the
  root's clips, and there is no `Clip` channel yet.
- Authoring nesting: parts can hang under parts in the model, file and
  runtime, but the animator has no way to reparent a part under another.
- The hierarchy panel is an indented parts list, not a tree editor.

Tests are left alone while this is mid-prototype; only game projects build.

## Not touched

- UI icons (`Assets/Icons/*.svg`) and the window icon (`Assets/icon.svg`)
  keep loading through `SvgRaster` in `HostTextures`. That is a separate
  concern; only SVG as art goes.
