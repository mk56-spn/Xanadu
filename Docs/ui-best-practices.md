# UI best practices, by example

The house style for ECS UI: the model, then one small tool built with it.
Atlas (`Tools/DevTools/Atlas/`) is the full-size reference: it is built exactly
this way. The rules are also in `agents.md` (UI). The specs live in
`UI/Ecs/Ui/Tree/`, the node calls in `UI/Ecs/Ui/UiNode*.cs`.

**If the library is missing something you need, ask before adding it.** Do not
hard code around the gap with hand-placed rects, raw canvas draws or your own
pointer reading. Every piece below started as such a gap.

## The model

UI is ECS entities. You **write** it as a tree of specs (`Xanadu.Ui.Tree`), so
the code has the shape of the screen, the way osu!lazer's drawables do; you
**change** it afterwards through the nodes those specs built (`UiNode`).

1. **Build with initializers.** `new Column { Gap = …, Children = { new Label(…),
   new Button(…) { Command = …, Tooltip = … } } }`, added with `parent.Add(spec)`.
   Nesting is the tree. Settings are named properties, children are listed in
   order, a sequence (`Select`, a helper's results) drops in among them, and a
   `null` (a child left out by a condition) is skipped.
2. **Keep a spec to reach its node.** `(_back = new Button("BACK") { … })` inside
   `Children`, then `_back.Node` after the build: a spec only describes, and
   `Node` is the `UiNode` it made. A spec converts to `UiNode` and `Entity`
   wherever one is wanted. (In a collection initializer an assignment needs its
   brackets, or it reads as a property.)
3. **Change with the node.** `_back.Node.Chosen(on)`, `.SetText(s)`, `.Hidden()`:
   every fluent call edits components in place, marks them dirty and returns the
   node. `With<T>`, `Tag<T>`, `Without<T>` and `Untag<T>` carry a tool's own state.
4. **Compose with your own specs.** A piece used more than once (a card, a chip)
   is a class deriving `UiSpec` whose `Create` adds its own tree and exposes the
   parts a fill needs, like a lazer composite drawable. Atlas's `TypeCard`,
   `BigCard` and `KindChip` (`Tools/DevTools/Atlas/AtlasCards.cs`) are the pattern.

Specs exist only while building; the entities are the UI, and systems never see
a spec. Anything the specs don't cover yet goes through `new Custom(p => p.…)`
with the fluent calls, and anything missing from both is a library gap: ask.
`UiBuilder` is the low-level factory underneath; only the F2 shader tuner calls
it directly.

### Specs at a glance

| | Specs (`Xanadu.Ui.Tree`) |
| --- | --- |
| Stacks | `Column` `Row` `Panel` (`Children`, `Gap`, `Pad`, `Main`, `Cross`, `Clip`), `Spacer` |
| Content | `Label(text, size)` (`Role`, `Align`, `Fit`, `Bold`, `Mono`), `Button(label, size)`, `Box` (`Category`), `Meter(share)`, `Field(value, size)`, `Connector(from, to, category)`, `PooledList(rowHeight, gap)` (`Row`, `Changed`), `Custom(build)` |
| On every spec | `Width` `Height` `Size` `Grow` `Stretch` `MinWidth` `MaxWidth` `Free` (out of the flow) `Hidden` `Tone` `Command` `Tooltip` `Style` (any fluent chain) |
| Roles | `Tone.Primary/Secondary/Quiet/Danger/Solid/Backing`, `TextRole.Normal/Dim/Accent` |

### Node calls at a glance (changing what is built)

| | Calls |
| --- | --- |
| Roots | `UiNode.Layer(store, parent, UiLayers.X)`, `.Page()`, `.Cover()`, `UiNode.Wrap(entity)` |
| Look (roles) | `.Primary()` `.Secondary()` `.Quiet()` `.Danger()` `.Chosen(on)` `.Solid()` `.Backing()` `.AccentText()` `.DimText()` `.CategoryTone(i)` `.CategoryFill(i)` `.CategoryText(i)` `.Theme(roles)` |
| Text | `.SetText(s)` `.Fit()` `.Align(...)` `.Bold()` `.Mono()` |
| Geometry and flex | `.Size(w, h)` `.Width(w)` `.Height(h)` `.Grow(w)` `.Stretch()` `.IgnoreLayout()` `.Anchor(UiEdge)` |
| Behaviour | `.Tooltip(s)` `.Input()` `.Disabled()` `.Hidden()` `.With(UiCommand.Of(...))` |
| Lists, lines, meters | `UiLists.SetCount / Row / OnChanged`, `UiConnectors.Join`, `UiMeters.Set` |
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
        body.Add(new Column
        {
            Grow = 1,
            Cross = StackCrossAlignment.Stretch,
            Gap = Space.Md,
            Children =
            {
                Header(),
                (_search = new Field { Size = new Vector2(ListWidth, Control.Sm) }),        // 3
                new Row
                {
                    Grow = 1,
                    Cross = StackCrossAlignment.Stretch,
                    Gap = Space.Xl,
                    Children = { TypeList(), Chart() },
                },
            },
        });

        _presses = UiCommandEvents.OnPressed<BrowserCommand>(body.Entity.Store, Pressed);   // 7
    }

    // 2. Containers place everything: Grow / Stretch / Width, never coordinates. Roles, never colours.
    private Row Header() => new()
    {
        Gap = Space.Sm,
        Children =
        {
            new Label("BROWSER", UiType.Heading) { Role = TextRole.Accent },
            (_views[0] = new Button("ALL") { Command = UiCommand.Of(BrowserCommand.View, 0), Tooltip = "every type" }),
            (_views[1] = new Button("USED") { Command = UiCommand.Of(BrowserCommand.View, 1), Tooltip = "only types something uses" }),
            new Spacer(),
            new Meter(226f / 255) { Size = new Vector2(110, Space.Sm) },
        },
    };

    // 4. Long lists are pooled: only the rows that fit exist, refilled from the data when the list changes.
    private Column TypeList() => new()
    {
        Width = ListWidth,
        Cross = StackCrossAlignment.Stretch,
        Children =
        {
            (_list = new PooledList(Control.Xs, Space.Xs)
            {
                Grow = 1,
                Changed = FillList,
                Row = _ => [Remember(_rows, new NameCard(BrowserCommand.Pick) { Grow = 1 })],
            }),
        },
    };

    private Column Chart() => new()
    {
        Grow = 1,
        Main = StackMainAlignment.Center,
        Children =
        {
            new Column
            {
                Width = CardWidth,
                Pad = Space.Md,
                Tone = Tone.Solid,
                Children = { (_cardName = new Label("", UiType.Section) { Fit = true }) },   // 5
            },
            // 6. Lines: new Connector(rowCard, _cardName, category), added before what they join.
        },
    };

    // A composite spec: one class, its own little tree, the parts a fill needs.
    private sealed class NameCard(BrowserCommand press) : UiSpec
    {
        private readonly Label _name = new("", UiType.Body) { Grow = 1, Fit = true };
        public UiNode Name => _name.Node;

        protected override UiNode Create(UiNode parent) => parent.Add(new Row
        {
            Command = UiCommand.Of(press),
            Children = { _name },
        }).Node;
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
            card.Name.SetText(NameOf(id));                 // the whole string; Fit cuts it
        }
    }

    public void Dispose() => _presses?.Dispose();
}
```

## What each numbered step replaces

| Step | Use | Never |
| --- | --- | --- |
| 1 | `IToolStage.Build(UiNode body)` in a tool tab | a canvas stage that draws in a `Rect2` area |
| 2 | nested `Column` / `Row` with `Grow`, `Stretch`, `Width`, `Gap`, `Pad`, `Spacer` | `new Rect2(724, 34, …)`, `x += width + 6`, a flat run of `page.Row()` statements |
| 2 | `UiType.*` sizes, `UiMetrics.*` gaps and controls | font sizes and gaps as numbers |
| 2 | `Tone`, `TextRole`, `.Chosen(on)`, `.CategoryTone(i)` | `Palette.X` passed per call |
| 3 | a `Field` | reading typed text and drawing `find: {q}_` yourself |
| 4 | `PooledList` + `UiLists.SetCount` for anything long | thousands of row entities, or your own scroll offsets |
| 5 | `Fit = true` on text whose box layout sizes | cutting strings to guessed character counts |
| 6 | `Connector(from, to, category)` | `Canvas.DrawLine` between computed points |
| 7 | `Command = UiCommand.Of(enum, arg)` + `UiCommandEvents.OnPressed<T>` | per-feature button components, `box.HasPoint(_mouse)`, click closures |
| — | `Tooltip = …` | a hand-drawn hint bar driven by hover tests |
| — | `Meter(share)` | rect pairs sized by hand |

## Habits

- **State changes in handlers, showing happens in one place.** Press handlers
  set fields and a flag; `Update` (or a system) rebuilds what is shown.
- **The build tells the story.** `Build` reads top-down as the screen does, one
  named method per region (`Header()`, `TypeList()`, `Chart()`), each returning
  its spec; details live in the method, not in the outline.
- **Measure static text, `Fit` live text.** Labels that never change take their
  measured box; anything a fill rewrites sits in a grown box with `Fit`.
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
