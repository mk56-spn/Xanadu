using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Friflo.Engine.ECS;
using MoonWorks.Input;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Labs.Editor;
using Xanadu.Storage;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A shader editor inside the lab, the start of the one the game will get: a
/// level shader slot typed on the left, compiled as you type and drawn on the
/// right. Slang's problems mark their lines; calls into the library show their
/// parameter names as inlay hints. The text saves itself to a git-ignored file.
/// </summary>
internal sealed class ShaderEditor : ILabExperiment
{
    private const string WorkingCopy = "res://Tools/Labs/Playground/editor.slang";
    private const string Template = "res://Tools/Labs/Playground/editor.template.slang";
    private const string Label = "editor";

    public string Name => "SHADER EDITOR";

    /// <summary>The editor lays out the whole body itself; its help sits under the problem list.</summary>
    public bool FullWidth => true;

    public string[] Notes => [];

    public ILabStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(context, parent, area);

    private static string WorkingFile()
    {
        string file = GamePaths.Globalize(WorkingCopy);
        if (!File.Exists(file))
            File.Copy(GamePaths.Globalize(Template), file);
        return file;
    }

    private sealed class Stage : ILabStage
    {
        /// <summary>How long typing has to pause before a compile starts.</summary>
        private const float Settle = 0.3f;

        private const float RepeatDelay = 0.4f, RepeatEvery = 0.033f;

        /// <summary>A second click on the same place within this long selects the word.</summary>
        private const float DoubleClick = 0.4f;

        private static readonly KeyCode[] Repeating =
        [
            KeyCode.Left, KeyCode.Right, KeyCode.Up, KeyCode.Down, KeyCode.Backspace, KeyCode.Delete,
            KeyCode.Return, KeyCode.PageUp, KeyCode.PageDown, KeyCode.Tab,
        ];

        private const float Gap = 12;
        private const float StatusHeight = 32;

        /// <summary>The right-hand column's width: the preview at 16:9 on top, problems and help under it.</summary>
        private const float SideWidth = 720;

        private readonly LabContext _context;
        private readonly EditorChrome _chrome;
        private readonly EditorTuning _tuning;
        private readonly Handle _codeItem;
        private readonly Handle _previewItem;
        private readonly Rect2 _preview;
        private readonly CodeView _view;
        private readonly CodeBuffer _buffer = new();
        private readonly CodeHistory _history;

        /// <summary>Set by an edit this frame that is not plain typing, so it gets an undo step of its own.</summary>
        private bool _breaksRun;

        /// <summary>Set when this frame undid or redid, so the completion list doesn't open on the restored text.</summary>
        private bool _steppedHistory;
        private readonly SlangHints _hints;
        private readonly SlangCompletions _completions;
        private readonly CompletionPopup _popup;
        private readonly string _file;
        private readonly Dictionary<KeyCode, float> _held = new();
        private List<SlangProblem> _shownProblems = [];

        /// <summary>The editor's own warnings (<see cref="SlangLint"/>) for the text being compiled.</summary>
        private List<SlangProblem> _lint = [];

        /// <summary>Every type a local can be declared with, for the lint: Slang's and the library's.</summary>
        private readonly HashSet<string> _typeNames;
        private CanvasMaterial? _material;
        private int[] _wanted = [];
        private int _compiledVersion = -1;
        private float _sinceEdit;
        private float _sinceCaretMove;
        private bool _wasHeld;

        /// <summary>A press that began in the code: holding it drags the selection.</summary>
        private bool _dragging;

        private float _lastClick = -1;
        private (int Line, int Column) _lastClickAt = (-1, -1);
        private bool _shift;
        private bool _everCompiled;
        private Vector2 _pointer;
        private string _status = "compiling…";
        private Color _statusColour = Palette.TextMuted;
        private Task<(int Version, string Source, ShaderParam[] Params, string? Error, long Ms)>? _compiling;

