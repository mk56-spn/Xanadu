# UI best practices, by example

The house style for ECS UI: the model, then one small tool built with it.
Atlas (`Tools/DevTools/Atlas/`) is the full-size reference for a tool, and the
song timing screen (`Game/Scenes/SongTimingScene.cs`) for a game screen with a
controller: both are built exactly this way. The rules are also in `agents.md` (UI). The specs live in
`UI/Ecs/Ui/Tree/`, the node calls in `UI/Ecs/Ui/UiNode*.cs`.

**If the library is missing something you need, ask before adding it.** Do not
hard code around the gap with hand-placed rects, raw canvas draws or your own
pointer reading. Every piece below started as such a gap.

## The model

UI is ECS entities. You **write** it as a tree of specs (`Xanadu.Ui.Tree`), so
the code has the shape of the screen, the way osu!lazer's drawables do; you
**change** it afterwards through the nodes those specs built (`UiNode`).

1. **What a node is and how it is laid out go in its constructor**, as named
   arguments, so most specs fit on one line:
   `new Label("ATLAS", UiType.Heading, ink: Ink.Accent)`,
   `new Button("BACK", tone: Tone.Secondary, cmd: Cmd(AtlasCommand.Back), tip: "…")`.
2. **A stack's children go in the braces after it:**
   `new Row(gap: Space.Sm) { a, b, c }`, added with `parent.Add(spec)`. Nesting is
   the tree. A sequence (`Select`, a helper's results) drops in among them, and a
   `null` (a child left out by a condition) is skipped.
3. **Rare settings are initializer properties** on leaves (`{ Hidden = true }`,
   `Free`, `MinWidth`, `MaxWidth`, `Style`). A stack's braces hold its children and
   C# can't mix the two, so stacks take `free:`, `hidden:` and `style:` in their
   constructor instead.
4. **Repeated shapes are presets.** A `Flex` (grow, cross, stretch) given once
   and passed first: `static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch)`,
   then `new Column(Fill, gap: Space.Md) { … }`. The stack's own arguments win.
5. **Keep a spec to reach its node.** `(_back = new Button("BACK", …))` in the
   braces, then `_back.Node` after the build: a spec only describes, and `Node` is
   the `UiNode` it made. A spec converts to `UiNode` and `Entity` wherever one is
   wanted. (In a collection initializer an assignment needs its brackets, or it
   reads as a property.) **A spec has no node until the tree is built**, so never
   turn one into an `Entity` early: a record or component filled with specs inside
   a row template or a helper holds null entities. Keep the specs, and read their
   nodes after `Add` (the song timing screen's `ChangeRowParts`).
6. **Change with the node.** `_back.Node.Chosen(on)`, `.SetText(s)`, `.Hidden()`:
   every fluent call edits components in place, marks them dirty and returns the
   node. `With<T>`, `Tag<T>`, `Without<T>` and `Untag<T>` carry a tool's own state.
7. **Compose with your own specs.** A piece used more than once (a card, a chip)
   is a class deriving `UiSpec`, taking its own constructor arguments, whose
   `Create` adds its own tree and exposes the parts a fill needs, like a lazer
   composite drawable. Atlas's `TypeCard`, `BigCard` and `KindChip`
   (`Tools/DevTools/Atlas/AtlasCards.cs`) are the pattern.

Short names keep lines short without hiding where things come from: `Cross.*`,
`Main.*` and `Align.*` stand for the long alignment enums, `Ink` is a text role,
`Tone` a fill role, and `Cmd(…)` (`using static Xanadu.Ui.Tree.UiSpecs;`) is
`UiCommand.Of`.

Specs exist only while building; the entities are the UI, and systems never see
a spec. Anything the specs don't cover yet goes through `new Custom(p => p.…)`
with the fluent calls, and anything missing from both is a library gap: ask.
`UiBuilder` is the low-level factory underneath; only the F2 shader tuner calls
it directly.

### Specs at a glance

| | Specs (`Xanadu.Ui.Tree`) |
| --- | --- |
| Stacks | `Column(flex, gap:, pad:, main:, cross:, clip:, …) { children }`, `Row(…) { … }`, `Panel(…) { … }`, `Spacer(grow)` |
| Content | `Label(text, type, ink:, align:, fit:, bold:, mono:)`, `Button(label, type, tone:, cmd:, tip:)`, `Box(width:, size:, category:, tone:)`, `Meter(share, size:)`, `Field(value, type, numeric:, size:, width:, height:)`, `Connector(from, to, category)`, `PooledList(rowHeight, gap, changed:, width:, height:, grow:) { Row = slot => … }`, `Custom(build)` |
| On most specs | `width:` `height:` `size:` `grow:` `stretch:` `tip:` (and `tone:` `cmd:` where they make sense) |
| Rare | `{ Free, Hidden, MinWidth, MaxWidth, Style }` on leaves; `free:` `hidden:` `style:` on stacks |
| Roles | `Tone.Primary/Secondary/Quiet/Danger/Solid/Backing`, `Ink.Normal/Dim/Accent` |

### Node calls at a glance (changing what is built)

| | Calls |
| --- | --- |
| Roots | `UiNode.Layer(store, parent, UiLayers.X)`, `.Page()`, `.Cover()`, `UiNode.Wrap(entity)` |
| Look (roles) | `.Primary()` `.Secondary()` `.Quiet()` `.Danger()` `.Chosen(on)` `.Solid()` `.Backing()` `.AccentText()` `.DimText()` `.CategoryTone(i)` `.CategoryFill(i)` `.CategoryText(i)` `.Theme(roles)` |
| Text | `.SetText(s)` `.Fit()` `.Align(...)` `.Bold()` `.Mono()` |
| Geometry and flex | `.Size(w, h)` `.Width(w)` `.Height(h)` `.Grow(w)` `.Stretch()` `.IgnoreLayout()` `.Anchor(UiEdge)` |
| Behaviour | `.Tooltip(s)` `.Input()` `.Disabled()` `.Hidden()` `.With(UiCommand.Of(...))` |
| Lists, lines, meters | `UiLists.SetCount / Reveal / Row / Pool / OnChanged`, the `UiList` component (`First`, `Shown`, `Count`), `UiConnectors.Join`, `UiMeters.Set` |
| More structure | `.Grid(n)` `.Edge(UiEdge.X)` `.Collapsible(...)` `.Dropdown(...)` `.IconButton` `.Image` `.Shape` (no spec yet: use `Custom`) |

### Defaults

- Stacks have no fill, size to their children and start at the top/left
  (`Main(Start)`); a `Tone` says when a stack should paint.
- Text and buttons are measured from `UiTextMetrics` (with insets and slack, as
  a box one glyph short drops that glyph); a size pins one. Fitted text asks
  for no width at all: layout gives it its room and the draw cuts it.
- A fixed `Size` on an auto-sized node cancels the auto sizing; `Width` leaves the
  height automatic.
- `Panel` brings the panel fill, padding and spacing.
- Layers, sizes and type: `UiLayers.*`, `UiMetrics.*`, `UiType.*`. Author
  against the 1920×1080 `UiStage`; the host scales it, so a screen never reads
  the window size.

## The example: a type browser tab

A tool tab with a header of view buttons, a budget meter, a search field, a
long list of types, and a card for the picked type joined to its neighbours by
lines. The numbers in the comments are explained in the table below it.

```csharp
internal sealed class BrowserStage : IToolStage
{
    // Named sizes only for the tool's own design; type and control sizes come from UiType / UiMetrics.
    private const float ListWidth = 420;
    private const float CardWidth = 300;
    private static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch);

    private enum BrowserCommand { View, Pick }

    private readonly Button[] _views = new Button[2];
    private readonly List<NameCard> _rows = new();
    private Field _search = null!;
    private PooledList _list = null!;
    private Label _cardName = null!;
    private IDisposable? _presses;
    private List<int> _ids = new();
    private bool _changed = true;

    // 1. Build once into the body the shell hands over. No canvas, no area, no pointer.
    public void Build(UiNode body)
    {
        body.Add(new Column(Fill, gap: Space.Md)
        {
            Header(),
            (_search = new Field(size: new Vector2(ListWidth, Control.Sm))),        // 3
            new Row(Fill, gap: Space.Xl) { TypeList(), Chart() },
        });

        _presses = UiCommandEvents.OnPressed<BrowserCommand>(body.Entity.Store, Pressed);   // 7
    }

    // 2. Containers place everything: grow / stretch / width, never coordinates. Roles, never colours.
    private Row Header() => new(gap: Space.Sm)
    {
        new Label("BROWSER", UiType.Heading, ink: Ink.Accent),
        (_views[0] = new Button("ALL", cmd: Cmd(BrowserCommand.View, 0), tip: "every type")),
        (_views[1] = new Button("USED", cmd: Cmd(BrowserCommand.View, 1), tip: "only types something uses")),
        new Spacer(),
        new Meter(226f / 255, size: new Vector2(110, Space.Sm)),
    };

    // 4. Long lists are pooled: only the rows that fit exist, refilled from the data when the list changes.
    private Column TypeList() => new(width: ListWidth, cross: Cross.Stretch)
    {
        (_list = new PooledList(Control.Xs, Space.Xs, changed: FillList, grow: 1)
        {
            Row = _ => [Remember(_rows, new NameCard(BrowserCommand.Pick, grow: 1))],
        }),
    };

    private Column Chart() => new(grow: 1, main: Main.Center)
    {
        new Column(width: CardWidth, pad: Space.Md, tone: Tone.Solid)
        {
            (_cardName = new Label("", UiType.Section, fit: true)),                  // 5
        },
        // 6. Lines: new Connector(rowCard, _cardName, category), added before what they join.
    };

    // A composite spec: one class, its own little tree, the parts a fill needs.
    private sealed class NameCard(BrowserCommand press, float grow = 0) : UiSpec(grow: grow)
    {
        private readonly Label _name = new("", UiType.Body, grow: 1, fit: true);
        public UiNode Name => _name.Node;

        protected override UiNode Create(UiNode parent) => parent.Add(new Row(cmd: Cmd(press)) { _name }).Node;
    }

    // Presses only change state; Update shows it after the UI has run.
    private void Pressed(BrowserCommand command, UiCommand press, Entity _) => _changed = true;

    public void Update(in ToolFrame frame)
    {
        if (!_changed)
            return;
        _changed = false;

        _views[0].Node.Chosen(true);                       // bright blue only for what is on
        _views[1].Node.Chosen(false);
        UiLists.SetCount(_list, _ids.Count);               // the list announces; FillList refills
    }

    private void FillList()
    {
        UiList view = _list.Node.Entity.GetComponent<UiList>();
        for (int i = 0; i < view.Shown; i++)
        {
            int id = _ids[view.First + i];
            NameCard card = _rows[i];
            card.Node.Entity.GetComponent<UiCommand>().Arg = id;
            card.Node.CategoryTone(KindOf(id));            // categories, not hues
            card.Name.SetText(NameOf(id));                 // the whole string; fit cuts it
        }
    }

    public void Dispose() => _presses?.Dispose();
}
```

## What each numbered step replaces

| Step | Use | Never |
| --- | --- | --- |
| 1 | `IToolStage.Build(UiNode body)` in a tool tab | a canvas stage that draws in a `Rect2` area |
| 2 | nested `Column` / `Row` with `grow:`, `stretch:`, `width:`, `gap:`, `pad:`, `Spacer`, a `Flex` preset | `new Rect2(724, 34, …)`, `x += width + 6`, a flat run of `page.Row()` statements |
| 2 | `items.Select(item => Row(item))` in the stack's braces | a `foreach` that calls `table.Add(new Row…)` after the initializer |
| 2 | `UiType.*` sizes, `UiMetrics.*` gaps and controls | font sizes and gaps as numbers |
| 2 | `tone:`, `ink:`, `.Chosen(on)`, `.CategoryTone(i)` | `Palette.X` passed per call |
| 3 | a `Field` | reading typed text and drawing `find: {q}_` yourself |
| 4 | `PooledList` + `UiLists.SetCount` / `Reveal` for anything long | thousands of row entities, fixed slots with ▲ ▼ buttons, or your own scroll offsets |
| 5 | `fit: true` on text whose box layout sizes | cutting strings to guessed character counts |
| 6 | `Connector(from, to, category)` | `Canvas.DrawLine` between computed points |
| 7 | `cmd: Cmd(enum, arg)` + `UiCommandEvents.OnPressed<T>` | per-feature button components, `box.HasPoint(_mouse)`, click closures |
| — | `tip: …` | a hand-drawn hint bar driven by hover tests |
| — | `Meter(share)` | rect pairs sized by hand |

## Habits

- **A pooled list owns its scroll.** Row `i` shows item `First + i` (read
  `UiList`); a row's commands and fields carry `i`, and whoever acts adds
  `First`. Keep the list's count in step with the data (`SetCount`, `keepPlace:
  true`), and `Reveal` an item you just added. Rows past the end are hidden, so
  their entities are disabled: a query that must see them (a test counting
  fields, say) needs `WithDisabled()`. A controller that refreshes every frame
  can fill the shown rows there; one that doesn't fills them from `changed:`.

- **State changes in handlers, showing happens in one place.** Press handlers
  set fields and a flag; `Update` (or a system) rebuilds what is shown.
- **The build tells the story.** `Build` reads top-down as the screen does.
  Split a region into its own method (`Header()`, `TypeList()`, `Chart()`, each
  returning its spec) only when `Build` would no longer fit on one screen, or
  when the region is used more than once; a small screen writes its card in
  place. A method called once that just wraps one stack is noise.
- **Name the repeated shape.** When the same named arguments turn up on three or
  more specs, write one small helper that returns that spec and pass in only what
  differs. The song timing screen's `Caption`, `Field`, `Action` and `Readout`,
  and the settings screen's `Setting(label, control)` and `Choice(text, cmd,
  style)`, are the pattern: every row of settings, keybinds included, is one
  `Setting`, so its column width and height are written once.
- **One spec, one line, when it fits.** Keep a spec's arguments on its line up to
  120 columns; break them onto separate lines only past that. A spec spread one
  argument per line hides the tree it belongs to.
- **Measure static text, `Fit` live text.** Labels that never change take their
  measured box; anything a fill rewrites sits in a grown box with `fit: true`.
- **Live text in a measured button:** build it with its longest label (say
  `"PROJECT ▼"`), then `SetText` the shorter one.
- **Hide, don't move.** `UiLayout.SetHidden` swaps views; pooled rows past the
  end are hidden by the list itself.
- **Colours tell kinds apart through `Categories`.** The tool maps its kinds to
  category indices in one place (`KindCategory` in Atlas); the theme owns hues.
- **HUD positions are data.** The play HUD's places are `HudLayout` entries the
  player drags and saves; they stay explicit rather than stacked. That is the one
  place coordinates are right.
- **Verify with one screenshot.** `./run.sh <exe> --shot=<scratch path>`; scratch
  files never go in the repo.
