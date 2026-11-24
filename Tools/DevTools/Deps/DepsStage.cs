using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Tools.DevTools.Deps;

/// <summary>
/// Deps: every built project DLL in a column for its level (<see cref="DepsGraph"/>),
/// from what the DLL really loads rather than what the csproj or a solution
/// folder suggests. Click one: what it uses lights in one colour, what uses it
/// in another, and the card below lists both, plus what it only reaches
/// through others. RELOAD reads the DLLs again after a build.
/// </summary>
internal sealed class DepsStage(string root, string? focus) : IToolStage
{
    // Gold, violet and pink in the theme's categories: none near the bright blue of the picked card.
    private const int UsesCategory = 0, ThroughCategory = 4, UsedByCategory = 6;
    private static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch);

    private enum DepsCommand
    {
        Pick,
        Reload,
    }

    private readonly List<Button> _cards = [];
    private DepsGraph _graph = null!;
    private Column _chart = null!;
    private Label _status = null!, _name = null!, _uses = null!, _through = null!, _usedBy = null!;
    private IDisposable? _presses;
    private int _picked = -1;
    private bool _reload = true, _changed = true;

    public void Build(UiNode body)
    {
        body.Add(new Column(Fill, gap: Space.Md)
        {
            new Row(gap: Space.Md, cross: Cross.Center)
            {
                new Label("DEPS", UiType.Heading, ink: Ink.Accent),
                new Label("what each built DLL really loads, by level: 1 loads no project DLL, each other one sits above its highest", UiType.Small),
                (_status = new Label("", UiType.Small, ink: Ink.Dim, align: TextAlign.Right, fit: true, grow: 1)),
                new Button("RELOAD", cmd: Cmd(DepsCommand.Reload), tip: "read the DLLs again (build first: ./run.sh devtools builds them)"),
            },
            new Row(gap: Space.Sm, cross: Cross.Center)
            {
                new Box(size: new Vector2(Space.Lg, Space.Lg), category: UsesCategory),
                new Label("uses", UiType.Caption),
                new Box(size: new Vector2(Space.Lg, Space.Lg), category: ThroughCategory),
                new Label("uses through others", UiType.Caption),
                new Box(size: new Vector2(Space.Lg, Space.Lg), category: UsedByCategory),
                new Label("used by", UiType.Caption),
            },
            (_chart = new Column(Fill)),
            new Column(gap: Space.Xs, pad: Space.Md, tone: Tone.Solid, stretch: true)
            {
                (_name = new Label("click a DLL", UiType.Section, ink: Ink.Accent, align: TextAlign.Left, stretch: true)),
                (_uses = new Label("", UiType.Label, align: TextAlign.Left, wrap: true, stretch: true)),
                (_through = new Label("", UiType.Label, ink: Ink.Dim, align: TextAlign.Left, wrap: true, stretch: true)),
                (_usedBy = new Label("", UiType.Label, align: TextAlign.Left, wrap: true, stretch: true)),
            },
        });

        _presses = UiCommandEvents.OnPressed<DepsCommand>(body.Entity.Store, Pressed);
    }

    // Presses only change state; Update shows it, outside the press event.
    private void Pressed(DepsCommand command, UiCommand press, Entity _)
    {
        if (command == DepsCommand.Reload)
            _reload = true;
        else
            _picked = press.Arg == _picked ? -1 : press.Arg;
        _changed = true;
    }

    public void Update(in ToolFrame frame)
    {
        if (_reload)
        {
            _reload = false;
            string? picked = _picked >= 0 ? _graph.Assemblies[_picked].Name : focus;
            _graph = DepsGraph.Read(root);
            _picked = picked is null ? -1 : _graph.Find(picked) is { } found ? IndexOf(found.Name) : -1;
            BuildChart();
        }

        if (!_changed)
            return;
        _changed = false;
        ShowPicked();
    }

    /// <summary>One column per level, a card per DLL; rebuilt whole, as a reload can move DLLs between levels.</summary>
    private void BuildChart()
    {
        UiNode chart = _chart.Node;
        UiLayout.DeleteChildren(chart.Entity);
        _cards.Clear();

        var assemblies = _graph.Assemblies;
        chart.Add(new Row(Fill, gap: Space.Md)
        {
            assemblies.GroupBy(a => a.Level).Select(level => new Column(Fill, gap: Space.Xs)
            {
                new Label($"LEVEL {level.Key}", UiType.Small, ink: Ink.Dim),
                level.Select(a => Remember(new Button(a.Short, cmd: Cmd(DepsCommand.Pick, IndexOf(a.Name)), stretch: true,
                    tip: $"uses {DepsGraph.List(a.Uses)} · built {DepsGraph.Age(a.Built)} ago"))),
            }),
        });

        DateTime oldest = assemblies.Count > 0 ? assemblies.Min(a => a.Built) : DateTime.Now;
        string status = $"{assemblies.Count} DLLs · oldest build {DepsGraph.Age(oldest)} ago";
        _status.Node.SetText(_graph.Cycle is { } cycle ? $"CYCLE {cycle} · {status}" : status);
    }

    private Button Remember(Button card)
    {
        _cards.Add(card);
        return card;
    }

    private int IndexOf(string name)
    {
        var assemblies = _graph.Assemblies;
        for (int i = 0; i < assemblies.Count; i++)
        {
            if (assemblies[i].Name == name)
                return i;
        }

        return -1;
    }

    private void ShowPicked()
    {
        var assemblies = _graph.Assemblies;
        DepsAssembly? picked = _picked >= 0 ? assemblies[_picked] : null;
        HashSet<string> through = picked is null ? [] : [.. _graph.Through(picked).Select(a => a.Name)];
        for (int i = 0; i < _cards.Count; i++)
        {
            UiNode card = _cards[i].Node;
            DepsAssembly a = assemblies[i];
            if (picked is null || i == _picked)
                card.Chosen(i == _picked);
            else if (picked.Uses.Contains(a.Name))
                card.CategoryTone(UsesCategory);
            else if (through.Contains(a.Name))
                card.CategoryTone(ThroughCategory);
            else if (a.Uses.Contains(picked.Name))
                card.CategoryTone(UsedByCategory);
            else
                card.Quiet();
        }

        if (picked is null)
        {
            _name.Node.SetText("click a DLL");
            _uses.Node.SetText("");
            _through.Node.SetText("");
            _usedBy.Node.SetText("");
            return;
        }

        _name.Node.SetText($"{picked.Short} · level {picked.Level} · built {DepsGraph.Age(picked.Built)} ago");
        _uses.Node.SetText($"uses: {DepsGraph.List(picked.Uses)}");
        _through.Node.SetText($"through others: {DepsGraph.List(through)}");
        _usedBy.Node.SetText($"used by: {DepsGraph.List(_graph.UsedBy(picked).Select(a => a.Name))}");
    }

    public void Dispose() => _presses?.Dispose();
}