        public Stage(LabContext context, Handle parent, Rect2 area)
        {
            _context = context;
            float codeWidth = area.Size.X - SideWidth - Gap;
            var codeArea = new Rect2(area.Position, new Vector2(codeWidth, area.Size.Y - StatusHeight));
            var bar = new Rect2(area.Position.X, codeArea.End.Y, codeWidth, StatusHeight);
            float sideLeft = codeArea.End.X + Gap;
            // The game's playfield is 16:9, so the preview is too: a shader looks here as it will there.
            _preview = new Rect2(sideLeft, area.Position.Y, SideWidth, MathF.Round(SideWidth * 9f / 16f));
            var side = new Rect2(sideLeft, _preview.End.Y + Gap, SideWidth, area.End.Y - _preview.End.Y - Gap);
            _pointer = _preview.GetCenter();

            _codeItem = Child(parent);
            _previewItem = Child(parent);
            Handle chromeItem = Child(parent);
            CanvasServer.Current.AddRect(_previewItem, _preview, new Color("#0b1020"));
            IRectCanvas canvas = HostPlatform.NewCanvas();
            _view = new CodeView(canvas, _codeItem, codeArea, context.Text);
            _chrome = new EditorChrome(canvas, chromeItem, bar, _preview, side);
            _tuning = new EditorTuning(canvas, chromeItem, _chrome.TuningArea, Array.ConvertAll(LabShaderValues.All, value => value.Name));
            string library = Path.Combine(SlangShaders.LibraryPath ?? "", "xanadu");
            _hints = SlangHints.FromLibrary(library);
            _typeNames = [.. SlangSyntax.BuiltInTypes, .. _hints.Types, .. _hints.Aliases];
            _completions = SlangCompletions.FromSources(
                Directory.Exists(library) ? Directory.EnumerateFiles(library, "*.slang").Select(File.ReadAllText) : [],
                Xanadu.Rendering.ShaderConstants.Library,
                LabShaderValues.All.Select(value => (value.Name, value.Type)));
            // Made last, so the list draws over the code and everything round it.
            _popup = new CompletionPopup(canvas, Child(parent), codeArea);

            _file = WorkingFile();
            _buffer.Load(File.ReadAllText(_file));
            _history = new CodeHistory(_buffer);
            context.ClaimTyping(true);
        }

        private static Handle Child(Handle parent)
        {
            Handle item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(item, parent);
            return item;
        }

        public void Update(in LabFrame frame)
        {
            int version = _buffer.Version;
            (int line, int column) caret = (_buffer.Line, _buffer.Column);
            _shift = frame.Inputs is { } held && (held.Keyboard.IsDown(KeyCode.LeftShift) || held.Keyboard.IsDown(KeyCode.RightShift));
            _breaksRun = _steppedHistory = false;
            if (frame.Inputs is { } inputs)
                Edit(inputs, frame);
            Point(frame);
            if (!_steppedHistory)
                _history.After(typing: !_breaksRun, frame.Delta);

            _popup.Follow(_buffer, _completions, edited: _buffer.Version != version && !_steppedHistory);
            if (_buffer.Version != version)
                _sinceEdit = 0;
            else
                _sinceEdit += frame.Delta;
            if (_buffer.Version != version || caret != (_buffer.Line, _buffer.Column))
            {
                _sinceCaretMove = 0;
                _view.Reveal(_buffer.Line, _shownProblems);
            }
            else
                _sinceCaretMove += frame.Delta;

            Finish();
            if (_compiling is null && _compiledVersion != _buffer.Version && _sinceEdit >= Settle)
                Compile();

            // The pointer counts only over the preview; elsewhere the shader keeps where it last was (the middle at first).
            if (_preview.HasPoint(frame.Mouse))
                _pointer = frame.Mouse;
            if (_material is not null)
            {
                LabShaderValues.Apply(_material, _wanted, _preview, _pointer, frame.Time);
                _tuning.Apply(_material);
            }

            // Solid while typing or moving, blinking once still.
            bool caretOn = _sinceCaretMove < 0.5f || (int)(_sinceCaretMove * 2) % 2 == 0;
            _view.Draw(_buffer, _hints, _shownProblems, caretOn);
            _chrome.Draw(new EditorState(
                _buffer.Line, _buffer.Column, _status, _statusColour,
                PreviewStale: _shownProblems.Exists(problem => !problem.Warning) || !_everCompiled,
                PreviewNote: _everCompiled ? "showing the last version that compiled" : "nothing has compiled yet",
                _shownProblems));
            _tuning.Draw();
            _popup.Draw(_view.CaretRect);
        }

