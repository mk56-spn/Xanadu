using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Editor;
using Xanadu.Settings;
using Xanadu.Theme;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Interactive visual test suite. The left sidebar — built as ECS UI — lists
/// every scene test; the right pane hosts the live ECS scene. Run All walks the
/// suite, marking each test passed or failed without exiting unless
/// <c>--visual-test-exit</c> is set. No Godot UI widget is created here: the
/// root <see cref="Control"/> only supplies a canvas item, and the pane is a
/// <see cref="Node2D"/> whose transform puts the scene beside the sidebar.
/// </summary>
public partial class VisualTestMain : Control
{
    private readonly List<SceneTest> _tests = new(SceneTestCatalog.All);
    private readonly List<VisualTestStatus> _statuses = new();
    private readonly SceneTestRunner _runner = new();

    private EntityStore? _entities;
    private XanaduSystemRoot? _systems;
    private SceneDirector? _director;
    private ScriptedPointerInputSource? _pointer;
    private ScriptedKeyboardState? _keyboard;
    private VisualTestContext? _context;

    private Node2D? _testHost;
    private VisualTestSidebar? _sidebar;
    private Vector2 _paneSize;

    private int _index = -1;
    private bool _resultHandled;
    private bool _suiteRunning;
    private bool _autoSingle;
    private bool _exitWhenDone;
    private int _pendingStartIndex = -1;
    private ulong _pendingStartAtMs;
    private string? _shotPath;
    private int _shotFrames = -1;

    public override void _Ready()
    {
        SetProcess(true);
        BuildUi();
        ParseArguments();

        if (_invalidRequest is not null)
        {
            GD.PrintErr($"Unknown visual test '{_invalidRequest}'.");
            PrintTestList();
            GetTree().Quit(2);
            return;
        }

        EnsureRoot();

        if (_runAllRequested)
        {
            StartSuite();
        }
        else if (_requestedIndex >= 0)
        {
            _autoSingle = true;
            StartTest(_requestedIndex, execute: true);
        }
        else
        {
            SelectTest(0);
        }

        _shotFrames = _shotPath is null ? -1 : 0;
    }

    private string? _invalidRequest;
    private int _requestedIndex = -1;
    private bool _runAllRequested;

    public override void _Process(double delta)
    {
        float time = (float)Time.GetTicksMsec() / 1000f;
        _sidebar?.Frame((float)delta, time);

        while (_sidebar is not null && _sidebar.TryTake(out VisualTestAction action))
            Handle(action);

        if (TryCaptureShot())
            return;

        if (_pendingStartIndex >= 0 &&
            Time.GetTicksMsec() >= _pendingStartAtMs)
        {
            int next = _pendingStartIndex;
            _pendingStartIndex = -1;
            StartTest(next, execute: true);
            return;
        }

        if (_systems is null || _context is null || _runner.Test is null)
            return;

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

        UpdateDetail();
    }

