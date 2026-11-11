using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Labs.Editor;
using Xanadu.Storage;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A shader editor inside the lab, the start of the one the game will get: a
/// level shader slot typed on the left, compiled as you type and drawn on the
/// right. Slang's problems mark their lines; calls into the library show their
/// parameter names as inlay hints. The text saves itself to a git-ignored file.
/// What the code compiles into and how it is drawn is the preview's
/// (<see cref="IEditorPreview"/>): a level shader slot, or the spawner.
/// </summary>
internal sealed class ShaderEditor(string name, string workingCopy, string template, string? file,
    Func<LabContext, Handle, IEditorPreview> preview) : ILabExperiment
{
    /// <summary>A level shader slot: <c>shade</c> on a quad.</summary>
    public static ShaderEditor Effects(string? file) => new("SHADER EDITOR",
        "res://Tools/Labs/Playground/editor.slang", "res://Tools/Labs/Playground/editor.template.slang", file,
        (context, item) => new MaterialPreview(context, item));

    /// <summary>The spawner: <c>count</c> things from <c>spawn(i)</c>, culled and drawn indirectly on the GPU.</summary>
    public static ShaderEditor Spawner() => new("SPAWNER",
        "res://Tools/Labs/Playground/spawner.slang", "res://Tools/Labs/Playground/spawner.template.slang", null,
        (context, item) => new SpawnerPreview(context, item));

    public string Name => name;

    /// <summary>The editor lays out the whole body itself; its help sits under the problem list.</summary>
    public bool FullWidth => true;

    public string[] Notes => [];

    public ILabStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(context, parent, area, WorkingFile(), preview);

    /// <summary><c>--editor-file</c>, or the lab's own working copy, made from the template the first time.</summary>
    private string WorkingFile()
    {
        if (file is not null)
            return file;

        string copy = GamePaths.Globalize(workingCopy);
        if (!File.Exists(copy))
            File.Copy(GamePaths.Globalize(template), copy);
        return copy;
    }

    private sealed class Stage : ILabStage
    {
        /// <summary>How long typing has to pause before a compile starts.</summary>
        private const float Settle = 0.3f;

        private const float Gap = 12;
        private const float StatusHeight = 32;

        /// <summary>The right-hand column's width at first: the preview at 16:9 on top, problems and help under it.</summary>
        private const float SideWidth = 720;

        /// <summary>The least the code and the column can be dragged down to; the column also keeps room under the preview.</summary>
        private const float MinCode = 360, MinSide = 280, MinUnderPreview = 260;

        private readonly LabContext _context;
        private readonly EditorChrome _chrome;
        private readonly EditorTuning _tuning;
        private readonly IEditorPreview _view;
        private readonly Rect2 _area;
        private Rect2 _preview;
        private float _sideWidth = SideWidth;

        /// <summary>The divider is being dragged; where the pointer grabbed it from its middle.</summary>
        private bool _dragging;
        private float _grabbedAt;

        /// <summary>The UI text editor the code lives in; the input and drawing are its systems'.</summary>
        private readonly Entity _editor;
        private readonly SlangEditorHost _host;
        private readonly SlangHints _hints;
        private readonly SlangCompletions _completions;
        private readonly CompletionPopup _popup;
        private readonly HoverPopup _hover;
        private readonly string _file;
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

        public Stage(LabContext context, Handle parent, Rect2 area, string workingFile, Func<LabContext, Handle, IEditorPreview> preview)
        {
            _context = context;
            _area = area;
            _sideWidth = ClampSide(SideWidth);
            (Rect2 codeArea, Rect2 bar, _preview, Rect2 side, Rect2 divider) = Arrange(_sideWidth);
            _pointer = _preview.GetCenter();

            // Made first, so everything after draws over the code.
            _file = workingFile;
            _editor = TextEditors.Create(context.Entities, parent, codeArea, File.ReadAllText(_file));
            _view = preview(context, Child(parent));
            _view.Place(_preview);
            Handle chromeItem = Child(parent);
            IRectCanvas canvas = HostPlatform.NewCanvas();
            _chrome = new EditorChrome(canvas, chromeItem, bar, _preview, side);
            _chrome.Help = [.. _view.Help, .. _chrome.Help];
            _tuning = new EditorTuning(canvas, chromeItem, _chrome.TuningArea, _view.Provided.Select(value => value.Name));
            string library = Path.Combine(SlangShaders.LibraryPath ?? "", "xanadu");
            _hints = SlangHints.FromLibrary(library);
            _typeNames = [.. SlangSyntax.BuiltInTypes, .. _hints.Types, .. _hints.Aliases, .. _view.Types];
            IEnumerable<string> sources = Directory.Exists(library) ? Directory.EnumerateFiles(library, "*.slang").Select(File.ReadAllText) : [];
            _completions = SlangCompletions.FromSources(
                _view.Prelude.Length > 0 ? sources.Append(_view.Prelude) : sources,
                Xanadu.Rendering.ShaderConstants.Library,
                _view.Provided);
            // Made last, so the boxes draw over the code and everything round it; the list over a hover box.
            _hover = new HoverPopup(canvas, Child(parent), codeArea);
            _popup = new CompletionPopup(canvas, Child(parent), codeArea);
            _host = new SlangEditorHost(_hints, _popup);
            _editor.GetComponent<TextEditor>().Host = _host;
            _chrome.Divider = divider;
        }

        /// <summary>
        /// Where everything goes for a column this wide: the code and its status bar on the left, the
        /// divider in the gap, the preview (16:9, as the game's playfield is, so a shader looks here as
        /// it will there) and the column under it on the right.
        /// </summary>
        private (Rect2 Code, Rect2 Bar, Rect2 Preview, Rect2 Side, Rect2 Divider) Arrange(float sideWidth)
        {
            float codeWidth = _area.Size.X - sideWidth - Gap;
            var code = new Rect2(_area.Position, new Vector2(codeWidth, _area.Size.Y - StatusHeight));
            var bar = new Rect2(_area.Position.X, code.End.Y, codeWidth, StatusHeight);
            float sideLeft = code.End.X + Gap;
            var preview = new Rect2(sideLeft, _area.Position.Y, sideWidth, MathF.Round(sideWidth * 9f / 16f));
            var side = new Rect2(sideLeft, preview.End.Y + Gap, sideWidth, _area.End.Y - preview.End.Y - Gap);
            var divider = new Rect2(code.End.X, _area.Position.Y, Gap, _area.Size.Y);
            return (code, bar, preview, side, divider);
        }

        /// <summary>A column width that leaves the code, the column and the room under the preview their least.</summary>
        private float ClampSide(float width)
        {
            float most = MathF.Min(_area.Size.X - Gap - MinCode, (_area.Size.Y - Gap - MinUnderPreview) * 16f / 9f);
            return MathF.Round(Math.Clamp(width, MinSide, MathF.Max(MinSide, most)));
        }

        /// <summary>Moves everything to a new column width: the code, the preview quad (so the shader sees its new size), the column and the popups' bounds.</summary>
        private void Resize(float sideWidth)
        {
            sideWidth = ClampSide(sideWidth);
            if (sideWidth == _sideWidth)
                return;
            _sideWidth = sideWidth;
            (Rect2 code, Rect2 bar, Rect2 preview, Rect2 side, Rect2 divider) = Arrange(sideWidth);

            ref TextEditor editor = ref _editor.GetComponent<TextEditor>();
            editor.Layout.Area = code;
            CanvasServer.Current.SetCustomRect(editor.Item, true, code);
            _hover.Bounds = code;
            _popup.Bounds = code;

            // The pointer keeps its place on the preview as a fraction of it.
            Vector2 at = (_pointer - _preview.Position) / _preview.Size;
            _preview = preview;
            _pointer = _preview.Position + at * _preview.Size;
            _view.Place(_preview);

            _chrome.Resize(bar, preview, side);
            _chrome.Divider = divider;
            _tuning.Area = _chrome.TuningArea;
        }

        private static Handle Child(Handle parent)
        {
            Handle item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(item, parent);
            return item;
        }

        public void Render(MoonWorks.Graphics.CommandBuffer commands) => _view.Render(commands);

        /// <summary>After the UI's text editor systems: follows what they did, compiles, and draws the shader's parts.</summary>
        public void Update(in LabFrame frame)
        {
            TextEditor editor = _editor.GetComponent<TextEditor>();
            TextBuffer buffer = editor.Buffer;
            TextEditorFrame did = editor.Frame;
            Point(frame, buffer);

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
            _hover.Follow(buffer, _completions, quiet ? editor.Layout.Under(buffer, _host, frame.Mouse) : null, frame.Mouse, frame.Delta);

            Finish();
            if (_compiling is null && _compiledVersion != buffer.Version && _sinceEdit >= Settle)
                Compile(buffer);

            // The pointer counts only over the preview; elsewhere the shader keeps where it last was (the middle at first).
            if (_preview.HasPoint(frame.Mouse))
                _pointer = frame.Mouse;
            _view.Update(frame, _pointer, _tuning);

            _chrome.Draw(new EditorState(
                buffer.Line, buffer.Column, _status, _statusColour,
                PreviewStale: _shownProblems.Exists(problem => !problem.Warning) || !_everCompiled,
                PreviewNote: _everCompiled ? "showing the last version that compiled" : "nothing has compiled yet",
                _shownProblems));
            _tuning.Draw();
            _hover.Draw();
            _popup.Draw(editor.Layout.CaretRect);
        }

        /// <summary>The sliders, and a click on a problem in the list jumping the editor to it.</summary>
        private void Point(in LabFrame frame, TextBuffer buffer)
        {
            // A held slider takes the pointer from everything else.
            bool pressed = frame.Held && !_wasHeld;
            _wasHeld = frame.Held;

            // The divider: a press on it drags the column's edge until let go.
            if (pressed && _chrome.Divider.HasPoint(frame.Mouse))
            {
                _dragging = true;
                _grabbedAt = frame.Mouse.X - _chrome.Divider.GetCenter().X;
            }

            if (_dragging)
            {
                Resize(_area.End.X - (frame.Mouse.X - _grabbedAt) - Gap / 2);
                if (!frame.Held)
                {
                    _dragging = false;
                    // Pressing outside the code took its focus; dragging shouldn't cost the typing.
                    _editor.AddTag<TextEditorFocused>();
                }
            }

            _chrome.DividerLit = _dragging || _chrome.Divider.HasPoint(frame.Mouse);
            if (_dragging)
                return;

            if (_tuning.Point(frame.Mouse, frame.Held, pressed))
                return;

            if (pressed && _chrome.ProblemAt(frame.Mouse) is { } problem)
            {
                buffer.Place(problem.Line - 1, problem.Column - 1);
                _editor.AddTag<TextEditorFocused>();
            }
        }

        /// <summary>Saves the text and compiles it off the frame, so typing never waits on Slang.</summary>
        private void Compile(TextBuffer buffer)
        {
            string source = buffer.Text;
            int version = buffer.Version;
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
            _view.Dispose();
        }
    }
}
