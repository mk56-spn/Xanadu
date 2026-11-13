using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Slang;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>Where a <see cref="ShaderCodeEditor"/>'s parts go: boxes its owner reads from container slots (or, in the lab, works out).</summary>
internal readonly record struct ShaderEditorBoxes(Rect2 Code, Rect2 Bar, Rect2 Preview, Rect2 Side, Rect2 Divider);

/// <summary>
/// Slang code being edited with its preview: the text (a UI text editor), its
/// problems (Slang's and <see cref="SlangLint"/>'s, marking their lines),
/// completion, hovers, inlay hints, and a compile off the frame whenever typing
/// pauses, shown by an <see cref="IEditorPreview"/>. It owns no layout: its
/// owner hands it boxes (<see cref="Place"/>) and tells it again when they move.
/// Shared by the shader tool and the lab's editor tabs.
/// <para>
/// Going to a library name's declaration (F12, Ctrl+click) opens its module in
/// a tab of its own over the code. A library tab is read-only until the first
/// try at an edit is answered yes; Ctrl+S there writes the module and compiles
/// the shader again. The shader's own tab is always first: its text is what
/// compiles, owns the problems and the clips, and is what <see cref="Text"/> is.
/// </para>
/// </summary>
internal sealed class ShaderCodeEditor : IDisposable
{
    /// <summary>How long typing has to pause before a compile starts.</summary>
    private const float Settle = 0.3f;

    private readonly EditorChrome _chrome;
    private readonly EditorTuning _tuning;
    private readonly IEditorPreview _view;
    private readonly ClipTimeline _timeline;

    /// <summary>The boxes it was last placed in, to lay out again when the timeline wants another height.</summary>
    private ShaderEditorBoxes _boxes;
    private readonly PlayStandIns _standIns = new();
    private readonly IRectCanvas _canvas;
    private readonly Handle _chromeItem;
    private int _clipsVersion = -1;
    private Rect2 _preview;

    /// <summary>The UI text editor the code lives in; the input and drawing are its systems'.</summary>
    private readonly Entity _editor;
    private readonly SlangEditorHost _host;
    private SlangCompletions _completions;
    private SlangLibraryDeclarations _declarations;

    /// <summary>One open text: the shader's own (first, no path) or a library module's.</summary>
    private sealed class Tab(string label, string? path, TextBuffer buffer, TextHistory history, string disk)
    {
        public readonly string Label = label;
        public readonly string? Path = path;
        public readonly TextBuffer Buffer = buffer;
        public readonly TextHistory History = history;
        public string Disk = disk;
        public int Scroll;
        public bool ReadOnly = path is not null;

        /// <summary>Whether a module's text differs from its file, worked out again only when it changes.</summary>
        public bool Dirty
        {
            get
            {
                if (Path is null)
                    return false;
                if (_dirtyVersion != Buffer.Version)
                {
                    _dirtyVersion = Buffer.Version;
                    _dirty = Buffer.Text != Disk;
                }

                return _dirty;
            }
        }

        private int _dirtyVersion = -1;
        private bool _dirty;

        public void Saved()
        {
            Disk = Buffer.Text;
            _dirtyVersion = -1;
        }
    }

    private readonly List<Tab> _tabs = new();
    private int _open;
    private readonly EditorTabs _strip;
    private LibraryPlace? _goTo;
    private Tab Main => _tabs[0];
    private readonly CompletionPopup _popup;
    private readonly HoverPopup _hover;
    private List<SlangProblem> _shownProblems = [];

    /// <summary>The editor's own warnings (<see cref="SlangLint"/>) for the text being compiled.</summary>
    private List<SlangProblem> _lint = [];

    /// <summary>Every type a local can be declared with, for the lint: Slang's and the library's.</summary>
    private readonly HashSet<string> _typeNames;
    private int _compiledVersion = -1;
    private float _sinceEdit;
    private bool _wasHeld;
    private bool _everCompiled;
    private Vector2 _pointer;
    private string _status = "compiling…";
    private Color _statusColour = Palette.TextMuted;
    private Task<(int Version, string Source, PreviewBuild? Build, string? Error, long Ms)>? _compiling;

