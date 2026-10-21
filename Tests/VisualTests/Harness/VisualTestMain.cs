using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Input;

namespace Writer.Testing;

/// <summary>
/// Boots ECS scenes without navigating menus and runs osu-style test scenes:
/// each test is a sequence of steps and assertions executed against the live
/// entity store. Supports interactive switching and CLI runs through
/// `--visual-test=&lt;slug&gt; [--visual-test-exit]`.
/// </summary>
public partial class VisualTestMain : Control
{
    private readonly List<SceneTest> _tests = new(SceneTestCatalog.All);
    private readonly List<VisualTestRun> _runs = new();
    private readonly SceneTestRunner _runner = new();

    private EntityStore? _entities;
    private WriterSystemRoot? _systems;
    private SceneDirector? _director;
    private ScriptedPointerInputSource? _pointer;
    private VisualTestContext? _context;
    private CanvasLayer? _hudLayer;
    private Label? _hud;

    private int _index;
    private bool _auto;
    private bool _runAll;
    private bool _resultHandled;
    private string? _invalidRequest;

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
        if (_systems is null || _context is null || _runner.Test is null)
            return;

        float time = (float)Time.GetTicksMsec() / 1000f;
        _systems.Update(new UpdateTick((float)delta, time));

        if (_resultHandled)
            return;

        VisualTestResult? result = _runner.Update(delta);

        if (result is { } completed)
        {
            _resultHandled = true;
            CompleteTest(completed);
            return;
        }

        UpdateHud();
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
        _resultHandled = false;

        _entities = new EntityStore();
        _pointer = new ScriptedPointerInputSource();
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

        _context = new VisualTestContext(
            _entities,
            _systems,
            screen,
            this,
            _pointer);

        SceneTest test = _tests[_index];

        try
        {
            test.Attach(_context);
            _runner.Start(test);
        }
        catch (Exception exception)
        {
            _resultHandled = true;
            CompleteTest(VisualTestResult.Fail(exception.Message));
            GD.PrintErr($"[scene-test:{test.Slug}] start failed: {exception}");
            return;
        }

        if (_hudLayer is not null)
            _hudLayer.Visible = !_auto;

        UpdateHud();
    }

    private void CompleteTest(VisualTestResult result)
    {
        SceneTest? test = _runner.Test;
        if (test is null)
            return;

        _runs.Add(new VisualTestRun(test.Name, result.Passed, result.Message));

        GD.Print(
            $"[scene-test:{test.Slug}] " +
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
        GD.Print(failed ? "Scene tests failed." : "Scene tests passed.");
        GetTree().Quit(failed ? 1 : 0);
    }

    private void SwitchTest(int direction)
    {
        int next = Mathf.Wrap(_index + direction, 0, _tests.Count);
        StartTest(next);
    }

    private void Cleanup()
    {
        foreach (Node child in GetChildren())
        {
            if (child is Writer.Editor.EditorOverlay overlay)
                overlay.Close();
        }

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
        _pointer = null;
        _director = null;
    }

    private void BuildHud()
    {
        _hudLayer = new CanvasLayer { Layer = 2000 };
        AddChild(_hudLayer);

        float height = GetViewportRect().Size.Y;
        const float sidebarWidth = 320;

        var sidebar = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(sidebarWidth, height),
            Color = new Color(0.02f, 0.03f, 0.06f, 0.9f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _hudLayer.AddChild(sidebar);

        var separator = new ColorRect
        {
            Position = new Vector2(sidebarWidth, 0),
            Size = new Vector2(1, height),
            Color = new Color(0.25f, 0.33f, 0.45f, 0.9f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _hudLayer.AddChild(separator);

        _hud = new Label
        {
            Position = new Vector2(16, 16),
            Size = new Vector2(sidebarWidth - 32, height - 32),
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        _hud.AddThemeFontSizeOverride("font_size", 13);
        _hud.AddThemeColorOverride("font_color", new Color("#e2e8f0"));
        _hud.AddThemeColorOverride("font_outline_color", new Color("#020617"));
        _hud.AddThemeConstantOverride("outline_size", 3);
        _hudLayer.AddChild(_hud);
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

        SceneTest? test = _runner.Test;

        if (test is null)
        {
            _hud.Text = "SCENE TESTS\nNo test loaded";
            return;
        }

        var lines = new List<string>
        {
            $"SCENE TESTS  {_index + 1}/{_tests.Count}",
            "",
        };

        for (int i = 0; i < _tests.Count; i++)
            lines.Add($"{(i == _index ? ">" : " ")} {_tests[i].Name}");

        lines.Add("");
        lines.Add("STEPS");

        foreach (SceneTestStep step in test.Steps)
        {
            string marker = step.State switch
            {
                SceneTestStepState.Running => "[>]",
                SceneTestStepState.Passed => "[x]",
                SceneTestStepState.Failed => "[!]",
                _ => "[ ]",
            };

            lines.Add($"{marker} {step.Description}");
        }

        lines.Add("");

        if (_runner.Result is { } result)
        {
            lines.Add(
                $"{(result.Passed ? "PASS" : "FAIL")}  {result.Message}");
        }
        else
        {
            lines.Add("RUNNING...");
        }

        lines.Add("");
        lines.Add("LEFT/RIGHT  switch");
        lines.Add("R           rerun");
        lines.Add("Esc         quit");

        string text = string.Join("\n", lines);
        if (_hud.Text != text)
            _hud.Text = text;
    }

    private void PrintTestList()
    {
        GD.Print("Available scene tests:");

        foreach (SceneTest test in _tests)
            GD.Print($"  {test.Slug}  {test.Name}");
    }

    private readonly record struct VisualTestRun(
        string Name,
        bool Passed,
        string Message);
}
