# UI best practices, by example

The current house style for ECS UI, shown on one small tool. Atlas
(`Tools/DevTools/Atlas/`) is the full-size reference: it is built exactly this
way. The rules behind it are in `agents.md` (UI) and
`Docs/ui-fluent-architecture.md`.

**If the library is missing something you need, ask before adding it.** Do not
hard code around the gap with hand-placed rects, raw canvas draws or your own
pointer reading. Every piece below started as such a gap.

## The example: a type browser tab

A tool tab with a header of view buttons, a budget meter, a search field, a
long list of types, and a card for the picked type joined to its neighbours by
lines.

```csharp
internal sealed class BrowserStage : IToolStage
{
    // Named sizes only for what is the tool's own design; type and control
    // sizes come from UiType / UiMetrics.
    private const float ListWidth = 420;
    private const float CardWidth = 300;

    private enum BrowserCommand { View, Pick }

    private UiNode _search, _list, _card, _cardName;
    private readonly List<UiNode> _rowNames = new();
    private readonly UiNode[] _views = new UiNode[2];
    private IDisposable? _presses;
    private List<int> _ids = new();
    private bool _changed = true;

    // 1. Build once into the body the shell hands over. No canvas, no area, no pointer.
    public void Build(UiNode body)
    {
        UiNode page = body.Column().Grow().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Md);

        // 2. Containers place everything. Grow / Stretch / Width, never coordinates.
        UiNode header = page.Row().Gap(UiMetrics.Space.Sm);
        header.Text("BROWSER", UiType.Heading).AccentText();              // theme role, not a colour
        _views[0] = header.Button("ALL", UiType.Label)
            .With(UiCommand.Of(BrowserCommand.View, 0))                   // one shared command component
            .Tooltip("every type");                                       // hover help is a tooltip
        _views[1] = header.Button("USED", UiType.Label)
            .With(UiCommand.Of(BrowserCommand.View, 1))
            .Tooltip("only types something uses");
        header.Column().Grow();                                           // spacer pushes the rest right
        header.Meter(226f / 255).Size(110, UiMetrics.Space.Sm);           // good / warn / bad from the theme

        // 3. A text field is a field, focused by the field systems; the host types into it.
        _search = page.Rect(default, "", new Vector2(ListWidth, UiMetrics.Control.Sm))
            .Input().With(new TextStyle(UiType.Label, page.Roles.Text))
            .Align(TextAlign.Left).With(new TextField(""));

        UiNode main = page.Row().Grow().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Xl);

        // 4. Long lists are pooled: only the rows that fit exist, refilled on scroll.
        _list = main.Column().Width(ListWidth).Cross(StackCrossAlignment.Stretch)
            .List(UiMetrics.Control.Xs, UiMetrics.Space.Xs, row =>
            {
                UiNode card = row.Row().Grow().Input()
                    .With(ButtonVisual.Create(default))                   // hover / flash
                    .With(UiCommand.Of(BrowserCommand.Pick));
                _rowNames.Add(card.Text("", UiType.Body).Grow().Fit());   // 5. cut to the box at draw time
            }).Grow();
        _list.Entity.OnChanged(FillList);                                 // native component event

        // 6. Lines between nodes are connectors, built before what they join so the cards cover their ends.
        UiNode chart = main.Row().Grow().Cross(StackCrossAlignment.Stretch);
        UiNode lines = chart.Column().IgnoreLayout();
        UiNode centre = chart.Column().Grow().Stretch().Main(StackMainAlignment.Center).Cross(StackCrossAlignment.Center);
        _card = centre.Column().Width(CardWidth).Pad(UiMetrics.Space.Md).Input().Tooltip("");
        _cardName = _card.Text("", UiType.Section).Fit();
        // lines.Connector(someRowCard, _card, category) per pooled row, joined in FillList.

        // 7. Presses arrive through the store's tag event; dispose it with the stage.
        _presses = UiCommandEvents.OnPressed<BrowserCommand>(body.Entity.Store, Pressed);
    }

    // The handler only changes state; Update shows it after the UI has run.
    private void Pressed(BrowserCommand command, UiCommand press, Entity _)
    {
        // ... set the view or picked id from press.Arg ...
        _changed = true;
    }

    public void Update(in ToolFrame frame)
    {
        if (!_changed)
            return;
        _changed = false;

        _views[0].Chosen(true);                                           // bright blue only for what is on
        _views[1].Chosen(false);
        UiLists.SetCount(_list, _ids.Count);                              // the list announces, FillList refills
    }

    private void FillList()
    {
        UiList view = _list.Entity.GetComponent<UiList>();
        for (int i = 0; i < view.Shown; i++)
        {
            int id = _ids[view.First + i];
            UiNode row = UiNode.Wrap(UiLists.Row(_list, i));
            row.Entity.ChildEntities[0].GetComponent<UiCommand>().Arg = id;
            _rowNames[i].SetText(NameOf(id));                             // the whole string; Fit cuts it
            UiNode.Wrap(row.Entity.ChildEntities[0]).CategoryTone(KindOf(id)); // categories, not hues
        }
    }

    public void Dispose() => _presses?.Dispose();
}
```

## What each numbered step replaces

| Step | Use | Never |
| --- | --- | --- |
| 1 | `IToolStage.Build(UiNode body)` in a tool tab | a canvas stage that draws in a `Rect2` area |
| 2 | `Row` / `Column`, `Grow`, `Stretch`, `Width`, `Gap`, `Pad` | `new Rect2(724, 34, …)`, `x += width + 6` |
| 2 | `UiType.*` sizes, `UiMetrics.*` gaps and controls | font sizes and gaps as numbers |
| 2 | role variants: `.AccentText()`, `.DimText()`, `.Secondary()`, `.Chosen(on)`, `.CategoryTone(i)` | `Palette.X` passed per call |
| 3 | a `TextField` node | reading typed text and drawing `find: {q}_` yourself |
| 4 | `.List(...)` + `UiLists.OnChanged` / `SetCount` / `Row` for anything long | thousands of row entities, or your own scroll offsets |
| 5 | `.Fit()` on text whose box layout sizes | cutting strings to guessed character counts |
| 6 | `.Connector(from, to, category)` | `Canvas.DrawLine` between computed points |
| 7 | `UiCommand.Of(enum, arg)` + `UiCommandEvents.OnPressed<T>` | per-feature button components, `box.HasPoint(_mouse)`, click closures |
| — | `.Tooltip(text)` | a hand-drawn hint bar driven by hover tests |
| — | `.Meter(share)` | rect pairs sized by hand |

## Habits

- **State changes in handlers, showing happens in one place.** Press handlers
  set fields and a flag; `Update` (or a system) rebuilds what is shown.
- **Measure static text, `Fit` live text.** Labels that never change take their
  measured box; anything a fill rewrites sits in a grown box with `.Fit()`.
- **Live text in a measured button:** build it with its longest label (say
  `"PROJECT ▼"`), then `SetText` the shorter one.
- **Hide, don't move.** `UiLayout.SetHidden` swaps views; pooled rows past the
  end are hidden by the list itself.
- **Colours tell kinds apart through `Categories`.** The tool maps its kinds to
  category indices in one place (`KindCategory` in Atlas); the theme owns hues.
- **Verify with one screenshot.** `./run.sh <exe> --shot=<scratch path>`; scratch
  files never go in the repo.