    /// <param name="preview">Makes the preview, drawing under the item it is given.</param>
    public ShaderCodeEditor(EntityStore entities, Handle parent, ShaderEditorBoxes boxes, string text, Func<Handle, IEditorPreview> preview)
    {
        _preview = boxes.Preview;
        _pointer = _preview.GetCenter();
        (Rect2 strip, Rect2 code) = SplitCode(boxes.Code);

        // Made first, so everything after draws over the code.
        _editor = TextEditors.Create(entities, parent, code, text);
        _view = preview(Child(parent));
        _view.Place(_preview);
        Handle chromeItem = Child(parent);
        IRectCanvas canvas = HostPlatform.NewCanvas();
        _canvas = canvas;
        _chromeItem = chromeItem;
        _boxes = boxes;
        (Rect2 clipsBox, Rect2 side) = UnderPreview(boxes.Side, ClipTimeline.MinHeight);
        _timeline = new ClipTimeline(entities) { Area = clipsBox, Scrubs = _view.TakesClock };
        _timeline.Jump += (line, column) =>
        {
            Show(0);
            Main.Buffer.Place(line, column);
            Focus();
        };
        _timeline.Released += Focus;
        _chrome = new EditorChrome(canvas, chromeItem, boxes.Bar, boxes.Preview, side) { Divider = boxes.Divider };
        _chrome.Help = [.. _view.Help, .. _chrome.Help];
        // The room for sliders ends above the help, so it is worked out again now the help is longer.
        _chrome.Resize(boxes.Bar, boxes.Preview, side);
        _tuning = new EditorTuning(canvas, chromeItem, _chrome.TuningArea,
            _view.Provided.Select(value => value.Name).Append(SlangShaders.ClockOverride));
        string library = Path.Combine(SlangShaders.LibraryPath ?? "", "xanadu");
        SlangHints hints = SlangHints.FromLibrary(library);
        _typeNames = [.. SlangSyntax.BuiltInTypes, .. hints.Types, .. hints.Aliases, .. _view.Types];
        (_completions, _declarations) = ReadLibrary();
        // Made last, so the boxes draw over the code and everything round it; the list over a hover box, the tabs' question over all.
        _hover = new HoverPopup(canvas, Child(parent), code);
        _popup = new CompletionPopup(canvas, Child(parent), code);
        _strip = new EditorTabs(canvas, Child(parent)) { Area = strip, Code = code };
        _host = new SlangEditorHost(hints, _popup) { Save = Save, GoElsewhere = GoElsewhere };
        ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
        editor.Host = _host;
        _tabs.Add(new Tab(_view.Label, null, editor.Buffer, editor.History, ""));
    }

    /// <summary>The library's completions and where its names are declared, from the modules as they are on disk.</summary>
    private (SlangCompletions, SlangLibraryDeclarations) ReadLibrary()
    {
        List<(string Path, string Text)> modules = SlangShaders.LibraryModules();
        string folder = Path.Combine(SlangShaders.LibraryPath ?? "", "xanadu");
        // Completions read the modules under xanadu/, as imported; the umbrella module only re-exports them.
        IEnumerable<string> sources = modules.Where(m => Path.GetDirectoryName(m.Path) == folder).Select(m => m.Text);
        var completions = SlangCompletions.FromSources(
            _view.Prelude.Length > 0 ? sources.Append(_view.Prelude) : sources,
            ShaderConstants.Library,
            _view.Provided);
        return (completions, SlangLibraryDeclarations.FromModules(modules));
    }

    /// <summary>The tab strip off the top of the code's box.</summary>
    private static (Rect2 Strip, Rect2 Code) SplitCode(Rect2 code)
    {
        var strip = new Rect2(code.Position, new Vector2(code.Size.X, EditorTabs.Height));
        return (strip, new Rect2(code.Position.X, strip.End.Y, code.Size.X, Math.Max(0, code.Size.Y - EditorTabs.Height)));
    }

    /// <summary>The name at the caret isn't declared in the open text: the library's declaration of it, opened next frame.</summary>
    private bool GoElsewhere(TextBuffer buffer)
    {
        string line = buffer.Lines[buffer.Line];
        LibraryPlace? found = _declarations.Find(line, buffer.Column);
        if (found is null && buffer.Column > 0)
            found = _declarations.Find(line, buffer.Column - 1);
        _goTo = found ?? _goTo;
        return found is not null;
    }

    /// <summary>Opens a library module in its tab (made the first time), the caret on the place, its line a third of the way down.</summary>
    private void Open(LibraryPlace place)
    {
        int index = _tabs.FindIndex(tab => tab.Path == place.Path);
        if (index < 0)
        {
            string? text = SlangShaders.LibraryModules().Find(m => m.Path == place.Path).Text;
            if (text is null)
                return;
            var buffer = new TextBuffer();
            buffer.Load(text);
            string label = Path.GetRelativePath(SlangShaders.LibraryPath ?? "", place.Path).Replace('\\', '/');
            _tabs.Add(new Tab(label, place.Path, buffer, new TextHistory(buffer), text));
            index = _tabs.Count - 1;
        }

        Show(index);
        ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
        editor.Buffer.Place(place.Line, place.Column);
        editor.Layout.Scroll = Math.Max(0, place.Line - editor.Layout.VisibleRows / 3);
    }

