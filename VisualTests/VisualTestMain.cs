using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Input;

namespace Writer.VisualTests;

/// <summary>
/// Boots ECS scenes without navigating menus, waits for their layer
/// initialization to settle, and runs structural assertions. Supports
/// interactive switching and CLI runs through
/// `--visual-test=&lt;slug&gt; [--visual-test-exit]`.
/// </summary>
public partial class VisualTestMain : Control
{
    private const int MaxWaitFrames = 120;

    private readonly List<IVisualTest> _tests = new(VisualTestCatalog.All);
    private readonly List<VisualTestRun> _runs = new();

    private EntityStore? _entities;
    private WriterSystemRoot? _systems;
    private SceneDirector? _director;
    private GodotPointerInputSource? _pointer;
    private VisualTestContext? _context;
    private IVisualTest? _test;
    private Label? _hud;

    private int _index;
    private int _frame;
    private bool _waiting;
    private bool _auto;
    private bool _runAll;
    private string? _invalidRequest;
    private VisualTestResult _result;

    public override void _Ready()
    {
        SetProcess(true);
        BuildHud();
        ParseArguments();

        if (_invalidRequest is not null)
        {
            GD.PrintErr($"Unknown visual test '{_invalidRequest}'.");
            PrintTestList();
            GetTree().Quit(2);
            return;
        }

        StartTest(_index);
    }

    public override void _Process(double delta)
    {
        if (_systems is null || _context is null || _test is null)
            return;

        float time = (float)Time.GetTicksMsec() / 1000f;
        _systems.Update(new UpdateTick((float)delta, time));

        if (!_waiting)
            return;

        _frame++;

        if (_test.IsReady(_context))
        {
            _waiting = false;
            CompleteTest(_test.Verify(_context));
            return;
        }

        if (_frame >= MaxWaitFrames)
        {
            _waiting = false;
            CompleteTest(VisualTestResult.Fail(
                $"timed out waiting for {_test.Name} ECS initialization"));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_auto ||
            @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Left:
                SwitchTest(-1);
                break;

            case Key.Right:
                SwitchTest(1);
                break;

            case Key.R:
                StartTest(_index);
                break;

            case Key.Escape:
                GetTree().Quit();
                break;
        }
    }

    public override void _ExitTree()
    {
        Cleanup();
    }

    private void StartTest(int index)
    {
        Cleanup();

        _index = Mathf.Clamp(index, 0, _tests.Count - 1);
        _test = _tests[_index];
        _frame = 0;
        _waiting = true;
        _result = default;

        _entities = new EntityStore();
        _pointer = new GodotPointerInputSource(this);
        _director = new SceneDirector();
        Vector2 screen = GetViewportRect().Size;
        if (screen.X <= 0 || screen.Y <= 0)
            screen = new Vector2(1920, 1080);

        _systems = new WriterSystemRoot(
            _entities,
            GetCanvasItem(),
            _pointer,
            _director,
            screen,
            new KeybindSettings(persist: false));

        _context = new VisualTestContext(_entities, _systems, screen);

        try
        {
            _test.Begin(_context);
        }
        catch (Exception exception)
        {
            _waiting = false;
            CompleteTest(VisualTestResult.Fail(exception.Message));
            GD.PrintErr($"[visual:{_test.Slug}] start failed: {exception}");
            return;
        }

        if (_hud is not null)
            _hud.Visible = !_auto;

        UpdateHud();
    }

    private void CompleteTest(VisualTestResult result)
    {
        if (_test is null)
            return;

        _result = result;
        _runs.Add(new VisualTestRun(_test.Name, result.Passed, result.Message));

        GD.Print(
            $"[visual:{_test.Slug}] " +
            $"{(result.Passed ? "PASS" : "FAIL")}: {result.Message}");

        UpdateHud();

        if (!_auto)
            return;

        if (_runAll && _index + 1 < _tests.Count)
        {
            StartTest(_index + 1);
            return;
        }

        Finish();
    }

    private void Finish()
    {
        bool failed = _runs.Exists(static run => !run.Passed);
        GD.Print(failed ? "Visual tests failed." : "Visual tests passed.");
        GetTree().Quit(failed ? 1 : 0);
    }

    private void SwitchTest(int direction)
    {
        int next = Mathf.Wrap(_index + direction, 0, _tests.Count);
        StartTest(next);
    }

    private void Cleanup()
    {
        if (_entities is not null)
        {
            foreach (Entity entity in _entities.Query<VisualRid>().ToEntityList())
            {
                if (!entity.IsNull)
                    entity.DeleteEntity();
            }
        }

        _systems?.Dispose();
        _systems = null;
        _entities = null;
        _context = null;
        _test = null;
        _pointer = null;
        _director = null;
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer { Layer = 2000 };
        AddChild(layer);

        var backdrop = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(780, 240),
            Color = new Color(0.02f, 0.03f, 0.06f, 0.82f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        layer.AddChild(backdrop);

        _hud = new Label
        {
            Position = new Vector2(16, 16),
            Size = new Vector2(760, 240),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = !_auto,
        };

        _hud.AddThemeFontSizeOverride("font_size", 14);
        _hud.AddThemeColorOverride("font_color", new Color("#e2e8f0"));
        _hud.AddThemeColorOverride("font_outline_color", new Color("#020617"));
        _hud.AddThemeConstantOverride("outline_size", 4);
        layer.AddChild(_hud);
    }

    private void ParseArguments()
    {
        string? requested = null;

        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--visual-test=", StringComparison.Ordinal))
                requested = argument["--visual-test=".Length..];
            else if (argument == "--visual-test-exit")
                _auto = true;
        }

        if (requested is null)
            return;

        if (requested.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            _runAll = true;
            _auto = true;
            _index = 0;
            return;
        }

        for (int i = 0; i < _tests.Count; i++)
        {
            if (_tests[i].Slug.Equals(requested, StringComparison.OrdinalIgnoreCase) ||
                _tests[i].Name.Equals(requested, StringComparison.OrdinalIgnoreCase))
            {
                _index = i;
                return;
            }
        }

        _invalidRequest = requested;
    }

    private void UpdateHud()
    {
        if (_hud is null)
            return;

        if (_test is null)
        {
            _hud.Text = "VISUAL TESTS\nNo test loaded";
            return;
        }

        string status = _waiting
            ? "WAITING FOR ECS..."
            : _result.Passed
                ? "PASS"
                : "FAIL";

        string message = _waiting ? "" : _result.Message;
        var lines = new List<string>
        {
            $"VISUAL TESTS  {_index + 1}/{_tests.Count}",
            "",
        };

        for (int i = 0; i < _tests.Count; i++)
            lines.Add($"{(i == _index ? ">" : " ")} {_tests[i].Name}");

        lines.Add("");
        lines.Add($"{status}  {message}");
        lines.Add("");
        lines.Add("LEFT/RIGHT switch   R rerun   Esc quit");
        _hud.Text = string.Join("\n", lines);
    }

    private void PrintTestList()
    {
        GD.Print("Available visual tests:");

        foreach (IVisualTest test in _tests)
            GD.Print($"  {test.Slug}  {test.Name}");
    }

    private readonly record struct VisualTestRun(
        string Name,
        bool Passed,
        string Message);
}
