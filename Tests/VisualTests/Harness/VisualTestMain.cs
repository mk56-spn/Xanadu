using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Settings;
using Writer.Systems.Input;

namespace Writer.Tests.VisualTests;

/// <summary>
/// Interactive visual test suite. The left sidebar lists every scene test;
/// the right pane hosts the live ECS scene. Run All walks the suite, marking
/// each test passed or failed without exiting unless --visual-test-exit is set.
/// </summary>
public partial class VisualTestMain : Control
{
    private const int SidebarWidth = 340;

    private readonly List<SceneTest> _tests = new(SceneTestCatalog.All);
    private readonly List<Button> _testButtons = new();
    private readonly List<Button> _runButtons = new();
    private readonly List<TestStatus> _statuses = new();
    private readonly SceneTestRunner _runner = new();

    private EntityStore? _entities;
    private WriterSystemRoot? _systems;
    private SceneDirector? _director;
    private ScriptedPointerInputSource? _pointer;
    private ScriptedKeyboardState? _keyboard;
    private VisualTestContext? _context;

    private SubViewport? _viewport;
    private Control? _testHost;
    private VBoxContainer? _testList;
    private Label? _summary;
    private Label? _detail;
    private Button? _runAll;
    private Vector2 _paneSize;

    private int _index = -1;
    private bool _resultHandled;
    private bool _suiteRunning;
    private bool _autoSingle;
    private bool _exitWhenDone;
    private int _pendingStartIndex = -1;
    private ulong _pendingStartAtMs;

