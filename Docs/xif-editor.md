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
(item units × `xif_unit`), never fractions of the shape.

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
| `IXifEditorHost` | `UI/Ecs/Ui/XifEditorComponents.cs` | What a tool supplies: the item, undo, allowed shaders. |
| `XifEditorPanel.Build` | `UI/Ecs/Ui/XifEditorPanel.cs` | Builds the panel (UiNode) and its backdrop on a layer, closed. |
| `XifEditorSystem` | `UI/Systems/Ui/XifEditorSystem.cs` | `Open` / `Close` / `IsOpen`; while open and the host has an item, shows it and runs its buttons, fields, colour picker and the shape box. |

The editor never owns the item. It asks the host for it every frame, so an
undo that swaps the item for a snapshot is picked up without telling it.

## Hosting it

1. Implement `IXifEditorHost`:
   - `Item`: the item to edit now, or `null` to hide the panel.
   - `Edit(change)`: run `change` as one undo step, then redraw.
   - `BeginGesture` / `EndGesture` / `Changed`: a point drag is one gesture;
     `Changed` fires on each move inside it and should only redraw.
   - `Shaders`: the choices the shader button cycles. A map-owned item may
     offer its map's slots; a rig only has common shaders.
   - `Opacity` (optional, default 1): how solid the editor draws, asked every
     frame. The panel, backdrop and fills fade with it; the field lines
     (outline, points, origin) stay solid.
   - `ShaderParams(item)` (optional): the settings the item's shader takes.
     By default a common shader is compiled and asked
     (`HostPlatform.DescribeMaterial`); values the drawer sets (`xif_*`) are
     left out. A host whose items use map shader slots answers for those.
   - `Placement` (optional, default null): item space to screen, asked every
     frame. Set, the field lines sit there and points drag there, e.g. on the
     bone a part rides, and the editor draws no fills: the host draws the item
     there through its shader. Null fits the item in the shape box with plain fills.
2. Build the panel on a layer of its own, well above the tool's menus:
   `XifEditorPanel.Build(layerNode, area, host, screen, linesNode)`; it covers
   `area` and dims `screen`. `linesNode` is a second layer above it, clear of
   its overlays, for the field lines, so the panel can fade without them.
3. Add `new XifEditorSystem(pointer, visuals)` to the tool's system group.
4. Open it with `XifEditorSystem.Open(panel)`; while `IsOpen`, keep the
   tool's own pointer and key handling off (the animator lets Esc, Z and Y
   through and closes the editor on Esc).

`Logic/Animator/Commands/AnimatorXifHost.cs` is the reference host. It edits
a part on its bone: opacity 0 (nothing dims the preview), placement = preview
placement × the part's world transform, and since a placed item is the host's
own drawing (shader and all) the editor draws only its field lines over it, and the preview still zooms (wheel) and pans (middle drag)
under the editor. Chain art and skinned parts have no one place, so they stay
solid in the shape box.

## Rules the editor keeps

- An item keeps at least one polygon; a polygon keeps three points.
- Points snap to whole units while dragged; Shift drags freely.
- Fill is picked, or typed as `#rrggbb` or `#rrggbbaa` in the picker's field.
- A shader the host does not offer shows as `NOT ALLOWED HERE`.

## Not yet

- Editing child nodes and modules other than geometry and shader.
- Zooming and panning the shape box (it fits the item, with room round it).
- The level editor hosting it for decos.
- Showing the item through its shader (the box draws plain fills).