    /// <summary>Puts a tab's text in the editor, keeping where the last one was scrolled to.</summary>
    private void Show(int index)
    {
        ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
        if (index == _open)
            return;
        _popup.Dismiss(editor.Buffer);
        _tabs[_open].Scroll = editor.Layout.Scroll;
        _open = index;
        Tab tab = _tabs[index];
        editor.Buffer = tab.Buffer;
        editor.History = tab.History;
        editor.Layout.Scroll = tab.Scroll;
        editor.ReadOnly = tab.ReadOnly;
        editor.Dragging = false;
        _host.ReadOnly = tab.ReadOnly;
        _host.Forget();
    }

    /// <summary>Closes a library tab, asking first when it has unsaved changes.</summary>
    private void Close(int index)
    {
        if (index <= 0 || index >= _tabs.Count)
            return;
        void Drop()
        {
            if (_open == index)
                Show(index - 1);
            else if (_open > index)
                _open--;
            _tabs.RemoveAt(index);
        }

        if (_tabs[index].Dirty)
            _strip.Question = new EditorQuestion($"Close {_tabs[index].Label}?", "Its unsaved changes will be lost.", "DISCARD", Drop);
        else
            Drop();
    }

    /// <summary>Ctrl+S: a library tab writes its module and the shader compiles again; the shader's own goes to the owner.</summary>
    private void Save()
    {
        Tab tab = _tabs[_open];
        if (tab.Path is null)
        {
            SaveRequested?.Invoke();
            return;
        }

        if (!tab.Dirty)
            return;
        if (!SlangShaders.WriteLibraryModule(tab.Path, tab.Buffer.Text, out string? error))
        {
            (_status, _statusColour) = ($"{tab.Label} not saved: {error}", Palette.DangerBright);
            return;
        }

        tab.Saved();
        (_completions, _declarations) = ReadLibrary();
        // The shader imports what was just written: compile it again.
        _compiledVersion = -1;
        _sinceEdit = Settle;
    }

    /// <summary>An edit was tried on a read-only library tab: ask whether to allow it.</summary>
    private void AskToEdit()
    {
        Tab tab = _tabs[_open];
        _strip.Question = new EditorQuestion(
            $"Edit {tab.Label}?", "It is the shared library: every shader that imports it changes.", "EDIT ANYWAY", () =>
            {
                tab.ReadOnly = false;
                if (_tabs[_open] == tab)
                {
                    _editor.GetComponent<TextEditor>().ReadOnly = false;
                    _host.ReadOnly = false;
                }

                Focus();
            });
    }

    /// <summary>Answers the open question from a press or Enter / Esc; true while one is open (the pointer is its).</summary>
    private bool Ask(in ToolFrame frame, bool pressed)
    {
        if (_strip.Question is not { } question)
            return false;
        bool? answer = pressed ? _strip.AnswerAt(frame.Mouse) : null;
        if (frame.Inputs is { } inputs)
        {
            if (inputs.Keyboard.IsPressed(MoonWorks.Input.KeyCode.Return) || inputs.Keyboard.IsPressed(MoonWorks.Input.KeyCode.KeypadEnter))
                answer = true;
            else if (inputs.Keyboard.IsPressed(MoonWorks.Input.KeyCode.Escape))
                answer = false;
        }

        if (answer is { } yes)
        {
            _strip.Question = null;
            if (yes)
                question.OnYes();
            else
                Focus();
        }

        return true;
    }

    /// <summary>The shader's text as it is now (its own tab's, whichever is open).</summary>
    public string Text => Main.Buffer.Text;

    /// <summary>Goes up with every edit of the shader's text.</summary>
    public int Version => Main.Buffer.Version;

    /// <summary>Ctrl+S in the code. Without a handler the key does nothing.</summary>
    public event Action? SaveRequested;

    /// <summary>Called with the text just before each compile; the lab saves its working copy there.</summary>
    public Action<string>? Compiling { get; init; }

    /// <summary>Lit while the owner drags the divider or the pointer is over it.</summary>
    public bool DividerLit { set => _chrome.DividerLit = value; }

    /// <summary>Puts the code back in its box and focused, after a press elsewhere took its focus.</summary>
    public void Focus() => _editor.AddTag<TextEditorFocused>();

    private static Handle Child(Handle parent)
    {
        Handle item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(item, parent);
        return item;
    }