    private enum TestStatus
    {
        Pending,
        Running,
        Passed,
        Failed,
    }

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
    }

    private string? _invalidRequest;
    private int _requestedIndex = -1;
    private bool _runAllRequested;

    public override void _Process(double delta)
    {
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

        UpdateDetail();
    }

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
    }

    private void BuildUi()
    {
        Vector2 window = GetViewportRect().Size;
        if (window.X <= 0 || window.Y <= 0)
            window = new Vector2(1920, 1080);

        _paneSize = new Vector2(
            Mathf.Max(640, window.X - SidebarWidth),
            window.Y);

        var background = new ColorRect
        {
            Position = Vector2.Zero,
            Size = window,
            Color = new Color("#05070b"),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(background);

        var viewportContainer = new SubViewportContainer
        {
            Position = new Vector2(SidebarWidth, 0),
            Size = _paneSize,
            Stretch = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(viewportContainer);

        _viewport = new SubViewport
        {
            Size = (Vector2I)_paneSize,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false,
            GuiDisableInput = true,
        };
        viewportContainer.AddChild(_viewport);

        _testHost = new Control
        {
            Position = Vector2.Zero,
            Size = _paneSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _viewport.AddChild(_testHost);

        var sidebar = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(SidebarWidth, window.Y),
            Color = new Color("#0b111b"),
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(sidebar);

        var content = new VBoxContainer
        {
            Position = new Vector2(16, 16),
            Size = new Vector2(SidebarWidth - 32, window.Y - 32),
            CustomMinimumSize = new Vector2(SidebarWidth - 32, 0),
        };
        sidebar.AddChild(content);

        var title = new Label
        {
            Text = "SCENE TESTS",
            CustomMinimumSize = new Vector2(0, 34),
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        content.AddChild(title);

        _runAll = new Button
        {
            Text = "RUN ALL",
            CustomMinimumSize = new Vector2(0, 38),
        };
        _runAll.Pressed += StartSuite;
        content.AddChild(_runAll);

        _summary = new Label
        {
            Text = "Ready.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 44),
        };
        content.AddChild(_summary);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        content.AddChild(scroll);

        _testList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        scroll.AddChild(_testList);

        for (int i = 0; i < _tests.Count; i++)
        {
            int index = i;
            _statuses.Add(TestStatus.Pending);

            var row = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(0, 34),
            };
            _testList.AddChild(row);

            var run = new Button
            {
                Text = "▶",
                Flat = true,
                TooltipText = $"Run {_tests[index].Name}",
                CustomMinimumSize = new Vector2(28, 34),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            run.AddThemeColorOverride("font_color", new Color("#4ade80"));
            run.AddThemeColorOverride("font_hover_color", new Color("#86efac"));
            run.AddThemeFontSizeOverride("font_size", 18);
            run.Pressed += () => OnRunPressed(index);
            _runButtons.Add(run);
            row.AddChild(run);

            var button = new Button
            {
                Text = _tests[index].Name,
                Alignment = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 34),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            button.Pressed += () => OnTestPressed(index);
            _testButtons.Add(button);
            row.AddChild(button);
        }

        _detail = new Label
        {
            Text = "Select a test, then press it again or press Run All.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 180),
            VerticalAlignment = VerticalAlignment.Top,
        };
        content.AddChild(_detail);
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
        if (_systems is not null || _testHost is null)
            return;

        _entities = new EntityStore();
        _pointer = new ScriptedPointerInputSource();
        _keyboard = new ScriptedKeyboardState();
        _director = new SceneDirector();

        _systems = new WriterSystemRoot(
            _entities,
            _testHost.GetCanvasItem(),
            _pointer,
            _director,
            _paneSize,
            new KeybindSettings(persist: false),
            transitions: false,
            keyboard: _keyboard);

        _context = new VisualTestContext(
            _entities,
            _systems,
            _paneSize,
            _testHost,
            _pointer,
            _keyboard);
    }

    private void OnRunPressed(int index)
    {
        if (_suiteRunning)
            return;

        RunTest(index);
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
        _statuses[_index] = TestStatus.Pending;
        UpdateButtons();
        _summary!.Text = $"{test.Name} selected. Press again to run.";
        _detail!.Text = "Selected. Press the row again or press R to run it.";
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
        if (_suiteRunning || _testList is null)
            return;

        _suiteRunning = true;
        _autoSingle = false;
        _runAll!.Disabled = true;

        for (int i = 0; i < _statuses.Count; i++)
            _statuses[i] = TestStatus.Pending;

        UpdateButtons();
        _summary!.Text = $"Running all {_tests.Count} tests...";
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
            _statuses[_index] = TestStatus.Running;
            _runner.Start(test);
            }
        else
        {
            _statuses[_index] = TestStatus.Pending;
        }

        UpdateButtons();
        UpdateDetail();
    }

    private void CompleteTest(VisualTestResult result)
    {
        SceneTest? test = _runner.Test;
        if (test is null || _index < 0 || _index >= _tests.Count)
            return;

        _statuses[_index] = result.Passed ? TestStatus.Passed : TestStatus.Failed;
        UpdateButtons();
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
            _summary!.Text =
                $"{(result.Passed ? "PASS" : "FAIL")}: {test.Name}";
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
        _runAll!.Disabled = false;
        int passed = 0;
        foreach (TestStatus status in _statuses)
        {
            if (status == TestStatus.Passed)
                passed++;
        }

        _summary!.Text = $"Stopped. {passed}/{_tests.Count} passed.";
        PrintSummary();

        if (_exitWhenDone)
            GetTree().Quit(passed == _tests.Count ? 0 : 1);
    }

    private void PrintSummary()
    {
        bool failed = _statuses.Contains(TestStatus.Failed);
        GD.Print(failed ? "Scene tests failed." : "Scene tests passed.");
    }

    private void UpdateButtons()
    {
        for (int i = 0; i < _testButtons.Count; i++)
        {
            Button button = _testButtons[i];
            button.Text = DisplayText(i);
            button.Disabled = _suiteRunning;
            _runButtons[i].Disabled = _suiteRunning;
            button.AddThemeColorOverride(
                "font_color",
                _statuses[i] switch
                {
                    TestStatus.Running => new Color("#facc15"),
                    TestStatus.Passed => new Color("#4ade80"),
                    TestStatus.Failed => new Color("#f87171"),
                    _ => new Color("#cbd5e1"),
                });
        }
    }

    private string DisplayText(int index)
    {
        string marker = _statuses[index] switch
        {
            TestStatus.Running => ">",
            TestStatus.Passed => "+",
            TestStatus.Failed => "!",
            _ => " ",
        };

        string selected = index == _index ? ">" : " ";
        return $"{marker}{selected} {_tests[index].Name}";
    }

    private void UpdateDetail()
    {
        if (_detail is null)
            return;

        SceneTest? test = _runner.Test;
        if (test is null)
            return;

        var lines = new List<string>
        {
            test.Name,
            "",
        };

        foreach (SceneTestStep step in test.Steps)
        {
            string marker = step.State switch
            {
                SceneTestStepState.Running => ">",
                SceneTestStepState.Passed => "[x]",
                SceneTestStepState.Failed => "[!]",
                _ => "[ ]",
            };
            lines.Add($"{marker} {step.Description}");
        }

        if (_runner.Result is { } result)
        {
            lines.Add("");
            lines.Add($"{(result.Passed ? "PASS" : "FAIL")}  {result.Message}");
        }
        else
        {
            lines.Add("");
            lines.Add("RUNNING...");
        }

        _detail.Text = string.Join("\n", lines);
    }

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

    private void PrintTestList()
    {
        GD.Print("Available scene tests:");

        foreach (SceneTest test in _tests)
            GD.Print($"  {test.Slug}  {test.Name}");
    }
}
