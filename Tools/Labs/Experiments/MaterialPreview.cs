using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Systems.Canvas;
using Xanadu.Slang;
using Xanadu.Host.Tools;

namespace Xanadu.Labs.Experiments;

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
        "Click the preview and S D K L / F Space J are the play keys: clock.since(keys.action1) plays from a press.",
    ];

    public IEnumerable<(string Name, string Type, string? Note)> Provided =>
        Array.ConvertAll(LabShaderValues.All, value => (value.Name, value.Type, LabShaderValues.Note(value.Name)));

    public PreviewBuild Compile(string source)
    {
        // What the lab provides is declared for the code that uses it; the file keeps what was typed.
        string full = LabShaderValues.Declare(source);
        // The clip timeline drives the clock; declared on the last line, so error lines stay the author's.
        if (!SlangShaders.DrivesClock(full))
            full = full.TrimEnd('\n') + $" uniform float2 {SlangShaders.ClockOverride};\n";
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

    public bool TakesClock => true;

    private Vector4 _clock;

    public void UseClock(float seconds, float beats) => _clock = new Vector4(seconds, beats, 0, 0);

    public void Place(Rect2 area)
    {
        _area = area;
        CanvasServer.Current.Clear(item);
        CanvasServer.Current.AddRect(item, area, new Color("#0b1020"));
    }

    public void Update(in ToolFrame frame, Vector2 pointer, EditorTuning tuning)
    {
        if (_material is null)
            return;
        LabShaderValues.Apply(_material, _wanted, _area, pointer);
        _material.Set(SlangShaders.ClockOverride, _clock);
        tuning.Apply(_material);
    }

    public void Dispose()
    {
        if (_material is not null)
            context.Pipelines.Forget(_material);
    }
}
