# A fluent UI tree API

**Status:** in use. Every game screen builds through `UiNode` and the design
stage; the editor partials are the last holdouts. `UiType`/`UiMetrics` and
`UiStage` landed, and `SceneUi`'s construction helpers are deleted. The API
lives in `UI/Ecs/Ui/UiNode*.cs`, `UiSurface.cs`, `UiStage.cs`, `UiEdge.cs`,
`UiDropdownNode.cs`; scene-level sugar in `Scenes/UiNodeActions.cs`.

---

## 1. What is clunky today

The ECS under the UI is fine. The *construction* API forces the caller to know
things the caller should not have to know.

### 1.1 Two return types, so nothing chains

Containers return `UiBuilder`; leaves return `Entity`.

```csharp
ui.Container(Vector2.Zero, new Vector2(300, 36), StackDirection.Horizontal,
    color: Palette.SurfaceInset);
ui.Current.AddComponent(new UiBorder(Palette.BorderStrong, 1));
```

`ui.Current` is the only way to reach the thing you just made, so a container's
own styling can never be part of the call that creates it. There are **49
`ui.Current` reads** in `Scenes/`, `Systems/`, `Editor/` and `UI/`.

### 1.2 `End()` is a hand-counted stack

Containers push; `End()` pops. Nothing checks the pairing: a missing `End()`
silently puts the next widget in the wrong parent, an extra one silently
climbs out of a panel.

```csharp
ui.Container(/* … */);
    ui.Container(/* … */);
        ui.Container(/* … */);
            …
        ui.End();          // which container closed? was one missing?
    ui.End();
ui.End();
```

There are **55 `Container(` opens and 65 `End()` calls** across the same files
(nesting helpers make the counts diverge, which is exactly the problem).
`EditorScene.Chrome.EdgeGroup` has to document "Close it with `End`".

### 1.3 Parameter soup

```csharp
ui.Container(
    Vector2.Zero,
    new Vector2(4, EditorShellMetrics.HeaderHeight),
    StackDirection.Horizontal,
    spacing: EditorShellMetrics.HeaderGap,
    padding: HeaderPad,
    color: Clear,
    autoWidth: true);
```

15 parameters, 6 of them booleans. The `new Vector2(4, …)` is a dummy size and
`padding: HeaderPad` is a *centering shim* — the container is 80 tall so 15px of
padding makes a 50px control sit in the middle. `.Pad(15)` would say that out
loud, and `Cross(Center)` says it without the shim.

### 1.4 ECS plumbing in scene code

The builder makes the entity; the caller then decorates it by hand:

```csharp
Entity button = ui.RectEntity(Vector2.Zero, size, SceneUi.ButtonColor, label, takesInput: true);
button.AddComponent(new TextStyle(fontSize, SceneUi.TextColor));
button.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));
AddEditorAction(button, kind);
```

**168 `AddComponent` calls in `Scenes/`**, and 42 of them are the same
anchor / tooltip / command / layout-ignore triple.

### 1.5 Six ways to make a button

`UiBuilder.Button`, `SceneUi.Button`, `SceneUi.ButtonEntity`,
`SceneUi.CarouselButton`, `EditorScene.AddEditorButton`,
`EditorScene.AddEditorButtonInContainer`. The last two differ only in their
parameter name.

### 1.6 Guessed geometry, everywhere

**290 `new Vector2(` literals in `Scenes/`**, most of them a width and a height
that exist only because the API demands one, even inside auto-sized stacks.
Text boxes are sized by eye (`new Vector2(480, 16)`) when `UiTextMetrics`
already estimates them.

---

## 2. The architecture

Three rules. Everything else follows.

### 2.1 Rule 1 — a node is the handle

`UiNode` wraps one `Entity`. It converts implicitly to `Entity`, so every
system, record and component that takes an entity keeps working unchanged.
Creation returns the node, so what you build is what you keep:

```csharp
UiNode level = info.Text("Untitled", 22, SceneUi.AccentColor);
// later, from a system:
level.SetText("Night Drive");
```

### 2.2 Rule 2 — children belong to the node they are called on

No cursor, no stack, no `End()`. A parent is named once and then extended:

