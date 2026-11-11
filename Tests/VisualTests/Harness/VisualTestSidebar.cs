using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.VisualTests;

/// <summary>How a scene test stands in the sidebar.</summary>
public enum VisualTestStatus
{
    Pending,
    Running,
    Passed,
    Failed,
}

/// <summary>What a click on the sidebar asked for.</summary>
public enum VisualTestActionKind
{
    Select,
    Run,
    RunAll,
}

/// <summary>One click, queued for the harness to handle next frame.</summary>
public readonly record struct VisualTestAction(VisualTestActionKind Kind, int Index);

/// <summary>One line of the detail block, in the colour of what it reports.</summary>
public readonly record struct VisualTestDetailLine(string Text, Color Color);

/// <summary>
/// The scene-test browser, built as an ECS UI tree like everything else: a
/// title, a run-all button, then two scroll views stacked so they cannot overlap,
/// one with a row per test and one with the current test's steps. It draws
/// under the host's canvas item with the host's pointer and keyboard.
/// </summary>
public sealed class VisualTestSidebar : IDisposable
{
    /// <summary>The design space the harness authors against; 16:9 only.</summary>
    public const float DesignWidth = 1920;
    public const float DesignHeight = 1080;

    /// <summary>Width of the list down the left of the window.</summary>
    public const float SidebarWidth = 340;

    private const float Padding = UiMetrics.Space.Lg;
    private const float ContentWidth = SidebarWidth - Padding * 2;
    private const float RowHeight = UiMetrics.Control.Xs;
    private const float RowGap = UiMetrics.Space.Xs;
    private const float RowPitch = RowHeight + RowGap;
    private const float ArrowWidth = 26;
    private const float NameWidth = ContentWidth - ArrowWidth - RowGap;
    private const float ListHeight = 16 * RowPitch;
    private const float DetailHeight = 460;
    private const float DetailPitch = UiMetrics.Space.Lg;

    private readonly IReadOnlyList<SceneTest> _tests;
    private readonly VisualTestStatus[] _statuses;
    private readonly Entity[] _rows;
    private readonly Entity[] _arrows;
    private readonly List<UiNode> _detail = new();
    private readonly Dictionary<string, List<string>> _wrapped = new();
    private readonly EntityStore _store = new();
    private readonly UiRoot _ui;
    private readonly SystemRoot _root;
    private readonly SidebarCommands _commands = new();

    private Entity _summary;
    private Entity _runAll;
    private Entity _listView;
    private Entity _detailView;
    private UiNode _detailContent;
    private int _selected = -1;
    private int _detailFocus = -1;

    public VisualTestSidebar(
        Handle canvas,
        IPointerInputSource pointer,
        IWheelSource wheel,
        IKeyboardState keyboard,
        IReadOnlyList<SceneTest> tests)
    {
        _tests = tests;
        _statuses = new VisualTestStatus[tests.Count];
        _rows = new Entity[tests.Count];
        _arrows = new Entity[tests.Count];

        // The sidebar's store is its own; no overlays, the scene's UI has those.
        _ui = new UiRoot(_store, canvas, pointer, new Vector2(DesignWidth, DesignHeight), keyboard, wheel, overlays: false);
        _root = new SystemRoot(_store, "Sidebar") { _commands };
        _ui.Run(_root);

        Entity root = _store.CreateEntity();
        UiNode panel = UiNode.Layer(_store, root, UiLayers.VisualTestSidebar)
            .Column()
            .Box(new Rect2(0, 0, SidebarWidth, DesignHeight))
            .Fill(Palette.Background)
            .Pad(Padding)
            .Gap(UiMetrics.Space.Sm)
            .Main(StackMainAlignment.Start)
            .Cross(StackCrossAlignment.Start);

        BuildChrome(panel);
        BuildRows(panel);
        BuildDetail(panel);
        RefreshRows();
    }

    /// <summary>The node every sidebar entity hangs from, for tests and debugging.</summary>
    public EntityStore Store => _store;

    /// <summary>Runs the UI systems for one frame.</summary>
    public void Frame(float delta, float time)
    {
        _ui.Update(new UpdateTick(delta, time));
    }

