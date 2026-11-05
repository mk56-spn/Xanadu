# Hand-off: render-architecture.md — all steps landed

Updated 2026-11-06. `docs/render-architecture.md` has steps **0–12 ticked**.
Everything is *uncommitted* in `/var/home/dhs/xanadu`, mixed with the earlier
steps' work.

## What is done

- **Step 10, buffered groups.** Buffer tint, alpha, shader (with its own
  `BufferKeys` track) and blend are in the group panel's shader row. The C#
  defaults of `CanvasItemSetCanvasGroupMode` are not the node's: they leave
  `fitEmpty` off, so the group's own command never exists and self modulate is
  ignored. `GodotShaderCanvasItems.SetCanvasGroup` now passes the node's
  `10, true, 10` and a buffered group fades uniformly. The buffer lock's edit
  guards live in `Systems/Editor/EditorBufferGuard` and are used by the layer
  stepper and both group-membership paths; a refusal is a `WarningRequest`.
  Tests: `GroupBufferTests`, `EditorLayersTests`, `editor-groups`.
  Screenshots: `play-buffered-group`, `editor-groups`.
- **Step 11, screen-read regions.** `ScreenReadRegion` is a placed rect with an
  `ItemLayer`; `PlayfieldDrawKind.ScreenRead` refreshes the back buffer where
  the order reaches it via `IRectCanvas.CopyScreen(rect)`. The canvas pools a
  copy item and `CanvasItemSetCopyToBackbuffer` gets render-target pixels
  (viewport `GetFinalTransform`); a self-contained layer copies its whole
  viewport buffer. The editor tool sits in the *Text and screen reads* flyout,
  places on the current layer, draws the ghost and the outline (after the item
  pass), and resizes through `GridPlacement`. Document, XMF (chunk 23 / 18,
  minors 2.6), mapper, undo and copy/paste carry it. Tests:
  `ScreenReadTests`, `ShaderRoutingCanvasTests`; screenshots:
  `play-screen-read`, `editor-screen-read`.
- **Step 12, animator order.** `Animation/AnimatorDrawOrder` states the order
  once (attachment list order, then cloth, chains and effects in bands) and the
  layer panel's row slot and `SvgAttachmentVisuals` both use it, with
  `DrawOrder.Banded` when layers are contained. Tests:
  `AnimatorDrawOrderTests`. **Not built (optional in the plan):** a buffered
  whole character for a uniform fade; the contained path is the base for it.

## Verification

Build green: `dotnet build Xanadu.csproj`, `Tests/Xanadu.Tests.csproj`,
`Tests/VisualTests/Xanadu.VisualTests.csproj`.

Tests per namespace (a single full run aborts in the test host after ~1245 of
~1800 tests in this sandbox — pre-existing):
`Groups` 93, `Editor` 252, `Playfields` 117, `Shaders` 64,
`Animation` 863 pass with the same **7 pre-existing `AnimatorDrawTests`**
failing, `Characters` 47, `UI` 63, `Scenes` 99, `History` 7,
`Physics` 24, `Particles` 4, `Layering`, `Architecture` 3.

Visual tests passing: `play-screen-read`, `editor-screen-read`,
`play-buffered-group`, `editor-groups`, `editor-tool-groups`,
`play-pass-blur`, `play-pass-distort`.

## Working notes

- Godot binary:
  `/home/dhs/Documents/Godot_v4.8-dev7_mono_linux_x86_64/Godot_v4.8-dev7_mono_linux.x86_64`
- Visual test + screenshot:
  `"$GODOT" --path Tests/VisualTests --fullscreen res://VisualTests.tscn -- --visual-test=<slug> --visual-test-shot=/var/home/dhs/xanadu/screenshots/<slug>.png`
- New source files still have untracked `.uid` files (Godot generates them);
  the repo tracks `.uid` for older scripts, so decide whether to commit them.
- The visual harness and `Main` pass their node as `passHost` to
  `XanaduSystemRoot`; play and the editor each get their own
  `GodotPassSurfaces`.
