using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Theme;

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

/// <summary>
/// The scene-test browser, built as an ECS UI tree like everything else: a
/// title, a run-all button, one row per test and a detail block for the current
/// test's steps. The host Control's canvas item is the only Godot UI surface it
/// touches, and no Godot widget is created.
/// </summary>
public sealed class VisualTestSidebar : IDisposable
{
    /// <summary>The design space the harness authors against; 16:9 only.</summary>
    public const float DesignWidth = 1920;
    public const float DesignHeight = 1080;

    /// <summary>Width of the list down the left of the window.</summary>
    public const float SidebarWidth = 340;

    private const int LayerZ = 900;
    private const float Padding = 16;
    private const float ContentWidth = SidebarWidth - Padding * 2;
    private const float RowsTop = 108;
    private const float RowPitch = 28;
    private const float RowHeight = 24;
    private const float ArrowWidth = 26;
    private const float NameWidth = ContentWidth - ArrowWidth - 4;
    private const float DetailTop = RowsTop + 20 * RowPitch;
    private const int DetailLines = 16;
    private const float DetailPitch = 15;

    private static readonly Color Clear = new(0, 0, 0, 0);

    private readonly IReadOnlyList<SceneTest> _tests;
    private readonly VisualTestStatus[] _statuses;
    private readonly Entity[] _rows;
    private readonly Entity[] _arrows;
    private readonly Entity[] _detail;
    private readonly EntityStore _store = new();
    private readonly SystemRoot _root;
    private readonly UiBuilder _ui;
    private readonly GodotPointerInputSource _pointer;
    private readonly SidebarCommands _commands = new();

    private Entity _summary;
    private Entity _runAll;
    private int _selected = -1;

    public VisualTestSidebar(Control host, IReadOnlyList<SceneTest> tests)
    {
        _tests = tests;
        _statuses = new VisualTestStatus[tests.Count];
        _rows = new Entity[tests.Count];
        _arrows = new Entity[tests.Count];
        _detail = new Entity[DetailLines];

        _pointer = new GodotPointerInputSource(host);
        _root = new SystemRoot(_store);
        _root.Add(new UiSystemGroup(
            _store,
            host.GetCanvasItem(),
            new GodotRectCanvas(),
            _pointer,
            keyboard: new GodotKeyboardState(),
            screen: new Vector2(DesignWidth, DesignHeight)));
        _root.Add(_commands);

        _ui = UiBuilder.Begin(_store).Layer(LayerZ);
        Entity root = _store.CreateEntity();
        root.AddChild(_ui.Current);

        BuildChrome();
        BuildRows();
        BuildDetail();
        RefreshRows();
    }

    /// <summary>The node every sidebar entity hangs from, for tests and debugging.</summary>
    public EntityStore Store => _store;

    /// <summary>Runs the UI systems for one frame.</summary>
    public void Frame(float delta, float time) =>
        _root.Update(new UpdateTick(delta, time));

    /// <summary>Takes the next click, if any. Select and Run are per row.</summary>
    public bool TryTake(out VisualTestAction action) => _commands.TryTake(out action);

    /// <summary>Catches a click that was pressed and released between two frames.</summary>
    public void LatchPress() => _pointer.LatchPress();

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
            RefreshRow(index);
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

    /// <summary>The current test's steps, one line each; extra lines are cleared.</summary>
    public void SetDetail(IReadOnlyList<string> lines)
    {
        for (int i = 0; i < _detail.Length; i++)
            UiText.Set(_detail[i], i < lines.Count ? lines[i] : "");
    }

    private void BuildChrome()
    {
        Rect(0, 0, SidebarWidth, DesignHeight, Palette.Background);
        Text(Padding, 12, ContentWidth, 26, "SCENE TESTS", 20, Palette.Accent);

        _runAll = Button(
            Padding, 44, ContentWidth, 30,
            "RUN ALL", 14, Palette.Text, Palette.Primary,
            new VisualTestCommand(VisualTestActionKind.RunAll, -1));

        _summary = Text(Padding, 80, ContentWidth, 18, "Ready.", 12, Palette.TextDim);
    }

    private void BuildRows()
    {
        for (int i = 0; i < _tests.Count; i++)
        {
            float y = RowsTop + i * RowPitch;
            _arrows[i] = Button(
                Padding, y, ArrowWidth, RowHeight,
                "▶", 13, Palette.SuccessBright, Palette.Surface,
                new VisualTestCommand(VisualTestActionKind.Run, i));
            _rows[i] = Button(
                Padding + ArrowWidth + 4, y, NameWidth, RowHeight,
                _tests[i].Name, 13, Palette.TextMuted, Palette.Surface,
                new VisualTestCommand(VisualTestActionKind.Select, i));
        }
    }

    private void BuildDetail()
    {
        float top = RowsTop + _tests.Count * RowPitch + 16;
        top = Mathf.Min(top, DetailTop);
        Text(Padding, top, ContentWidth, 16, "DETAIL", 11, Palette.TextDim);

        for (int i = 0; i < DetailLines; i++)
        {
            _detail[i] = Text(
                Padding, top + 18 + i * DetailPitch, ContentWidth, DetailPitch,
                "", 11, Palette.TextDim);
        }
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
        UiText.Set(_rows[index], $"{marker}{selected} {_tests[index].Name}");

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

    private Entity Text(
        float x,
        float y,
        float width,
        float height,
        string text,
        int fontSize,
        Color color)
    {
        Entity entity = _ui.RectEntity(
            Center(x, y, width, height),
            new Vector2(width, height),
            Clear,
            text);
        entity.AddComponent(new TextStyle(fontSize, color));
        entity.AddComponent(new TextAlignment(TextAlign.Left));
        return entity;
    }

    private Entity Button(
        float x,
        float y,
        float width,
        float height,
        string text,
        int fontSize,
        Color textColor,
        Color fill,
        VisualTestCommand command)
    {
        Entity entity = _ui.RectEntity(
            Center(x, y, width, height),
            new Vector2(width, height),
            fill,
            text,
            takesInput: true);
        entity.AddComponent(new TextStyle(fontSize, textColor));
        entity.AddComponent(new TextAlignment(TextAlign.Left));
        entity.AddComponent(ButtonVisual.Create(fill));
        entity.AddComponent(command);
        return entity;
    }

    private void Rect(float x, float y, float width, float height, Color color) =>
        _ui.RectEntity(
            Center(x, y, width, height),
            new Vector2(width, height),
            color);

    private static Vector2 Center(float x, float y, float width, float height) =>
        new(x + width / 2, y + height / 2);

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