    /// <summary>The column under the preview split: the clip timeline on top, the problems and the rest under it.</summary>
    private static (Rect2 Clips, Rect2 Others) UnderPreview(Rect2 side, float timeline)
    {
        const float Gap = 8;
        var clips = new Rect2(side.Position, new Vector2(side.Size.X, timeline));
        var rest = new Rect2(side.Position.X, clips.End.Y + Gap, side.Size.X, Math.Max(0, side.Size.Y - timeline - Gap));
        return (clips, rest);
    }

    /// <summary>Moves everything to new boxes: the code, the preview quad (so the shader sees its new size), the column and the popups' bounds.</summary>
    public void Place(ShaderEditorBoxes boxes)
    {
        _boxes = boxes;
        ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
        (Rect2 strip, Rect2 code) = SplitCode(boxes.Code);
        editor.Layout.Area = code;
        CanvasServer.Current.SetCustomRect(editor.Item, true, code);
        _hover.Bounds = code;
        _popup.Bounds = code;
        _strip.Area = strip;
        _strip.Code = code;

        // The pointer keeps its place on the preview as a fraction of it.
        Vector2 at = (_pointer - _preview.Position) / _preview.Size;
        _preview = boxes.Preview;
        _pointer = _preview.Position + at * _preview.Size;
        _view.Place(_preview);

        (Rect2 clipsBox, Rect2 side) = UnderPreview(boxes.Side, _timeline.Height);
        _timeline.Area = clipsBox;
        _chrome.Resize(boxes.Bar, boxes.Preview, side);
        _chrome.Divider = boxes.Divider;
        _tuning.Area = _chrome.TuningArea;
    }

    public void Render(MoonWorks.Graphics.CommandBuffer commands) => _view.Render(commands);

    /// <summary>After the UI's text editor systems: follows what they did, compiles, and draws the shader's parts.</summary>
    /// <param name="pointerFree">False while the owner has the pointer (dragging its divider): no slider or problem takes it.</param>
    public void Update(in ToolFrame frame, bool pointerFree = true)
    {
        bool pressed = frame.Held && !_wasHeld;
        _wasHeld = frame.Held;
        TextEditorFrame did = _editor.GetComponent<TextEditor>().Frame;
        bool asking = Ask(frame, pressed && pointerFree);
        if (!asking && did.Refused && _tabs[_open].ReadOnly)
            AskToEdit();
        if (_goTo is { } place)
        {
            _goTo = null;
            Open(place);
        }

        if (pointerFree && !asking)
            Point(frame, pressed);

        TextEditor editor = _editor.GetComponent<TextEditor>();
        TextBuffer buffer = editor.Buffer;
        bool onMain = _open == 0;
        _host.Problems = onMain ? _shownProblems : [];

        if (did.SteppedHistory && _popup.Open)
            _popup.Dismiss(buffer);
        if (did.SteppedHistory)
            _host.StopRename();
        if (_host.IsRenaming && _popup.Open)
            _popup.Dismiss(buffer);
        else if (!_host.IsRenaming)
            _popup.Follow(buffer, _completions, edited: did.Edited && !did.SteppedHistory);
        _sinceEdit = did.Edited && onMain ? 0 : _sinceEdit + frame.Delta;
        bool quiet = !_popup.Open && !frame.Held && !did.Edited;
        _hover.Follow(buffer, _completions, _host.FadedAt, quiet ? editor.Layout.Under(buffer, _host, frame.Mouse) : null, frame.Mouse, frame.Delta);

        Finish();
        TextBuffer main = Main.Buffer;
        if (_compiling is null && _compiledVersion != main.Version && _sinceEdit >= Settle)
            Compile(main);

        // The clips follow the shader's text as it is typed, not the last compile.
        if (_clipsVersion != main.Version)
        {
            _clipsVersion = main.Version;
            float height = _timeline.Height;
            _timeline.Use(SlangClips.Find(main.Lines));
            // More clips, more rows: the column under the preview makes room.
            if (_timeline.Height != height)
                Place(_boxes);
            _tuning.UseStandIns(PlayStandIns.Needed(main.Text));
        }

        PressPlayKeys(frame);
        _timeline.Advance(frame.Delta, _view.TakesClock ? null : frame.Time);
        (float seconds, float beats) = _timeline.Clock;
        _view.UseClock(seconds, beats);

        // The pointer counts only over the preview; elsewhere the shader keeps where it last was (the middle at first).
        if (_preview.HasPoint(frame.Mouse))
            _pointer = frame.Mouse;
        _standIns.Update(frame.Delta, _pointer, _preview, _timeline.Progress, seconds, beats, _tuning);
        _view.Update(frame, _pointer, _tuning);

        _chrome.Draw(new EditorState(
            buffer.Line, buffer.Column, _status, _statusColour,
            PreviewStale: _shownProblems.Exists(problem => !problem.Warning) || !_everCompiled,
            PreviewNote: _everCompiled ? "showing the last version that compiled" : "nothing has compiled yet",
            _shownProblems));
        _timeline.Draw(_canvas, _chromeItem);
        _tuning.Draw();
        _hover.Draw();
        _popup.Draw(editor.Layout.CaretRect);
        _strip.Draw(_tabs.Select(tab => new TabLook(tab.Label, tab.Dirty, tab.ReadOnly, tab.Path is not null)).ToList(), _open, frame.Mouse);
    }