```csharp
UiNode page   = UiNode.Layer(store, root, 960).Page(screen);
UiNode header = page.Row().Size(screen.X, 80);
UiNode left   = header.Edge(UiEdge.Left, gap: 12).Pad(15);
```

`left.Text(...)` is a child of `left`, always — it can never land in "whatever
was opened last", because there is no last. Adding to a subtree later is
`panel.Text(...)`, and continuing an entity built elsewhere is
`UiNode.Wrap(entity)`.

### 2.3 Rule 3 — style chains after creation

Every mutator edits the entity's components, marks them dirty, and returns the
node:

```csharp
left.IconButton("UN", undoIcon)
    .Size(50)
    .Fill(Palette.Muted)
    .Tooltip("Undo")
    .With(new EditorButton(EditorButtonKind.Undo));
```

`With<T>`, `Tag<T>`, `Without<T>` are the escape hatches, so tool-specific
state (`SceneCommand`, `EditorButton`, `SongSelectTile`, …) never needs the
builder to know about it.

### 2.4 Why not `using` scopes?

The obvious alternative is `using var row = ui.Row()` with `Dispose` popping
the stack. It removes `End()` but needs two types (a leaf must not dispose a
scope), and out-of-order or escaped scopes are still possible. Explicit parents
have neither failure mode, and they make the tree's shape visible at the call
site. The allocation is the same; only the syntax differs.

---

## 3. The API

**Structure** (each creates under the node it is called on)

| Call | Result |
| --- | --- |
| `UiNode.Layer(store, parent, z)` | the layer, as a node |
| `.Page(screen)` | full-screen vertical root |
| `.Column()` `.Row()` | stacks, sized to content by default |
| `.Grid(columns, gap, rowGap)` | grid, row by row |
| `.Panel(fill?)` | themed column: padding, spacing, surface fill |
| `.Edge(UiEdge.Left, gap)` | group hugging an edge, out of the parent's flow |
| `.Scroll(size, contentHeight, out content)` | clipped scroll viewport |
| `.Wrap(entity)` | continue an existing entity |

**Content**: `.Text` `.Button` `.IconButton` `.Rect` `.Image` `.Shape`
`.Dropdown`

**Geometry**: `.Size(w, h)` `.Width(w)` `.Height(h)` `.Box(rect)` `.Cover(screen)`
`.At(point)` `.Move(delta)` `.AutoWidth()` `.AutoHeight()` `.AutoSize()`

**Stack**: `.Gap(f)` `.Pad(f)` `.LineGap(f)` `.Skew(rad)` `.Main(...)`
`.Cross(...)` `.Columns(n)` `.Clip()` `.AnchorTop()` `.AnchorLeft()`

**Placement**: `.Anchor(UiEdge)` `.Anchor(anchor, pivot)` `.IgnoreLayout()`

**Look**: `.Fill(color)` `.Border(color, w)` `.Chamfer(px)` `.Raised()`
`.Below()` `.Overlay()`

**Text**: `.Font(px)` `.TextColor(c)` `.Bold()` `.Align(TextAlign)`
`.Glow(c, r, i)` `.Outline(c, w)` `.SetText(s)`

**Behaviour**: `.Tooltip(s)` `.Input(bool)` `.Disabled(bool)` `.With<T>(c)`
`.Tag<T>()` `.Without<T>()` `.Untag<T>()`

---

## 4. Before / after, on real code

### 4.1 The header's edge groups — `EditorScene.Chrome`

Before (a helper plus three `End()` calls plus tag/component fiddling):

```csharp
private static Entity EdgeGroup(UiBuilder ui, Vector2 anchor)
{
    ui.Container(
        Vector2.Zero,
        new Vector2(4, EditorShellMetrics.HeaderHeight),
        StackDirection.Horizontal,
        spacing: EditorShellMetrics.HeaderGap,
        padding: HeaderPad,
        color: Clear,
        autoWidth: true);
    Entity group = ui.Current;
    group.AddTag<LayoutIgnore>();
    group.AddComponent(new UiAnchor(anchor, anchor));
    return group;
}

EdgeGroup(ui, new Vector2(0, 0.5f));
    … children …
ui.End();
EdgeGroup(ui, new Vector2(1, 0.5f));
    Entity file = ui.Button("FILE ▾", …);
    Entity songMenu = ui.Button("SONG ▾", …);
    Entity menu = ui.Button("MENU ▾", …);
ui.End();
```

