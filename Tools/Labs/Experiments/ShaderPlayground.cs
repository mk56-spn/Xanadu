using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Storage;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A Slang effect on a full stage, recompiled every time its file is saved.
/// The file is written exactly like a level's shader slot (uniforms plus
/// <c>float4 shade(ItemInput i)</c>, <c>import xanadu;</c> for the library), so
/// anything made here pastes straight into a level. A broken save keeps the
/// last good version on screen and shows Slang's errors in the panel.
/// </summary>
internal sealed class ShaderPlayground(string? path) : ILabExperiment
{
    private const string WorkingCopy = "res://Tools/Labs/Playground/playground.slang";
    private const string Template = "res://Tools/Labs/Playground/playground.template.slang";
    private const int ErrorLines = 18;
    private const int ErrorColumns = 80;

    /// <summary>The panel's inside width, so error lines can sit left-aligned in boxes of a fixed size.</summary>
    private const float ErrorWidth = 590;

    private Entity _status;
    private readonly Entity[] _errors = new Entity[ErrorLines];

    public string Name => "SHADER PLAYGROUND";

    public string[] Notes =>
    [
        "Edit the file below in any editor: every save",
        "recompiles and redraws. It is a level shader slot:",
        "uniforms and float4 shade(ItemInput i); import xanadu.",
        "Keep state across frames with struct State and",
        "void update(inout State s, float dt); shade reads state.",
        .. LabShaderValues.Notes,
    ];

    /// <summary>The watched file: <c>--shader</c>, or the lab's own working copy, made from the template the first time.</summary>
    private string File
    {
        get
        {
            if (path is not null)
                return path;

            string copy = GamePaths.Globalize(WorkingCopy);
            if (!System.IO.File.Exists(copy))
                System.IO.File.Copy(GamePaths.Globalize(Template), copy);
            return copy;
        }
    }

    public void BuildPanel(UiNode panel)
    {
        panel.Text(File, UiType.Small, Palette.TextDim);
        _status = panel.Text("waiting for the first compile…", UiType.Small, Palette.TextMuted);
        // Slang's errors point at columns with carets, so they read left-aligned in a monospaced face.
        UiNode column = panel.Column().Cross(StackCrossAlignment.Start).Width(ErrorWidth);
        for (int i = 0; i < ErrorLines; i++)
            _errors[i] = column.Text("", UiType.Caption, Palette.DangerBright, TextAlign.Left).Mono().Width(ErrorWidth);
    }

    public ILabStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    private void ShowStatus(string text, Color colour)
    {
        UiText.Set(_status, text);
        UiText.SetColor(_status, colour);
    }

    private void ShowErrors(string message)
    {
        string[] lines = message.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < ErrorLines; i++)
        {
            string line = i < lines.Length ? lines[i].Replace("\t", "    ") : "";
            UiText.Set(_errors[i], line.Length > ErrorColumns ? line[..(ErrorColumns - 1)] + "…" : line);
        }
    }

    private sealed class Stage : ILabStage
    {
        private const float PollSeconds = 0.2f;

        private readonly ShaderPlayground _owner;
        private readonly LabContext _context;
        private readonly Handle _item;
        private readonly Rect2 _area;
        private readonly string _file;
        private CanvasMaterial? _material;
        private DateTime _seen;
        private float _sincePoll = PollSeconds;
        private Task<(string Source, string[] Params, string? Error, long Ms)>? _compiling;

        /// <summary>The provided values the shader on screen declares, by index into <see cref="LabShaderValues.All"/>.</summary>
        private int[] _wanted = [];

        public Stage(ShaderPlayground owner, LabContext context, Handle item, Rect2 area)
        {
            _owner = owner;
            _context = context;
            _item = item;
            _area = area;
            _file = owner.File;
            CanvasServer.Current.AddRect(item, area, Colors.White);
        }

        public void Update(in LabFrame frame)
        {
            Finish();

            _sincePoll += frame.Delta;
            if (_sincePoll >= PollSeconds && _compiling is null)
            {
                _sincePoll = 0;
                Poll();
            }

            if (_material is null)
                return;

            LabShaderValues.Apply(_material, _wanted, _area, frame.Mouse, frame.Time);
        }

        /// <summary>Starts a compile when the file's write time moves; editors that save by renaming still bump it.</summary>
        private void Poll()
        {
            DateTime written;
            try
            {
                written = System.IO.File.GetLastWriteTimeUtc(_file);
            }
            catch (IOException)
            {
                return;
            }

            if (written == _seen)
                return;

            string source;
            try
            {
                source = System.IO.File.ReadAllText(_file);
            }
            catch (IOException)
            {
                // Mid-save; the next poll gets it.
                return;
            }

            _seen = written;
            _owner.ShowStatus("compiling…", Palette.TextMuted);
            // Off the frame, so the window keeps drawing the last good version meanwhile.
            _compiling = Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                try
                {
                    CompiledShader compiled = SlangShaders.CompileEffectBody(Name, LabShaderValues.Declare(source), keepsState: true);
                    string[] names = Array.ConvertAll(compiled.Params, param => param.Name);
                    return (source, names, (string?)null, clock.ElapsedMilliseconds);
                }
                catch (Exception exception)
                {
                    return (source, Array.Empty<string>(), exception.Message, clock.ElapsedMilliseconds);
                }
            });
        }

        private const string Name = "playground";

        private void Finish()
        {
            if (_compiling is not { IsCompleted: true } done)
                return;

            _compiling = null;
            (string source, string[] declared, string? error, long ms) = done.Result;
            if (error is not null)
            {
                _owner.ShowStatus($"error at {DateTime.Now:HH:mm:ss}: still showing the last version that compiled", Palette.Danger);
                _owner.ShowErrors(error);
                return;
            }

            // The renderer compiles it again for itself, from the disk cache this compile just filled.
            CanvasMaterial previous = _material!;
            _material = CanvasMaterial.FromSource(Name, LabShaderValues.Declare(source), keepsState: true);
            CanvasServer.Current.SetMaterial(_item, _material);
            _wanted = LabShaderValues.Wanted(declared);
            if (previous is not null)
                _context.Pipelines.Forget(previous);

            string given = LabShaderValues.Describe(_wanted);
            _owner.ShowStatus($"compiled at {DateTime.Now:HH:mm:ss} in {ms} ms{given}", Palette.Success);
            _owner.ShowErrors("");
        }

        public void Dispose()
        {
            if (_material is not null)
                _context.Pipelines.Forget(_material);
        }
    }
}
