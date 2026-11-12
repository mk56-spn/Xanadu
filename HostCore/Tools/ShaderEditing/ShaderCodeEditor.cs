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
/// </summary>
internal sealed class ShaderCodeEditor : IDisposable
{
    /// <summary>How long typing has to pause before a compile starts.</summary>
    private const float Settle = 0.3f;

    private readonly EditorChrome _chrome;
    private readonly EditorTuning _tuning;
    private readonly IEditorPreview _view;
    private readonly ClipTimeline _timeline;
    private readonly PlayStandIns _standIns = new();
    private readonly IRectCanvas _canvas;
    private readonly Handle _chromeItem;
    private int _clipsVersion = -1;
    private Rect2 _preview;

    /// <summary>The UI text editor the code lives in; the input and drawing are its systems'.</summary>
    private readonly Entity _editor;
    private readonly SlangEditorHost _host;
    private readonly SlangCompletions _completions;
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

        // Made first, so everything after draws over the code.
        _editor = TextEditors.Create(entities, parent, boxes.Code, text);
        _view = preview(Child(parent));
        _view.Place(_preview);
        Handle chromeItem = Child(parent);
        IRectCanvas canvas = HostPlatform.NewCanvas();
        _canvas = canvas;
        _chromeItem = chromeItem;
        (Rect2 clipsBox, Rect2 side) = UnderPreview(boxes.Side);
        _timeline = new ClipTimeline(entities) { Area = clipsBox, Scrubs = _view.TakesClock };
        _timeline.Jump += (line, column) =>
        {
            _editor.GetComponent<TextEditor>().Buffer.Place(line, column);
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
        IEnumerable<string> sources = Directory.Exists(library) ? Directory.EnumerateFiles(library, "*.slang").Select(File.ReadAllText) : [];
        _completions = SlangCompletions.FromSources(
            _view.Prelude.Length > 0 ? sources.Append(_view.Prelude) : sources,
            ShaderConstants.Library,
            _view.Provided);
        // Made last, so the boxes draw over the code and everything round it; the list over a hover box.
        _hover = new HoverPopup(canvas, Child(parent), boxes.Code);
        _popup = new CompletionPopup(canvas, Child(parent), boxes.Code);
        _host = new SlangEditorHost(hints, _popup) { Save = () => SaveRequested?.Invoke() };
        _editor.GetComponent<TextEditor>().Host = _host;
    }

    /// <summary>The text as it is now.</summary>
    public string Text => _editor.GetComponent<TextEditor>().Buffer.Text;

    /// <summary>Goes up with every edit.</summary>
    public int Version => _editor.GetComponent<TextEditor>().Buffer.Version;

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
    private static (Rect2 Clips, Rect2 Others) UnderPreview(Rect2 side)
    {
        const float Gap = 8;
        var clips = new Rect2(side.Position, new Vector2(side.Size.X, ClipTimeline.Height));
        var rest = new Rect2(side.Position.X, clips.End.Y + Gap, side.Size.X, Math.Max(0, side.Size.Y - ClipTimeline.Height - Gap));
        return (clips, rest);
    }

    /// <summary>Moves everything to new boxes: the code, the preview quad (so the shader sees its new size), the column and the popups' bounds.</summary>
    public void Place(ShaderEditorBoxes boxes)
    {
        ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
        editor.Layout.Area = boxes.Code;
        CanvasServer.Current.SetCustomRect(editor.Item, true, boxes.Code);
        _hover.Bounds = boxes.Code;
        _popup.Bounds = boxes.Code;

        // The pointer keeps its place on the preview as a fraction of it.
        Vector2 at = (_pointer - _preview.Position) / _preview.Size;
        _preview = boxes.Preview;
        _pointer = _preview.Position + at * _preview.Size;
        _view.Place(_preview);

        (Rect2 clipsBox, Rect2 side) = UnderPreview(boxes.Side);
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
        TextEditor editor = _editor.GetComponent<TextEditor>();
        TextBuffer buffer = editor.Buffer;
        TextEditorFrame did = editor.Frame;
        bool pressed = frame.Held && !_wasHeld;
        _wasHeld = frame.Held;
        if (pointerFree)
            Point(frame, buffer, pressed);

        if (did.SteppedHistory && _popup.Open)
            _popup.Dismiss(buffer);
        if (did.SteppedHistory)
            _host.StopRename();
        if (_host.IsRenaming && _popup.Open)
            _popup.Dismiss(buffer);
        else if (!_host.IsRenaming)
            _popup.Follow(buffer, _completions, edited: did.Edited && !did.SteppedHistory);
        _sinceEdit = did.Edited ? 0 : _sinceEdit + frame.Delta;
        bool quiet = !_popup.Open && !frame.Held && !did.Edited;
        _hover.Follow(buffer, _completions, _host.FadedAt, quiet ? editor.Layout.Under(buffer, _host, frame.Mouse) : null, frame.Mouse, frame.Delta);

        Finish();
        if (_compiling is null && _compiledVersion != buffer.Version && _sinceEdit >= Settle)
            Compile(buffer);

        // The clips follow the text as it is typed, not the last compile.
        if (_clipsVersion != buffer.Version)
        {
            _clipsVersion = buffer.Version;
            _timeline.Use(SlangClips.Find(buffer.Lines));
            _tuning.UseStandIns(PlayStandIns.Needed(buffer.Text));
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

    /// <summary>The sliders, and a click on a problem in the list jumping the editor to it.</summary>
    private void Point(in ToolFrame frame, TextBuffer buffer, bool pressed)
    {
        // A held slider takes the pointer from everything else.
        if (_tuning.Point(frame.Mouse, frame.Held, pressed))
            return;

        if (pressed && _chrome.ProblemAt(frame.Mouse) is { } problem)
        {
            buffer.Place(problem.Line - 1, problem.Column - 1);
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
            _host.Problems = _shownProblems = WithLint(SlangProblems.Parse(error, _view.Label, source.Split('\n')));
            int count = _shownProblems.Count;
            (_status, _statusColour) = ($"{count} problem{(count == 1 ? "" : "s")}", Palette.DangerBright);
            return;
        }

        _host.Problems = _shownProblems = WithLint([]);
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