After (`Tests/Editor/EditorHeaderFluentTests.cs`, which passes the existing
chrome assertions):

```csharp
UiNode left = header.Edge(UiEdge.Left, EditorShellMetrics.HeaderGap).Pad(Pad);
left.Column()
    .Size(EditorShellMetrics.NameWidth, Control)
    .Fill(SceneUi.PanelColor)
    .Text("LEVEL EDITOR", UiTheme.HeadingFontSize, SceneUi.AccentColor);
left.IconButton("UN", IconDir + "undo.svg", Control, SceneUi.ButtonColor)
    .With(new EditorButton(EditorButtonKind.Undo));

UiNode right = header.Edge(UiEdge.Right, EditorShellMetrics.HeaderGap).Pad(Pad);
right.Button("FILE ▾", UiTheme.FontSize, SceneUi.MutedButtonColor)
    .Size(EditorShellMetrics.DropdownWidth, Control);
```

No `End`, no `Current`, no `AddComponent`, no dummy geometry, no anchor vector
written by hand.

### 4.2 A decorated button — `SceneUi.ButtonEntity` / `AddEditorButton*`

Before:

```csharp
Entity button = builder.RectEntity(center, size, normal, label, takesInput: true);
button.AddComponent(new TextStyle(fontSize, TextColor));
button.AddComponent(ButtonVisual.Create(normal));
button.AddComponent(new SceneCommand(action));
```

After (one call; `.On` is a five-line extension in `Scenes/`):

```csharp
parent.Button(label, 18, normal).On(action);
```

### 4.3 A panel — `EditorScene.Metadata`

Before:

```csharp
panelUi.Container(
    UiLayout.Hidden,
    new Vector2(MetadataPanelWidth, MetadataPanelHeight),
    StackDirection.Vertical,
    spacing: 5,
    color: SceneUi.PanelColor,
    takesInput: true);
Entity panel = panelUi.Current;
```

After:

```csharp
UiNode panel = tools.Panel().Size(MetadataPanelWidth, MetadataPanelHeight)
    .Gap(5).At(UiLayout.Hidden);
```

### 4.4 A list row — `SongSelectScene.List`

Before: open a container, take `ui.Current`, then four `AddComponent` calls and
two text helpers, then `End()`.

After:

```csharp
UiNode tile = list.Row().Size(RowSize).Gap(RowGap).Pad(RowPadding)
    .Fill(Colors.Black).Main(StackMainAlignment.Start).Input()
    .With(new SongSelectTile(-1))
    .Chamfer(RowChamfer)
    .Border(Fade(RowBorderColor, RowOpacity), RowBorder)
    .With(ButtonVisual.Create(Colors.Black));

UiNode name = tile.Text("", NameSize, Colors.White);
UiNode song = tile.Text("", SongSize, Gold)
    .Glow(new Color(Gold, 0.6f), radius: 8, intensity: 1.4f);
```

---

## 5. Defaults that moved (on purpose)

| | `UiBuilder` | `UiNode` | Why |
| --- | --- | --- | --- |
| stack container fill | `SurfaceAlt` | transparent | structural rows should not paint; `.Fill()` says so |
| stack container size | fixed, caller-supplied | auto, fits content | removes dummy sizes |
| main alignment | `Center` | `Start` | matches nearly every call site; irrelevant for auto-size |
| text box | caller-supplied | measured from `UiTextMetrics` | removes eyeballed sizes; `.Size()` pins |
| buttons | caller-supplied size | measured label + 24 × `ButtonHeight` | same |
| `Panel` | explicit everything | panel fill, padding, spacing | the common case |
| `Size()` on an auto container | – | also clears `AutoWidth/AutoHeight` | `Size` cannot be silently overridden by layout |

Migration can pin any dimension with `.Size()` / `.Box()` first and drop the
pin later where the design allows, so the layout diff is controlled.

