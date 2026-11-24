using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Friflo.Engine.ECS;
using Xanadu.Core.Rendering.Rhythm;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The owner's laid-out slots the editor lives in: the code's column (the tab strip and the
/// code go in it), the bar under it, the preview, the column under the preview (the clips,
/// problems, values and key guide go in it) and the divider between.
/// </summary>
internal readonly record struct ShaderEditorSlots(UiNode Code, UiNode Bar, UiNode Preview, UiNode Side, UiNode Divider);

/// <summary>
/// Slang code being edited with its preview: the text (a UI text editor), its
/// problems (Slang's and <see cref="SlangLint"/>'s, marking their lines),
/// completion, hovers, inlay hints, and a compile off the frame whenever typing
/// pauses, shown by an <see cref="IEditorPreview"/>. It lives in its owner's
/// laid-out slots (<see cref="ShaderEditorSlots"/>): its UI parts are nodes in
/// them, moving with the layout in the same frame, and what it draws itself
/// follows their boxes each <see cref="Update"/>.
/// Shared by the shader tool and the lab's editor tabs.
/// <para>
/// A place written as <c>float2(x, y)</c> under the caret, and every <c>uniform float2</c>
/// that reads as one, is a dot on the preview to drag (<see cref="PreviewPoints"/>).
/// </para>
/// <para>
/// Going to a library name's declaration (F12, Ctrl+click) opens its module in
/// a tab of its own over the code. A library tab is read-only until the first
/// try at an edit is answered yes; Ctrl+S there writes the module and compiles
/// the shader again. The shader's own tab is always first: its text is what
/// compiles, owns the problems and the clips.
/// </para>
/// <para>
/// It edits one text (the lab's slots) or a shader stack's bricks (the shader tool):
/// then the pipeline strip (<see cref="BrickStrip"/>) over the code picks the brick
/// in the first tab, adds and removes them, and the editor composes them
/// (<see cref="ShaderStack.Compose"/>) for each compile, every problem landing on its
/// own brick.
/// </para>
/// </summary>
internal sealed partial class ShaderCodeEditor : IDisposable
{
    /// <summary>How long typing has to pause before a compile starts.</summary>
    private const float Settle = 0.3f;

    private readonly EditorChrome _chrome;
    private readonly EditorTuning _tuning;
    private readonly IEditorPreview _view;
    private readonly ClipTimeline _timeline;

    private readonly ShaderEditorSlots _slots;

    /// <summary>The nodes it made in its slots: the code, and under the preview the clips, the problems, the values' room, the key guide.</summary>
    private readonly StackGraphView _graph;
    private readonly UiNode _code, _rest, _clipsNode, _problemsNode, _tuningNode, _guideNode;
    private int _guideLines = -1;
    private readonly PlayStandIns _standIns = new();
    private readonly PreviewPoints _points = new();
    private readonly IRectCanvas _canvas;
    private readonly Handle _chromeItem;
    private int _clipsVersion = -1;
    private Rect2 _preview;

    /// <summary>The UI text editor the code lives in; the input and drawing are its systems'.</summary>
    private readonly Entity _editor;
    private readonly SlangEditorHost _host;
    private SlangCompletions _completions;
    private SlangLibraryDeclarations _declarations;

    /// <summary>One open text: the shader's own or one of its bricks (no path), or a library module's.</summary>
    private sealed class Tab(string label, string? path, TextBuffer buffer, TextHistory history, string disk)
    {
        public readonly string Label = label;

        /// <summary>The brick it is; null for a single text or a library module.</summary>
        public BrickKind? Kind;

        /// <summary>The last compile's problems and colour conversions on this text's own lines.</summary>
        public List<SlangProblem> Problems = [];
        public List<(ColourConversion Place, string Text)> Conversions = [];
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

    /// <summary>The open texts: the shader's own (the open brick) first, then library modules.</summary>
    private readonly List<Tab> _tabs = [];
    private int _open;
    private readonly EditorTabs _strip;
    private LibraryPlace? _goTo;

    /// <summary>The shader's text in the first tab: the one text, or the brick open.</summary>
    private Tab Main => _tabs[0];
    private readonly CompletionPopup _popup;
    private readonly HoverPopup _hover;

    /// <summary>Every problem as the list under the preview shows it, and the text each is on (null: none of them).</summary>
    private List<SlangProblem> _shownProblems = [];
    private List<Tab?> _listed = [];

    /// <summary>The last compile's problems that aren't on any of the shader's lines.</summary>
    private List<SlangProblem> _global = [];

    /// <summary>Every type a local can be declared with, for the lint: Slang's and the library's.</summary>
    private readonly HashSet<string> _typeNames;
    /// <summary>Each shape's cost a pixel, from the library's @cost tags, for the cost warnings.</summary>
    private readonly Dictionary<string, float> _shapeCosts;
    private int _compiledVersion = -1;
    private float _sinceEdit;
    private bool _wasHeld;
    private bool _everCompiled;
    private Vector2 _pointer;
    private string _status = "compiling…";
    private Color _statusColour = Palette.TextMuted;
    private Task<Compiled>? _compiling;

    /// <summary>What a compile off the frame hands back.</summary>
    private sealed record Compiled(
        int Version, string Source, List<Placed> Places, PreviewBuild? Build, string? Error, long Ms, double SlangMs, List<SlangProblem> Lint);

    /// <summary>Edits one text: its problems carry the preview's label.</summary>
    /// <param name="preview">Makes the preview, drawing under the item it is given.</param>
    public ShaderCodeEditor(EntityStore entities, Handle parent, ShaderEditorSlots slots, string text, Func<Handle, IEditorPreview> preview)
        : this(entities, parent, slots, [new Brick(BrickKind.Shade, text)], null, preview)
    {
    }

    /// <summary>Edits a shader stack's bricks; <paramref name="stack"/> names it, as <see cref="ShaderStack.Compose"/> labels them.</summary>
    public ShaderCodeEditor(EntityStore entities, Handle parent, ShaderEditorSlots slots, string stack, IReadOnlyList<Brick> bricks,
        Func<Handle, IEditorPreview> preview)
        : this(entities, parent, slots, bricks, stack, preview)
    {
    }

    private ShaderCodeEditor(EntityStore entities, Handle parent, ShaderEditorSlots slots, IReadOnlyList<Brick> bricks, string? stack,
        Func<Handle, IEditorPreview> preview)
    {
        _stack = stack;
        if (bricks.Count == 0)
            bricks = [new Brick(BrickKind.Shade, ShaderStack.Template(BrickKind.Shade))];
        Brick first = FirstOpen(bricks);
        _slots = slots;
        _preview = UiLayout.Box(slots.Preview.Entity);
        _pointer = _preview.GetCenter();
        // The text as opened is usually in the shader cache; the first edit isn't, and shouldn't wait for Slang to start.
        SlangShaders.Prepare();

        // The code's column: the pipeline (a stack's), the tab strip, then the code taking the rest.
        slots.Code.Cross(StackCrossAlignment.Stretch);
        if (stack is not null)
            _brickStrip = new BrickStrip(slots.Code);
        _strip = new EditorTabs(entities, slots.Code);
        _code = slots.Code.Column().Grow();
        _graph = new StackGraphView(slots.Code);
        // Under the preview: the clips, then the rest on the column's own background.
        slots.Side.Gap(UnderGap).Cross(StackCrossAlignment.Stretch);
        _clipsNode = slots.Side.Column().Height(ClipTimeline.MinHeight);
        _rest = slots.Side.Column().Grow().Cross(StackCrossAlignment.Stretch).Fill(EditorChrome.ColumnColour);
        _problemsNode = _rest.Column().Height(EditorChrome.ProblemsHeight);
        _tuningNode = _rest.Column().Grow().Pad(TuningPad).Cross(StackCrossAlignment.Stretch);
        _guideNode = _rest.Column();

        // Made first, so everything after draws over the code.
        _editor = TextEditors.Create(_code, parent, first.Text);
        _view = preview(Child(parent));
        _view.Place(_preview);
        Handle chromeItem = Child(parent);
        IRectCanvas canvas = HostPlatform.NewCanvas();
        _canvas = canvas;
        _chromeItem = chromeItem;
        _timeline = new ClipTimeline(entities) { Scrubs = _view.TakesClock };
        _timeline.Jump += (line, column) =>
        {
            if (Unjoin(_clipPlaces, line + 1) is not ({ } tab, int own))
                return;
            OpenBrick(tab);
            Main.Buffer.Place(own - 1, column);
            Focus();
        };
        _timeline.Released += Focus;
        _chrome = new EditorChrome(canvas, chromeItem);
        _chrome.Help = [.. _view.Help, .. _chrome.Help];
        _tuning = new EditorTuning(_tuningNode, _view.Provided.Select(value => value.Name).Append(SlangShaders.ClockOverride));
        // Every library folder: the API's (xanadu/) and the game's (rhythm/).
        string library = SlangShaders.LibraryPath ?? "";
        SlangHints hints = SlangHints.FromLibrary(library);
        _typeNames = [.. SlangSyntax.BuiltInTypes, .. hints.Types, .. hints.Aliases, .. _view.Types];
        _shapeCosts = hints.Costs;
        (_completions, _declarations) = ReadLibrary();
        // Made last, so the boxes draw over the code and everything round it; the list over a hover box, the tabs' question over all.
        Rect2 code = UiLayout.Box(_code.Entity);
        _hover = new HoverPopup(canvas, Child(parent), code);
        _popup = new CompletionPopup(canvas, Child(parent), code);
        _host = new SlangEditorHost(hints, SlangColours.FromLibrary(library), _popup) {
            Save = Save,
            GoElsewhere = GoElsewhere,
            EntryColours = ShaderStack.Kinds.SelectMany(kind => ShaderStack.Entries(kind).Select(name => (name, kind)))
                .ToDictionary(entry => entry.name, entry => BrickStrip.Colour(entry.kind)),
        };
        ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
        editor.Host = _host;
        MakeBricks(bricks, first, editor.Buffer, editor.History);
    }

    /// <summary>The library's completions and where its names are declared, from the modules as they are on disk.</summary>
    private (SlangCompletions, SlangLibraryDeclarations) ReadLibrary()
    {
        List<(string Path, string Text)> modules = SlangShaders.LibraryModules();
        string root = Path.TrimEndingDirectorySeparator(SlangShaders.LibraryPath ?? "");
        // Completions read the modules in the library's folders (xanadu/, rhythm/), as imported; the umbrella modules only re-export them.
        IEnumerable<string> sources = modules.Where(m => Path.GetDirectoryName(m.Path) != root).Select(m => m.Text);
        var completions = SlangCompletions.FromSources(
            _view.Prelude.Length > 0 ? sources.Append(_view.Prelude) : sources,
            ShaderConstants.Library,
            _view.Provided);
        return (completions, SlangLibraryDeclarations.FromModules(modules));
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
        if (index == _open)
            return;
        Leave();
        _open = index;
        Load(_tabs[index]);
    }

    /// <summary>Keeps where the open tab was scrolled to, as the editor leaves it.</summary>
    private void Leave()
    {
        ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
        _popup.Dismiss(editor.Buffer);
        _tabs[_open].Scroll = editor.Layout.Scroll;
    }

    /// <summary>Puts a text in the editor.</summary>
    private void Load(Tab tab)
    {
        ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
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

    /// <summary>The one text as it is now, whichever tab is open (a stack's first brick; see <see cref="Bricks"/>).</summary>
    public string Text => _bricks[0].Buffer.Text;

    /// <summary>A stack's bricks as they are now, in pipeline order.</summary>
    public List<Brick> Bricks => [.. _bricks.Select(brick => new Brick(brick.Kind ?? BrickKind.Shade, brick.Buffer.Text))];

    /// <summary>Changes with every edit of the shader's text, and every brick added or removed.</summary>
    public int Version
    {
        get
        {
            int version = _structure;
            foreach (Tab brick in _bricks)
                version = unchecked(version * 31 + brick.Buffer.Version);
            return version;
        }
    }

    /// <summary>Ctrl+S in the code. Without a handler the key does nothing.</summary>
    public event Action? SaveRequested;

    /// <summary>
    /// Whether the stack's render graph stands where the code is: its passes, the data
    /// they share and who reads and writes what, as of the last compile that worked.
    /// </summary>
    public bool ShowsGraph
    {
        get => _showsGraph;
        set
        {
            _showsGraph = value;
            _code.Hidden(value);
            _graph.Node.Hidden(!value);
            CanvasServer.Current.SetVisible(_editor.GetComponent<TextEditor>().Item, !value);
        }
    }

    private bool _showsGraph;

    /// <summary>The last stack that compiled: its graph is read again each frame, with the values as they are tuned now.</summary>
    private CompiledShader? _compiledStack;

    /// <summary>Where the editor says what a brick still needs; nothing is said without one.</summary>
    public ToastSystem? Toasts { get; set; }

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

    /// <summary>Space between the clips and the column under them, and round the values.</summary>
    private const float UnderGap = 8, TuningPad = 12;

    /// <summary>
    /// What it draws itself follows its nodes' boxes as this frame laid them out: the
    /// preview quad (so the shader sees its new size), the chrome, the clips, the popups'
    /// bounds and the question over the code. The key guide gives way, a line at a time
    /// from its end, so the values keep room.
    /// </summary>
    private void Follow()
    {
        Rect2 code = UiLayout.Box(_code.Entity), preview = UiLayout.Box(_slots.Preview.Entity);
        _hover.Bounds = code;
        _popup.Bounds = code;
        _strip.Code = code;
        if (preview != _preview && preview.Size.X > 0 && _preview.Size.X > 0)
        {
            // The pointer keeps its place on the preview as a fraction of it.
            Vector2 at = (_pointer - _preview.Position) / _preview.Size;
            _pointer = preview.Position + at * preview.Size;
        }

        if (preview != _preview)
        {
            _preview = preview;
            _view.Place(preview);
        }

        _timeline.Area = UiLayout.Box(_clipsNode.Entity);
        _chrome.Place(UiLayout.Box(_slots.Bar.Entity), preview, UiLayout.Box(_problemsNode.Entity), UiLayout.Box(_guideNode.Entity));
        _chrome.Divider = UiLayout.Box(_slots.Divider.Entity);
        _tuning.Follow();

        float room = UiLayout.Box(_rest.Entity).Size.Y - EditorChrome.ProblemsHeight - TuningPad * 2
                     - EditorChrome.MinTuningRows * EditorChrome.RowHeight;
        int lines = _chrome.HelpFitting(room);
        if (lines != _guideLines)
        {
            _guideLines = lines;
            _guideNode.Height(EditorChrome.HelpHeight(lines));
        }
    }

    public void Render(MoonWorks.Graphics.CommandBuffer commands) => _view.Render(commands);

    /// <summary>After the UI's text editor systems: follows what they did, compiles, and draws the shader's parts.</summary>
    /// <param name="pointerFree">False while the owner has the pointer (dragging its divider): no slider or problem takes it.</param>
    public void Update(in ToolFrame frame, bool pointerFree = true)
    {
        Follow();
        if (_showsGraph && _compiledStack is { } stack)
            _graph.Show(StackGraph.Of(stack, name => stack.ScheduleValue(name, _tuning.Value(name))));
        _graph.Update();
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

        _points.Follow(_open == 0 ? Main.Buffer : null, _completions.ParameterNames);
        if (pointerFree && !asking)
            Point(frame, pressed);

        TextEditor editor = _editor.GetComponent<TextEditor>();
        TextBuffer buffer = editor.Buffer;
        bool onMain = _open == 0;
        _host.Problems = onMain ? Main.Problems : [];
        _host.ColourConversions = onMain ? Main.Conversions : [];

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
        int version = Version;
        if (_compiling is null && _compiledVersion != version && _sinceEdit >= Settle)
            Compile();

        // The clips follow the shader's text as it is typed, not the last compile.
        if (_clipsVersion != version)
        {
            _clipsVersion = version;
            (string joined, _clipPlaces) = Joined();
            float height = _timeline.Height;
            _timeline.Use(SlangClips.Find(joined.Split('\n')));
            // More clips, more rows: the column under the preview makes room.
            if (_timeline.Height != height)
                _clipsNode.Height(_timeline.Height);
            _tuning.UseStandIns(PlayStandIns.Needed(joined));
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
        _points.Draw(_canvas, _chromeItem, _preview, _tuning, frame.Mouse);
        _hover.Draw();
        _popup.Draw(editor.Layout.CaretRect);
        _strip.Draw(_tabs.Select(tab => new TabLook(tab.Label, tab.Dirty, tab.ReadOnly, tab.Path is not null)).ToList(), _open, frame.Mouse);
        DrawBricks();
    }

    /// <summary>The play keys at their default places, numbered as <see cref="ShaderKeys"/>.</summary>
    private static readonly MoonWorks.Input.KeyCode[] PlayKeys =
    [
        MoonWorks.Input.KeyCode.F, MoonWorks.Input.KeyCode.J, MoonWorks.Input.KeyCode.K, MoonWorks.Input.KeyCode.L,
        MoonWorks.Input.KeyCode.S, MoonWorks.Input.KeyCode.Space, MoonWorks.Input.KeyCode.D,
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
        // A held slider or point takes the pointer from everything else.
        if (_tuning.Point(frame.Mouse, frame.Held, pressed))
            return;
        if (_points.Point(Main.Buffer, Main.History, _completions.ParameterNames, _preview, _tuning, frame.Mouse, frame.Held, pressed, Focus))
            return;

        if (pressed && PressBricks(frame.Mouse))
            Focus();
        else if (pressed && _strip.TabAt(frame.Mouse) is { } tab)
        {
            if (tab.Close)
                Close(tab.Tab);
            else
                Show(tab.Tab);
            Focus();
        }
        else if (pressed && _chrome.ProblemAt(frame.Mouse) is { } problem && _shownProblems.IndexOf(problem) is >= 0 and int index
                 && _listed[index] is { } on)
        {
            OpenBrick(on);
            Main.Buffer.Place(problem.Line - 1, problem.Column - 1);
            Focus();
        }
    }

    public void Dispose()
    {
        TextEditors.Delete(_editor);
        _tuning.Dispose();
        _strip.Dispose();
        _brickStrip?.Dispose();
        _timeline.Dispose();
        _view.Dispose();
        UiLayout.DeleteSubtree(_code.Entity);
        _graph.Delete();
        UiLayout.DeleteSubtree(_clipsNode.Entity);
        UiLayout.DeleteSubtree(_rest.Entity);
    }
}
