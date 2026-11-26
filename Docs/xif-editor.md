# The XIF editor

One editor for `Xif` items, shared by every tool that holds them. The animator
hosts it to draw parts and chain art; the level editor is meant to host it for level items.

It opens on purpose, over everything (2026-11-22): in the animator, select a
part (or chain) and press **EDIT XIF** in the ART section, or double-click a
limb's art in the preview. The rest of the tool dims and takes no clicks or
shortcuts (Esc and undo/redo still work); **DONE** or Esc closes it. Every
change is already kept, on the tool's own undo.

In its big shape box the item is drawn filled, every shape in its colour:

- drag a point to move it (whole units; Shift for free);
- click an edge of the picked shape to add a point there and drag it on, as
  one undo step (a diamond shows where);
- right-click a point to delete it (a shape keeps three);
- click a shape to pick it, and drag it to move the whole shape.

Shapes come in three kinds (`XifShapeKind`): **+ SHAPE** adds a free outline
edited point by point; **+ QUAD** a rectangle dragged by its corners (the
opposite corner stays put); **+ CIRCLE** an ellipse dragged by its four sides.
A quad or circle keeps its kind: points are not added to or deleted from it.
Every kind keeps its outline as points, so drawing never needs to know.

A shader may tag uniforms `[Fit]`. When it does, **FIT TO SHAPE** shows above
its settings: on, the drawer fills those uniforms' x and y with the shape's
half size, in item units, the same pixel units a hand-set value uses (the
flare's box). Any shape fits, by its bounding box. Sizes in shaders are pixels
(item units × `unit`), never fractions of the shape.

On the right: name; **SHAPES**, one row per shape with a swatch in its colour
(click the row to pick the shape, its swatch to open the colour picker for it
under the list; a picker drag is one undo step), then add and delete; point
(+ / -); the
shader, and under **SETTINGS** the shader's own settings (its uniforms, such as
a flare's box, light, tint and shape) as value fields. A setting the item never
set shows the shader's default; a scrub is one undo step; right-click resets.

## Pieces

| Piece | Where | What it does |
| --- | --- | --- |
| `XifEditing` | `Core/Items/XifEditing.cs` | The edits themselves: rename, add/remove polygons, insert/remove/move points, fill, the node's shader. Pure, no UI. |
| `XifEditorFrame` + signals | `UI/Ecs/Ui/XifEditorSignals.cs` | What a tool supplies (the item, allowed shaders) and the edit signals it records on its undo. |
| `XifEditorPanel.Build` | `UI/Ecs/Ui/XifEditorPanel.cs` | Builds the panel (UiNode) and its backdrop on a layer, closed. |
| `XifEditorSystem` | `UI/Systems/Ui/XifEditorSystem.cs` | `Open` / `Close` / `IsOpen`; while open and the owner has an item, shows it and runs its buttons, fields, colour picker and the shape box (`XifEditorHits`). |

The editor never owns the item. It asks the owner for it every frame
(`XifEditorFrameDue`), so an undo that swaps the item for a snapshot is picked
up without telling it.

## Owning it

1. Build the panel on a layer of its own, well above the tool's menus:
   `XifEditorPanel.Build(layerNode, area, screen, shaders, layers, linesNode)`;
   it covers `area` and dims `screen`. `shaders` are the choices the shader
   picker offers (a map-owned item may offer its map's slots; a rig only has
   common shaders), `layers` the common shaders that may stack over it (none
   hides LAYERS). `linesNode` is a second layer above it, clear of its
   overlays, for the field lines, so the panel can fade without them.
2. Hear its signals on the panel (`AddSignalHandler`):
   - `XifEditorFrameDue`: write `XifEditorFrame` now: `Item` (null hides the
     panel), `Title`, and optionally `Opacity` (default 1: the panel, backdrop
     and fills fade with it; the field lines stay solid), `Placement` (item
     space to screen; set, the field lines sit there, points drag there and
     the editor draws no fills, as the owner draws the item there through its
     shader) and `DrawsItem` (the owner draws the item in the shape box too, at
     `XifEditor.Shown`).
   - `XifEditAsked(change)`: run `change` as one undo step, then redraw.
     It is an `Action` while items are C# objects outside the ECS.
   - `XifGestureStarted` / `XifGestureEnded` / `XifChanged`: a point drag is one
     gesture; `XifChanged` fires on each move inside it and should only redraw.
   The settings under SHADER are the common shader's (`XifShaderParams.Common`);
   values the drawer sets (`xif_*`) are left out.
3. Add `new XifEditorSystem(pointer, visuals)` to the tool's system group.
4. Open it with `XifEditorSystem.Open(panel)`; while `IsOpen`, keep the
   tool's own pointer and key handling off (the animator lets Esc, Z and Y
   through and closes the editor on Esc).

`Logic/Animator/Commands/AnimatorXif.cs` is the reference owner. It edits
a part on its bone: opacity 0 (nothing dims the preview), placement = preview
placement × the part's world transform, and since a placed item is the owner's
own drawing (shader and all) the editor draws only its field lines over it, and the preview still zooms (wheel) and pans (middle drag)
under the editor. Chain art and skinned parts have no one place, so they stay
solid in the shape box.

## Rules the editor keeps

- An item keeps at least one polygon; a polygon keeps three points.
- Points snap to whole units while dragged; Shift drags freely.
- Fill is picked, or typed as `#rrggbb` or `#rrggbbaa` in the picker's field.
- A shader the owner does not offer shows as `NOT ALLOWED HERE`.

## Not yet

- Editing child nodes and modules other than geometry and shader.
- Zooming and panning the shape box (it fits the item, with room round it).
- The level editor hosting it for decos.
- Showing the item through its shader (the box draws plain fills).