---

## 6. Migration status

Every screen below builds through `UiNode` and reads `UiStage`; none takes a
screen size.

| Screen | Notes |
| --- | --- |
| Settings, Profiles, Main menu, Offset wizard | flat layouts, rows/columns and tokens |
| Song timing, Results | live labels pinned, action rows, stat card |
| Pause, Confirmation, Transitions | runtime overlays built inside `SceneSystem` |
| Play HUD | HUD positions kept as drag-and-save layout data |
| Song select | all eight partials: actions, chrome, info, list, player card, preview, scores |

Remaining: the editor partials (`EditorScene.Chrome`, `.Sidebar`, `.Metadata`,
`.Groups`) still use `UiBuilder` and `SceneUi.Text`, and their helpers cross
files (`HudText`, `SidebarLabel`), so they move together.

Cleanup already done: `SceneUi`'s construction helpers (`Background`, `Panel`,
`Label`, `Button`, `ButtonEntity`, `CarouselButton`, `ScrollView`) are gone.
Only layer ids, palette aliases, `Layer` and `Text` remain for the editor.

---

## 7. Rules that emerged

1. **Measure static text, pin live text.** A measured box clips when a system
   later writes a longer string (`BPM 120` → `BPM 1200`), so labels a system
   rewrites keep an explicit `Size`.
2. **HUD positions are data.** The play HUD's coordinates are `HudLayout`
   entries the player can drag and save; they stay explicit rather than stacked.
3. **Screens come from the stage.** `Page()`, `Cover()`, `Stage.Center` and
   `Stage.Bounds` replace per-scene screen math; no scene takes a screen size.
4. **Identity in the signature, decoration chained.** `Text(value, size, colour,
   align, bold)`; glow, outline, tooltips chain.
5. **Tokens for the long tail.** `UiType` and `UiMetrics` cover type, control
   heights, space, borders and radii. Numbers that remain are named scene
   constants (card width, HUD layout), never inline literals.
6. **Measuring needs slack.** The shipped font is measured against
   `UiTextMetrics`; boxes include the canvas's 4px insets and an 8% margin,
   because a box one glyph short silently drops that glyph.
7. **Transition states must be temporary.** Song select briefly bridged
   `UiBuilder` and `UiNode` through `UiNode.Wrap(ui.Current, ui.Visuals)`. It
   exposed a real bug — Wrap ignored the caller's visual factory, crashing tests
   that pass a fake one — and is gone.

---

## 8. What the prototype proves

* Children always land under the node they were called on.
* Chained calls edit components in place and dirty exactly what changed
  (container + parent `LayoutDirty`, `Dirty` for looks, `Moved` for geometry).
* `Size()` beats auto sizing; `Width()` leaves the height automatic.
* `Edge()` produces the old `EdgeGroup` structure (anchored, ignored by flow,
  content-sized).
* A tree built with `UiNode` lays out **identically** to the same tree built
  with `UiBuilder` — asserted entity by entity in
  `UiNodeTests.AFixedContainerLaysOutLikeTheOldBuilder`.
* The editor header rebuilt fluently passes the chrome tests' geometry
  assertions (`menu.End.X == screen.X - 15`, name at `x == 15`, undo/redo order).

## 9. Open questions

Resolved since the proposal: measured text by default (yes, with the insets and
margin above), left alignment as the default, `Start` as the default main
alignment.

Still open:

1. **Naming** — `UiNode.Layer(...)` / `UiNode.Wrap(...)` vs a separate
   `Ui.Layer(...)` entry type. `Ui` collides with the `Xanadu.Systems.Ui`
   namespace, hence the static on the node.
2. **Dropdown styling** — `dropdown.Button.Size(...)` today because extension
   methods do not apply through the implicit conversion. Acceptable, or give
   `UiDropdownNode` a `Size`/`Fill`/`At` passthrough?
3. **Delete `UiBuilder`'s public surface** after the editor moves, or keep it as
   the low-level factory? `SceneUi.Layer` still returns it for the editor.
4. **`SceneUi.SetText` and the colour aliases** — fold into `UiText`/`Palette`
   once the editor is off them.
