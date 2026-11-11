using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Labs.Experiments;
using Xanadu.Systems.Canvas;

namespace Xanadu.Labs.Editor;

/// <summary>What a compile hands back: whatever the preview needs to show it, and the values it can tune.</summary>
internal sealed record PreviewBuild(object Result, ShaderParam[] Params);

/// <summary>
/// What the shader editor turns its code into and draws on the right. The editor
/// owns the text, the problems, completion and the layout; a preview owns the
/// compile and the picture. A level shader slot is <see cref="MaterialPreview"/>;
/// the spawner is <see cref="SpawnerPreview"/>.
/// </summary>
internal interface IEditorPreview : IDisposable
{
    /// <summary>The name Slang's errors carry for the code's own lines.</summary>
    string Label { get; }

    /// <summary>Values the code may use without declaring them, for completion and hovering.</summary>
    IEnumerable<(string Name, string Type, string? Note)> Provided { get; }

    /// <summary>Two lines under the problems: what the code is, and what it may use undeclared.</summary>
    string[] Help { get; }

    /// <summary>Slang the code can lean on that isn't in the library (types the preview declares), for completion.</summary>
    string Prelude => "";

    /// <summary>Type names <see cref="Prelude"/> declares, so the lint knows them.</summary>
    IEnumerable<string> Types => [];

    /// <summary>Off the frame: compiles the code, or throws with Slang's message.</summary>
    PreviewBuild Compile(string source);

    /// <summary>On the frame: shows what <see cref="Compile"/> made. Returns what to add to the status (" · lab sets time").</summary>
    string Show(PreviewBuild build);

    /// <summary>Where the picture goes; called at first and when the divider moves.</summary>
    void Place(Rect2 area);

    /// <summary>Every frame: the pointer (on the preview, or where it last was) and the sliders' values.</summary>
    void Update(in LabFrame frame, Vector2 pointer, EditorTuning tuning);

    /// <summary>The preview's own GPU passes, before the canvas draws.</summary>
    void Render(MoonWorks.Graphics.CommandBuffer commands) { }
}

/// <summary>A level shader slot: <c>shade</c> on a 16:9 quad, through the canvas's materials.</summary>
internal sealed class MaterialPreview(LabContext context, Handle item) : IEditorPreview
{
    private CanvasMaterial? _material;
    private int[] _wanted = [];
    private Rect2 _area;

    public string Label => "editor";

    public string[] Help =>
    [
        "shade(Canvas canvas) returns the pixel (add ItemInput i for uv …); import xanadu for shapes, colours., ease. …",
        "Use time, mouse, mouseUv or resolution and the lab fills it; no need to declare them.",
    ];

    public IEnumerable<(string Name, string Type, string? Note)> Provided =>
        Array.ConvertAll(LabShaderValues.All, value => (value.Name, value.Type, LabShaderValues.Note(value.Name)));

    public PreviewBuild Compile(string source)
    {
        // What the lab provides is declared for the code that uses it; the file keeps what was typed.
        string full = LabShaderValues.Declare(source);
        CompiledShader compiled = SlangShaders.CompileEffectBody(Label, full, keepsState: true);
        return new PreviewBuild(full, compiled.Params);
    }

    public string Show(PreviewBuild build)
    {
        CanvasMaterial? previous = _material;
        _material = CanvasMaterial.FromSource(Label, (string)build.Result, keepsState: true);
        CanvasServer.Current.SetMaterial(item, _material);
        _wanted = LabShaderValues.Wanted(Array.ConvertAll(build.Params, param => param.Name));
        if (previous is not null)
            context.Pipelines.Forget(previous);
        return LabShaderValues.Describe(_wanted);
    }

    public void Place(Rect2 area)
    {
        _area = area;
        CanvasServer.Current.Clear(item);
        CanvasServer.Current.AddRect(item, area, new Color("#0b1020"));
    }

    public void Update(in LabFrame frame, Vector2 pointer, EditorTuning tuning)
    {
        if (_material is null)
            return;
        LabShaderValues.Apply(_material, _wanted, _area, pointer, frame.Time);
        tuning.Apply(_material);
    }

    public void Dispose()
    {
        if (_material is not null)
            context.Pipelines.Forget(_material);
    }
}