    public override void _Input(InputEvent @event) => _sidebar?.Feed(@event);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.Escape:
                GetTree().Quit();
                break;
            case Key.Left:
                SelectTest(Mathf.Max(0, _index - 1));
                break;
            case Key.Right:
                SelectTest(Mathf.Min(_tests.Count - 1, _index + 1));
                break;
            case Key.R:
            case Key.Enter:
                if (!_suiteRunning)
                    RunTest(_index);
                break;
        }
    }

    public override void _ExitTree()
    {
        CloseTestOverlays();
        _sidebar?.Dispose();
        _sidebar = null;
    }

    private void BuildUi()
    {
        _paneSize = new Vector2(
            VisualTestSidebar.DesignWidth - VisualTestSidebar.SidebarWidth,
            VisualTestSidebar.DesignHeight);

        _testHost = new Node2D
        {
            Name = "TestHost",
            Position = new Vector2(VisualTestSidebar.SidebarWidth, 0),
        };
        AddChild(_testHost);

        for (int i = 0; i < _tests.Count; i++)
            _statuses.Add(VisualTestStatus.Pending);

        _sidebar = new VisualTestSidebar(this, _tests);
    }

    /// <summary>
    /// Drops the sidebar and shows the scene across the whole window, so a shot
    /// sees the full 1920-wide stage, right-hand HUD included.
    /// </summary>
    private void ShowStageOnly()
    {
        _sidebar?.Dispose();
        _sidebar = null;
        if (_testHost is not null)
            _testHost.Position = Vector2.Zero;
    }

    private void ParseArguments()
    {
        string? requested = null;
        bool exit = false;

        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--visual-test=", StringComparison.Ordinal))
                requested = argument["--visual-test=".Length..];
            else if (argument == "--visual-test-exit")
                exit = true;
            else if (argument.StartsWith("--visual-test-shot=", StringComparison.Ordinal))
                _shotPath = argument["--visual-test-shot=".Length..];
            else if (argument == "--visual-test-full")
                ShowStageOnly();
        }

        _exitWhenDone = exit;
        if (requested is null)
            return;

        if (requested.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            _runAllRequested = true;
            return;
        }

        for (int i = 0; i < _tests.Count; i++)
        {
            if (_tests[i].Slug.Equals(requested, StringComparison.OrdinalIgnoreCase) ||
                _tests[i].Name.Equals(requested, StringComparison.OrdinalIgnoreCase))
            {
                _requestedIndex = i;
                return;
            }
        }

        _invalidRequest = requested;
    }

    private void Handle(VisualTestAction action)
    {
        switch (action.Kind)
        {
            case VisualTestActionKind.RunAll:
                StartSuite();
                break;
            case VisualTestActionKind.Run:
                if (!_suiteRunning)
                    RunTest(action.Index);
                break;
            case VisualTestActionKind.Select:
                OnTestPressed(action.Index);
                break;
        }
    }

    private void DisposeCurrentRoot()
    {
        CloseTestOverlays();
        if (_entities is not null)
        {
            foreach (Entity entity in _entities.Entities.ToEntityList())
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
        _keyboard = null;
        _director = null;
    }

    private void EnsureRoot()
    {
        SongStore.Shared = new SongStore(
            catalogPath: ProjectSettings.GlobalizePath(SongStore.CatalogFile));
        if (_systems is not null || _testHost is null)
            return;

        _entities = new EntityStore();
        _pointer = new ScriptedPointerInputSource();
        _keyboard = new ScriptedKeyboardState();
        _director = new SceneDirector();

        _systems = new XanaduSystemRoot(
            _entities,
            _testHost.GetCanvasItem(),
            _pointer,
            _director,
            _paneSize,
            new KeybindSettings(persist: false),
            transitions: false,
            keyboard: _keyboard,
            passHost: _testHost);

        _context = new VisualTestContext(
            _entities,
            _systems,
            _paneSize,
            _testHost,
            _pointer,
            _keyboard);
    }

    private void OnTestPressed(int index)
    {
        if (_suiteRunning)
            return;

        if (index != _index)
        {
            SelectTest(index);
            return;
        }

        RunTest(index);
    }

    private void SelectTest(int index)
    {
        if (index < 0 || index >= _tests.Count || _suiteRunning)
            return;

        _index = index;
        _autoSingle = false;
        _resultHandled = false;
        _runner.Clear();
        DisposeCurrentRoot();
        EnsureRoot();

        SceneTest test = _tests[_index];
        test.Attach(_context!);
        SetStatus(_index, VisualTestStatus.Pending);
        _sidebar?.SetSelected(_index);
        _sidebar?.SetSummary(
            $"{test.Name} selected. Press again to run.",
            Palette.TextMuted);
        _sidebar?.SetDetail(
            [new VisualTestDetailLine(
                "Selected. Press the row again or press R to run it.",
                Palette.TextDim)]);
    }

    private void RunTest(int index)
    {
        if (_suiteRunning || index < 0 || index >= _tests.Count)
            return;

        _autoSingle = true;
        StartTest(index, execute: true);
    }

    private void StartSuite()
    {
        if (_suiteRunning || _sidebar is null)
            return;

        _suiteRunning = true;
        _autoSingle = false;
        _sidebar.SetBusy(true);

        for (int i = 0; i < _statuses.Count; i++)
            SetStatus(i, VisualTestStatus.Pending);

        _sidebar.SetSummary(
            $"Running all {_tests.Count} tests...",
            Palette.Accent);
        StartTest(0, execute: true);
    }

    private void StartTest(int index, bool execute)
    {
        if (index < 0 || index >= _tests.Count)
            return;

        _index = index;
        _resultHandled = false;
        _pendingStartIndex = -1;
        _runner.Clear();
        DisposeCurrentRoot();
        EnsureRoot();

        SceneTest test = _tests[_index];
        test.Attach(_context!);

        if (execute)
        {
            SetStatus(_index, VisualTestStatus.Running);
            _runner.Start(test);
        }
        else
        {
            SetStatus(_index, VisualTestStatus.Pending);
        }

        _sidebar?.SetSelected(_index);
        UpdateDetail();
    }

    private void CompleteTest(VisualTestResult result)
    {
        SceneTest? test = _runner.Test;
        if (test is null || _index < 0 || _index >= _tests.Count)
            return;

        SetStatus(
            _index,
            result.Passed ? VisualTestStatus.Passed : VisualTestStatus.Failed);
        UpdateDetail();

        GD.Print(
            $"[scene-test:{test.Slug}] " +
            $"{(result.Passed ? "PASS" : "FAIL")}: {result.Message}");

        if (_suiteRunning)
        {
            if (_index + 1 < _tests.Count)
            {
                _pendingStartIndex = _index + 1;
                _pendingStartAtMs = Time.GetTicksMsec() + 150;
                return;
            }

            FinishSuite();
            return;
        }

        if (_autoSingle)
        {
            _autoSingle = false;
            _sidebar?.SetSummary(
                $"{(result.Passed ? "PASS" : "FAIL")}: {test.Name}",
                result.Passed ? Palette.SuccessBright : Palette.Danger);
            if (_exitWhenDone)
            {
                PrintSummary();
                GetTree().Quit(result.Passed ? 0 : 1);
            }
        }
    }

    private void FinishSuite()
    {
        _suiteRunning = false;
        _sidebar?.SetBusy(false);
        int passed = 0;
        foreach (VisualTestStatus status in _statuses)
        {
            if (status == VisualTestStatus.Passed)
                passed++;
        }

        bool clean = passed == _tests.Count;
        _sidebar?.SetSummary(
            $"Stopped. {passed}/{_tests.Count} passed.",
            clean ? Palette.SuccessBright : Palette.Danger);
        PrintSummary();

        if (_exitWhenDone)
            GetTree().Quit(clean ? 0 : 1);
    }

    private void SetStatus(int index, VisualTestStatus status)
    {
        if (index < 0 || index >= _statuses.Count)
            return;

        _statuses[index] = status;
        _sidebar?.SetStatus(index, status);
    }

    private void PrintSummary()
    {
        bool failed = _statuses.Contains(VisualTestStatus.Failed);
        GD.Print(failed ? "Scene tests failed." : "Scene tests passed.");
    }

    private void UpdateDetail()
    {
        if (_sidebar is null)
            return;

        SceneTest? test = _runner.Test;
        if (test is null)
            return;

        var lines = new List<VisualTestDetailLine>
        {
            new(test.Name, Palette.Text),
            new("", Palette.TextDim),
        };

        int focus = -1;
        foreach (SceneTestStep step in test.Steps)
        {
            // The first unfinished step, so the view holds still between two steps.
            if (focus < 0 && step.State != SceneTestStepState.Passed)
                focus = lines.Count;

            lines.Add(new VisualTestDetailLine(step.Description, StepColor(step.State)));
        }

        lines.Add(new VisualTestDetailLine("", Palette.TextDim));
        if (_runner.Result is { } result)
        {
            lines.Add(new VisualTestDetailLine(
                $"{(result.Passed ? "PASS" : "FAIL")}  {result.Message}",
                result.Passed ? Palette.SuccessBright : Palette.Danger));

            if (result.Passed)
                focus = lines.Count - 1;
        }
        else
        {
            lines.Add(new VisualTestDetailLine("RUNNING...", Palette.Accent));
        }

        _sidebar.SetDetail(lines, focus);
    }

    /// <summary>A check stays grey until it passes, the same colours as the test rows.</summary>
    private static Color StepColor(SceneTestStepState state) => state switch
    {
        SceneTestStepState.Running => Palette.Accent,
        SceneTestStepState.Passed => Palette.SuccessBright,
        SceneTestStepState.Failed => Palette.Danger,
        _ => Palette.TextDim,
    };

    private void CloseTestOverlays()
    {
        if (_testHost is null)
            return;

        foreach (Node child in _testHost.GetChildren())
        {
            if (child is EditorOverlay overlay)
            {
                overlay.Close();
                overlay.QueueFree();
            }
        }
    }

    /// <summary>
    /// Saves the rendered window to <c>--visual-test-shot=path</c> once it has
    /// settled, then quits. Reading our own viewport needs neither the
    /// compositor nor the window being visible.
    /// </summary>
    private bool TryCaptureShot()
    {
        if (_shotPath is null || _shotFrames < 0)
            return false;

        // A test that runs past the first 60 frames is shot the moment it finishes.
        bool running = _pendingStartIndex >= 0 || (_runner.Test is not null && _runner.Result is null);
        if (++_shotFrames < 60 || running)
            return false;

        _shotFrames = -1;
        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(_shotPath);
        if (error == Error.Ok)
            GD.Print($"Wrote {_shotPath}");
        else
            GD.PrintErr($"Could not write {_shotPath}: {error}");

        GetTree().Quit(error == Error.Ok ? 0 : 1);
        return true;
    }

    private void PrintTestList()
    {
        GD.Print("Available scene tests:");

        foreach (SceneTest test in _tests)
            GD.Print($"  {test.Slug}  {test.Name}");
    }
}
