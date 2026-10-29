# The XIF editor

One editor for `Xif` items, shared by every tool that holds them. The animator
hosts it for rig effects; the level editor is meant to host it for notes.

## Pieces

| Piece | Where | What it does |
| --- | --- | --- |
| `XifEditing` | `Core/Items/XifEditing.cs` | The edits themselves: rename, add/remove polygons, insert/remove/move points, fill, texture, shader. Pure, no UI. |
| `IXifEditorHost` | `UI/Ecs/Ui/XifEditorComponents.cs` | What a tool supplies: the item, undo, allowed shaders, texture check. |
| `XifEditorPanel.Build` | `UI/Ecs/Ui/XifEditorPanel.cs` | Builds the panel (UiNode) on a layer, parked off screen. |
| `XifEditorSystem` | `UI/Systems/Ui/XifEditorSystem.cs` | Shows the panel while the host has an item, runs its buttons and fields, drags points. |

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
   - `CanLoad(path)`: whether a texture path resolves, for the status line.
2. Build the panel: `XifEditorPanel.Build(layerNode, center, host)`.
3. Add `new XifEditorSystem(pointer, visuals)` to the tool's system group.

`Tools/Animator/Commands/AnimatorXifHost.cs` is the reference host.

## Rules the editor keeps

- An item keeps at least one primitive; a polygon keeps three points.
- Points snap to whole units while dragged; Shift drags freely.
- Fill is typed as `#rrggbb` or `#rrggbbaa`; anything else is refused.
- A shader the host does not offer shows as `NOT ALLOWED HERE`.

## Not yet

- Editing `Params` (tuned shader uniforms).
- SVG primitives: they are listed but have no editable geometry.
- Adding a point by clicking an edge; points are inserted with `+ PT`.