    /// <summary>Takes the next click, if any. Select and Run are per row.</summary>
    public bool TryTake(out VisualTestAction action) => _commands.TryTake(out action);


    /// <summary>Shows the one-line status under the title.</summary>
    public void SetSummary(string text, Color color)
    {
        UiText.Set(_summary, text);
        SetTextColor(_summary, color);
    }

    /// <summary>Marks one test's state; the row's text and tint follow.</summary>
    public void SetStatus(int index, VisualTestStatus status)
    {
        if (index < 0 || index >= _statuses.Length)
            return;

        _statuses[index] = status;
        RefreshRow(index);
    }

    /// <summary>Moves the selection marker and the row highlight.</summary>
    public void SetSelected(int index)
    {
        int previous = _selected;
        _selected = index;

        if (previous >= 0)
            RefreshRow(previous);

        if (index >= 0)
        {
            RefreshRow(index);
            KeepRowInView(index);
        }
    }

    /// <summary>Disables every control while a suite walks the tests.</summary>
    public void SetBusy(bool busy)
    {
        UiLayout.SetDisabled(_runAll, busy);

        for (int i = 0; i < _rows.Length; i++)
        {
            UiLayout.SetDisabled(_rows[i], busy);
            UiLayout.SetDisabled(_arrows[i], busy);
        }
    }

    /// <summary>
    /// The current test's steps; long lines wrap and extra lines are hidden. When
    /// <paramref name="focus"/> moves, the view follows it, so a running step is
    /// on screen while the wheel stays free between steps.
    /// </summary>
    public void SetDetail(IReadOnlyList<VisualTestDetailLine> lines, int focus = -1)
    {
        int shown = 0;
        float focusTop = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            if (i == focus)
                focusTop = shown * DetailPitch;

            foreach (string text in Wrapped(lines[i].Text))
                ShowDetailLine(shown++, text, lines[i].Color);
        }

        for (int i = shown; i < _detail.Count; i++)
        {
            if (!UiLayout.IsHidden(_detail[i]))
                UiLayout.SetHidden(_detail[i], true);
        }