    /// <summary>The play keys at their default places, numbered as <see cref="ShaderKeys"/>.</summary>
    private static readonly MoonWorks.Input.KeyCode[] PlayKeys =
    [
        MoonWorks.Input.KeyCode.S, MoonWorks.Input.KeyCode.D, MoonWorks.Input.KeyCode.K, MoonWorks.Input.KeyCode.L,
        MoonWorks.Input.KeyCode.F, MoonWorks.Input.KeyCode.Space, MoonWorks.Input.KeyCode.J,
    ];

    /// <summary>
    /// While the code doesn't have the keys (a press on the preview took them),
    /// the play keys are the shader's: <c>keys.</c> sees their presses.
    /// </summary>
    private void PressPlayKeys(in ToolFrame frame)
    {
        if (frame.Inputs is not { } inputs || _editor.Tags.Has<TextEditorFocused>())
            return;
        for (int key = 0; key < PlayKeys.Length; key++)
        {
            if (inputs.Keyboard.IsPressed(PlayKeys[key]))
            {
                ShaderKeys.Press(key);
                _standIns.Pressed(key);
            }
        }
    }

    /// <summary>The sliders, the tabs (a press opens one, its cross closes it), and a click on a problem jumping the shader's text to it.</summary>
    private void Point(in ToolFrame frame, bool pressed)
    {
        // A held slider takes the pointer from everything else.
        if (_tuning.Point(frame.Mouse, frame.Held, pressed))
            return;

        if (pressed && _strip.TabAt(frame.Mouse) is { } tab)
        {
            if (tab.Close)
                Close(tab.Tab);
            else
                Show(tab.Tab);
            Focus();
        }
        else if (pressed && _chrome.ProblemAt(frame.Mouse) is { } problem)
        {
            Show(0);
            Main.Buffer.Place(problem.Line - 1, problem.Column - 1);
            Focus();
        }
    }

    /// <summary>Compiles the text off the frame, so typing never waits on Slang.</summary>
    private void Compile(TextBuffer buffer)
    {
        string source = buffer.Text;
        int version = buffer.Version;
        Compiling?.Invoke(source);

        _lint = SlangLint.Check(source, _typeNames);
        if (!_everCompiled)
            (_status, _statusColour) = ("compiling…", Palette.TextMuted);
        IEditorPreview view = _view;
        _compiling = Task.Run(() =>
        {
            var clock = Stopwatch.StartNew();
            try
            {
                return (version, source, (PreviewBuild?)view.Compile(source), (string?)null, clock.ElapsedMilliseconds);
            }
            catch (Exception exception)
            {
                return (version, source, null, exception.Message, clock.ElapsedMilliseconds);
            }
        });
    }

    private void Finish()
    {
        if (_compiling is not { IsCompleted: true } done)
            return;

        _compiling = null;
        (int version, string source, PreviewBuild? build, string? error, long ms) = done.Result;
        _compiledVersion = version;
        string said = "";
        if (error is null)
        {
            try
            {
                said = _view.Show(build!);
            }
            catch (Exception exception)
            {
                error = $"{_view.Label}(1): error: the GPU refused it: {exception.Message}";
            }
        }

        if (error is not null)
        {
            _shownProblems = WithLint(SlangProblems.Parse(error, _view.Label, source.Split('\n')));
            int count = _shownProblems.Count;
            (_status, _statusColour) = ($"{count} problem{(count == 1 ? "" : "s")}", Palette.DangerBright);
            return;
        }

        _shownProblems = WithLint([]);
        _tuning.Use(build!.Params);
        _everCompiled = true;
        int warnings = _lint.Count;
        string warned = warnings == 0 ? "" : $" · {warnings} warning{(warnings == 1 ? "" : "s")}";
        (_status, _statusColour) = ($"compiled in {ms} ms{warned}{said}", warnings == 0 ? Palette.Success : Palette.Warning);
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
        TextEditors.Delete(_editor);
        _timeline.Dispose();
        _view.Dispose();
    }
}
