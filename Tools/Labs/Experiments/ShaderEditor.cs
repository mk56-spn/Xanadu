using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    private const string Template = "res://Tools/Labs/Playground/playground.template.slang";
    private const string Label = "editor";
    private const int ProblemLines = 14;
    private const float ProblemWidth = 590;

    private Entity _status;
    private readonly Entity[] _problems = new Entity[ProblemLines];

    public string Name => "SHADER EDITOR";

    public string[] Notes =>
    [
        "Type on the left; it compiles as you go and draws on",
        "the right. A level shader slot: uniforms and",
        "float4 shade(ItemInput i); import xanadu.",
        "Faded names in calls are hints, not code.",
        "Arrows, Home/End, PgUp/PgDn, Tab, Ctrl+V, wheel.",
        .. LabShaderValues.Notes,
    ];

    public void BuildPanel(UiNode panel)
    {
        _status = panel.Text("waiting for the first compile…", UiType.Small, Palette.TextMuted);
        UiNode column = panel.Column().Cross(StackCrossAlignment.Start).Width(ProblemWidth);
        for (int i = 0; i < ProblemLines; i++)
            _problems[i] = column.Text("", UiType.Caption, Palette.DangerBright, TextAlign.Left).Mono().Width(ProblemWidth);
    }

    public ILabStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    private void Show(string status, Color colour, IReadOnlyList<SlangProblem> problems)
    {
        UiText.Set(_status, status);
        UiText.SetColor(_status, colour);
        for (int i = 0; i < ProblemLines; i++)
        {
            string line = "";
            if (i < problems.Count)
            {
                SlangProblem problem = problems[i];
                line = (problem.Line > 0 ? $"{problem.Line,4}: " : "      ") + problem.Message;
                if (line.Length > 78)
                    line = line[..77] + "…";
            }

            UiText.Set(_problems[i], line);
        }
    }

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

        private static readonly KeyCode[] Repeating =
        [
            KeyCode.Left, KeyCode.Right, KeyCode.Up, KeyCode.Down, KeyCode.Backspace, KeyCode.Delete,
            KeyCode.Return, KeyCode.PageUp, KeyCode.PageDown, KeyCode.Tab,
        ];

        private readonly ShaderEditor _owner;
        private readonly LabContext _context;
        private readonly Handle _codeItem;
        private readonly Handle _previewItem;
        private readonly Rect2 _preview;
        private readonly CodeView _view;
        private readonly CodeBuffer _buffer = new();
        private readonly SlangHints _hints;
        private readonly string _file;
        private readonly Dictionary<KeyCode, float> _held = new();
        private List<SlangProblem> _shownProblems = [];
        private CanvasMaterial? _material;
        private int[] _wanted = [];
        private int _compiledVersion = -1;
        private float _sinceEdit;
        private float _sinceCaretMove;
        private bool _wasHeld;
        private Task<(int Version, string Source, string[] Params, string? Error, long Ms)>? _compiling;

        public Stage(ShaderEditor owner, LabContext context, Handle parent, Rect2 area)
        {
            _owner = owner;
            _context = context;
            float split = MathF.Round(area.Size.X * 0.64f);
            var codeArea = new Rect2(area.Position, new Vector2(split, area.Size.Y));
            _preview = new Rect2(area.Position.X + split + 8, area.Position.Y, area.Size.X - split - 8, area.Size.Y);

            _codeItem = Child(parent);
            _previewItem = Child(parent);
            CanvasServer.Current.AddRect(_previewItem, _preview, Colors.White);
            _view = new CodeView(HostPlatform.NewCanvas(), _codeItem, codeArea, context.Text);
            _hints = SlangHints.FromLibrary(Path.Combine(SlangShaders.LibraryPath ?? "", "xanadu"));

            _file = WorkingFile();
            _buffer.Load(File.ReadAllText(_file));
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
            if (frame.Inputs is { } inputs)
                Edit(inputs, frame);
            Point(frame);

            if (_buffer.Version != version)
                _sinceEdit = 0;
            else
                _sinceEdit += frame.Delta;
            if (_buffer.Version != version || caret != (_buffer.Line, _buffer.Column))
            {
                _sinceCaretMove = 0;
                _view.Reveal(_buffer.Line);
            }
            else
                _sinceCaretMove += frame.Delta;

            Finish();
            if (_compiling is null && _compiledVersion != _buffer.Version && _sinceEdit >= Settle)
                Compile();

            if (_material is not null)
                LabShaderValues.Apply(_material, _wanted, _preview, frame.Mouse, frame.Time);

            // Solid while typing or moving, blinking once still.
            bool caretOn = _sinceCaretMove < 0.5f || (int)(_sinceCaretMove * 2) % 2 == 0;
            _view.Draw(_buffer, _hints, _shownProblems, caretOn);
        }

        private void Edit(Inputs inputs, in LabFrame frame)
        {
            Keyboard keys = inputs.Keyboard;
            bool ctrl = keys.IsDown(KeyCode.LeftControl) || keys.IsDown(KeyCode.RightControl);

            if (frame.Typed.Length > 0 && !ctrl)
                _buffer.Insert(frame.Typed);
            if (ctrl && keys.IsPressed(KeyCode.V) && SDL3.SDL.SDL_GetClipboardText() is { Length: > 0 } clip)
                _buffer.Insert(clip);

            foreach (KeyCode key in Repeating)
            {
                if (Fires(keys, key, frame.Delta))
                    Press(key);
            }

            if (keys.IsPressed(KeyCode.Home))
                _buffer.Home();
            if (keys.IsPressed(KeyCode.End))
                _buffer.End();
        }

        private void Press(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Left: _buffer.MoveColumn(-1); break;
                case KeyCode.Right: _buffer.MoveColumn(1); break;
                case KeyCode.Up: _buffer.MoveLine(-1); break;
                case KeyCode.Down: _buffer.MoveLine(1); break;
                case KeyCode.PageUp: _buffer.MoveLine(-_view.VisibleLines); break;
                case KeyCode.PageDown: _buffer.MoveLine(_view.VisibleLines); break;
                case KeyCode.Backspace: _buffer.Backspace(); break;
                case KeyCode.Delete: _buffer.Delete(); break;
                case KeyCode.Return: _buffer.NewLine(); break;
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
            if (frame.Held && !_wasHeld && _view.Hit(_buffer, _hints, frame.Mouse) is { } hit)
                _buffer.Place(hit.Line, hit.Column);
            _wasHeld = frame.Held;

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

            _owner.Show("compiling…", Palette.TextMuted, _shownProblems);
            _compiling = Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                try
                {
                    CompiledShader compiled = SlangShaders.CompileEffectBody(Label, source);
                    return (version, source, Array.ConvertAll(compiled.Params, param => param.Name), (string?)null, clock.ElapsedMilliseconds);
                }
                catch (Exception exception)
                {
                    return (version, source, Array.Empty<string>(), exception.Message, clock.ElapsedMilliseconds);
                }
            });
        }

        private void Finish()
        {
            if (_compiling is not { IsCompleted: true } done)
                return;

            _compiling = null;
            (int version, string source, string[] declared, string? error, long ms) = done.Result;
            _compiledVersion = version;
            if (error is not null)
            {
                _shownProblems = SlangProblems.Parse(error, Label);
                int count = _shownProblems.Count;
                _owner.Show(
                    $"{count} problem{(count == 1 ? "" : "s")} · the preview shows the last version that compiled",
                    Palette.Danger, _shownProblems);
                return;
            }

            _shownProblems = [];
            CanvasMaterial? previous = _material;
            _material = CanvasMaterial.FromSource(Label, source);
            CanvasServer.Current.SetMaterial(_previewItem, _material);
            _wanted = LabShaderValues.Wanted(declared);
            if (previous is not null)
                _context.Pipelines.Forget(previous);
            _owner.Show($"compiled in {ms} ms{LabShaderValues.Describe(_wanted)}", Palette.Success, _shownProblems);
        }

        public void Dispose()
        {
            _context.ClaimTyping(false);
            if (_material is not null)
                _context.Pipelines.Forget(_material);
        }
    }
}