        if (focus != _detailFocus)
        {
            _detailFocus = focus;
            _detailView.GetComponent<ScrollView>().Offset =
                focus < 0 ? 0 : focusTop - DetailHeight / 2;
        }
    }

    private void BuildChrome(UiNode panel)
    {
        panel.Text("SCENE TESTS", UiType.Section, Palette.Accent);

        _runAll = Button(panel, ContentWidth, "RUN ALL", Palette.Text, Palette.Primary)
            .Height(UiMetrics.Control.Sm)
            .With(new VisualTestCommand(VisualTestActionKind.RunAll, -1));

        _summary = panel.Text("Ready.", UiType.Caption, Palette.TextDim).Width(ContentWidth);
    }

    private void BuildRows(UiNode panel)
    {
        float contentHeight = _tests.Count * RowPitch - RowGap;
        _listView = panel.Scroll(new Vector2(ContentWidth, ListHeight), contentHeight, out UiNode content);
        content.IgnoreLayout().Gap(RowGap).Main(StackMainAlignment.Start);

        for (int i = 0; i < _tests.Count; i++)
        {
            UiNode row = content.Row().Gap(RowGap);
            _arrows[i] = Button(row, ArrowWidth, "▶", Palette.SuccessBright, Palette.Surface)
                .With(new VisualTestCommand(VisualTestActionKind.Run, i));
            _rows[i] = Button(row, NameWidth, _tests[i].Name, Palette.TextMuted, Palette.Surface)
                .With(new VisualTestCommand(VisualTestActionKind.Select, i))
                .Tooltip(_tests[i].Name);
        }
    }

    private void BuildDetail(UiNode panel)
    {
        panel.Text("DETAIL", UiType.Caption, Palette.TextDim);

        _detailView = panel.Scroll(new Vector2(ContentWidth, DetailHeight), 0, out _detailContent);
        _detailContent
            .IgnoreLayout()
            .AutoHeight()
            .Gap(0)
            .Main(StackMainAlignment.Start)
            .Cross(StackCrossAlignment.Start);
    }

    private void ShowDetailLine(int index, string text, Color color)
    {
        while (_detail.Count <= index)
        {
            _detail.Add(_detailContent
                .Text("", UiType.Caption, UiTheme.Text)
                .Size(ContentWidth, DetailPitch));
        }

        Entity line = _detail[index];
        if (UiLayout.IsHidden(line))
            UiLayout.SetHidden(line, false);

        UiText.Set(line, text);
        SetTextColor(line, color);
    }

    /// <summary>The lines a text wraps to. Cached, as steps are redrawn every frame of a run.</summary>
    private List<string> Wrapped(string text)
    {
        if (_wrapped.TryGetValue(text, out List<string>? lines))
            return lines;

        lines = text.Length == 0 ? [""] : UiTextFit.Wrap(text, UiType.Caption, ContentWidth);
        _wrapped[text] = lines;
        return lines;
    }

    private void KeepRowInView(int index)
    {
        ref ScrollView view = ref _listView.GetComponent<ScrollView>();
        float top = index * RowPitch;
        if (top < view.Offset)
            view.Offset = top;
        else if (top + RowHeight > view.Offset + ListHeight)
            view.Offset = top + RowHeight - ListHeight;
    }

    private void RefreshRows()
    {
        for (int i = 0; i < _rows.Length; i++)
            RefreshRow(i);
    }

    private void RefreshRow(int index)
    {
        string marker = _statuses[index] switch
        {
            VisualTestStatus.Running => ">",
            VisualTestStatus.Passed => "+",
            VisualTestStatus.Failed => "!",
            _ => " ",
        };

        string selected = index == _selected ? ">" : " ";
        UiText.Set(
            _rows[index],
            UiTextFit.Cut($"{marker}{selected} {_tests[index].Name}", UiType.Caption, NameWidth));

        SetTextColor(_rows[index], _statuses[index] switch
        {
            VisualTestStatus.Running => Palette.Accent,
            VisualTestStatus.Passed => Palette.SuccessBright,
            VisualTestStatus.Failed => Palette.Danger,
            _ => Palette.TextMuted,
        });

        SetFill(
            _rows[index],
            index == _selected ? Palette.SurfaceRaised : Palette.Surface);
    }

    private static void SetTextColor(Entity entity, Color color)
    {
        ref TextStyle style = ref entity.GetComponent<TextStyle>();
        if (style.Color == color)
            return;

        style.Color = color;
        entity.AddTag<Dirty>();
    }

    private static void SetFill(Entity entity, Color color)
    {
        ref FillColor fill = ref entity.GetComponent<FillColor>();
        if (fill.Value == color)
            return;

        fill.Value = color;
        entity.AddComponent(ButtonVisual.Create(color));
        entity.AddTag<Dirty>();
    }

    private static UiNode Button(UiNode parent, float width, string text, Color textColor, Color fill) =>
        parent.Button(text, UiType.Caption, fill)
            .Size(width, RowHeight)
            .TextColor(textColor)
            .Align(TextAlign.Left);

    public void Dispose()
    {
        foreach (Entity entity in _store.Entities.ToEntityList())
        {
            if (!entity.IsNull)
                entity.DeleteEntity();
        }
    }

    /// <summary>Turns a press on a sidebar button into one queued action.</summary>
    private sealed class SidebarCommands : QuerySystem<VisualTestCommand>
    {
        private readonly Queue<VisualTestAction> _pending = new();
        private readonly HashSet<int> _held = new();

        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref VisualTestCommand command, Entity entity) =>
            {
                bool pressed = entity.Tags.Has<Pressed>();
                bool wasPressed = _held.Contains(entity.Id);

                if (pressed)
                    _held.Add(entity.Id);
                else
                    _held.Remove(entity.Id);

                if (!pressed || wasPressed)
                    return;

                _pending.Enqueue(new VisualTestAction(command.Kind, command.Index));
            });
        }

        public bool TryTake(out VisualTestAction action)
        {
            if (_pending.Count == 0)
            {
                action = default;
                return false;
            }

            action = _pending.Dequeue();
            return true;
        }
    }
}

/// <summary>What a sidebar button does when it is pressed.</summary>
public struct VisualTestCommand(VisualTestActionKind kind, int index) : IComponent
{
    public VisualTestActionKind Kind = kind;
    public int Index = index;
}