        private void Edit(Inputs inputs, in LabFrame frame)
        {
            Keyboard keys = inputs.Keyboard;
            bool ctrl = keys.IsDown(KeyCode.LeftControl) || keys.IsDown(KeyCode.RightControl);
            bool shift = _shift;

            // Undo and redo take the whole frame, so nothing else edits around them.
            if (ctrl && (keys.IsPressed(KeyCode.Z) || keys.IsPressed(KeyCode.Y)))
            {
                if (keys.IsPressed(KeyCode.Z) && !shift)
                    _history.Undo();
                else
                    _history.Redo();
                if (_popup.Open)
                    _popup.Dismiss(_buffer);
                _steppedHistory = true;
                return;
            }

            // Anything typed over a selection replaces it: its own step, not part of a run.
            _breaksRun = _buffer.HasSelection;

            if (frame.Typed.Length > 0 && !ctrl)
                _buffer.Insert(frame.Typed);
            if (ctrl && keys.IsPressed(KeyCode.A))
                _buffer.SelectAll();
            if (ctrl && (keys.IsPressed(KeyCode.C) || keys.IsPressed(KeyCode.X)) && _buffer.HasSelection)
            {
                SDL3.SDL.SDL_SetClipboardText(_buffer.SelectedText);
                if (keys.IsPressed(KeyCode.X))
                {
                    _buffer.DeleteSelection();
                    _breaksRun = true;
                }
            }

            if (ctrl && keys.IsPressed(KeyCode.V) && SDL3.SDL.SDL_GetClipboardText() is { Length: > 0 } clip)
            {
                _buffer.Insert(clip);
                _breaksRun = true;
            }

            if (_popup.Open && keys.IsPressed(KeyCode.Escape))
                _popup.Dismiss(_buffer);
            foreach (KeyCode key in Repeating)
            {
                if (!Fires(keys, key, frame.Delta))
                    continue;
                // While the list is open its keys choose and take, not move the caret.
                if (_popup.Open && !shift && key is KeyCode.Up or KeyCode.Down or KeyCode.Return or KeyCode.Tab)
                {
                    if (key == KeyCode.Up)
                        _popup.Move(-1);
                    else if (key == KeyCode.Down)
                        _popup.Move(1);
                    else
                    {
                        _popup.Accept(_buffer);
                        _breaksRun = true;
                    }

                    continue;
                }

                Press(key, shift);
            }

            if (keys.IsPressed(KeyCode.Home))
                _buffer.Home(shift);
            if (keys.IsPressed(KeyCode.End))
                _buffer.End(shift);
        }

        /// <summary>A key's edit or move; with Shift held, moves extend the selection.</summary>
        private void Press(KeyCode key, bool shift)
        {
            switch (key)
            {
                case KeyCode.Left: _buffer.MoveColumn(-1, shift); break;
                case KeyCode.Right: _buffer.MoveColumn(1, shift); break;
                case KeyCode.Up: _buffer.MoveLine(-1, shift); break;
                case KeyCode.Down: _buffer.MoveLine(1, shift); break;
                case KeyCode.PageUp: _buffer.MoveLine(-_view.VisibleRows, shift); break;
                case KeyCode.PageDown: _buffer.MoveLine(_view.VisibleRows, shift); break;
                case KeyCode.Backspace: _buffer.Backspace(); break;
                case KeyCode.Delete: _buffer.Delete(); break;
                case KeyCode.Return:
                    _buffer.NewLine();
                    _breaksRun = true;
                    break;
                case KeyCode.Tab: _buffer.Insert(new string(' ', CodeBuffer.Indent - _buffer.Column % CodeBuffer.Indent)); break;
            }
        }

        /// <summary>A key fires when pressed, then repeatedly once held past a delay.</summary>
        private bool Fires(Keyboard keys, KeyCode key, float delta)
        {
            if (keys.IsPressed(key))
            {
                _held[key] = 0;
                return true;
            }

            if (!keys.IsDown(key) || !_held.TryGetValue(key, out float before))
            {
                _held.Remove(key);
                return false;
            }

            float now = before + delta;
            _held[key] = now;
            if (now < RepeatDelay)
                return false;
            return before < RepeatDelay || (int)((before - RepeatDelay) / RepeatEvery) != (int)((now - RepeatDelay) / RepeatEvery);
        }

        private void Point(in LabFrame frame)
        {
            // A held slider takes the pointer from everything else.
            if (_tuning.Point(frame.Mouse, frame.Held, frame.Held && !_wasHeld))
            {
                _wasHeld = frame.Held;
                return;
            }

            bool pressed = frame.Held && !_wasHeld;
            _wasHeld = frame.Held;
            if (!frame.Held)
                _dragging = false;
            bool picked = pressed && _popup.Click(_buffer, frame.Mouse);
            _breaksRun |= picked;
            if (pressed && !picked)
            {
                if (_view.Hit(_buffer, _hints, frame.Mouse) is { } hit)
                {
                    bool twice = frame.Time - _lastClick < DoubleClick && _lastClickAt == hit;
                    if (twice)
                        _buffer.SelectWord(hit.Line, hit.Column);
                    else
                        _buffer.Place(hit.Line, hit.Column, extend: _shift);
                    // A third click starts over rather than reselecting the word.
                    _lastClick = twice ? -1 : frame.Time;
                    _lastClickAt = hit;
                    _dragging = !twice;
                }
                else if (_chrome.ProblemAt(frame.Mouse) is { } problem)
                    _buffer.Place(problem.Line - 1, problem.Column - 1);
            }
            else if (_dragging && _view.Hit(_buffer, _hints, frame.Mouse, dragging: true) is { } to && to != (_buffer.Line, _buffer.Column))
                _buffer.Place(to.Line, to.Column, extend: true);

            if (frame.Inputs is { } inputs && inputs.Mouse.Wheel != 0 && _view.Area.HasPoint(frame.Mouse))
                _view.Scroll = Math.Clamp(_view.Scroll - inputs.Mouse.Wheel * 3, 0, Math.Max(0, _buffer.Lines.Count - 1));
        }

        /// <summary>Saves the text and compiles it off the frame, so typing never waits on Slang.</summary>
        private void Compile()
        {
            string source = _buffer.Text;
            int version = _buffer.Version;
            try
            {
                File.WriteAllText(_file, source);
            }
            catch (IOException)
            {
                // Saving is a convenience; the compile goes ahead.
            }

            _lint = SlangLint.Check(source, _typeNames);
            if (!_everCompiled)
                (_status, _statusColour) = ("compiling…", Palette.TextMuted);
            _compiling = Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                try
                {
                    // What the lab provides is declared for the code that uses it; the file keeps what was typed.
                    string full = LabShaderValues.Declare(source);
                    CompiledShader compiled = SlangShaders.CompileEffectBody(Label, full);
                    return (version, full, compiled.Params, (string?)null, clock.ElapsedMilliseconds);
                }
                catch (Exception exception)
                {
                    return (version, source, Array.Empty<ShaderParam>(), exception.Message, clock.ElapsedMilliseconds);
                }
            });
        }

        private void Finish()
        {
            if (_compiling is not { IsCompleted: true } done)
                return;

            _compiling = null;
            (int version, string source, ShaderParam[] declared, string? error, long ms) = done.Result;
            _compiledVersion = version;
            if (error is not null)
            {
                _shownProblems = WithLint(SlangProblems.Parse(error, Label));
                int count = _shownProblems.Count;
                (_status, _statusColour) = ($"{count} problem{(count == 1 ? "" : "s")}", Palette.DangerBright);
                return;
            }

            _shownProblems = WithLint([]);
            CanvasMaterial? previous = _material;
            _material = CanvasMaterial.FromSource(Label, source);
            CanvasServer.Current.SetMaterial(_previewItem, _material);
            _wanted = LabShaderValues.Wanted(Array.ConvertAll(declared, param => param.Name));
            _tuning.Use(declared);
            if (previous is not null)
                _context.Pipelines.Forget(previous);
            _everCompiled = true;
            int warnings = _lint.Count;
            string warned = warnings == 0 ? "" : $" · {warnings} warning{(warnings == 1 ? "" : "s")}";
            (_status, _statusColour) = ($"compiled in {ms} ms{warned}{LabShaderValues.Describe(_wanted)}", warnings == 0 ? Palette.Success : Palette.Warning);
        }

        /// <summary>Slang's problems and the editor's warnings together, by line; Slang's first on a shared line.</summary>
        private List<SlangProblem> WithLint(List<SlangProblem> problems)
        {
            var all = new List<SlangProblem>(problems);
            foreach (SlangProblem warning in _lint)
            {
                if (!problems.Exists(problem => problem.Line == warning.Line))
                    all.Add(warning);
            }

            all.Sort((a, b) => a.Line.CompareTo(b.Line));
            return all;
        }

        public void Dispose()
        {
            _context.ClaimTyping(false);
            if (_material is not null)
                _context.Pipelines.Forget(_material);
        }
    }
}
