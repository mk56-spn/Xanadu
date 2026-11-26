using System.Collections.Generic;
using Xanadu.Slang;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// What a compile hands back: whatever the preview needs to show it, the values it can tune, and the
/// compiled stack its render graph is read from (null when the preview has none).
/// </summary>
internal sealed record PreviewBuild(object Result, ShaderParam[] Params, CompiledShader? Stack = null);

/// <summary>
/// What the shader editor turns its code into and draws on the right. The editor
/// owns the text, the problems, completion and the layout; a preview owns the
/// compile and the picture: the shader tool's is a whole stack.
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
    void Update(in ToolFrame frame, Vector2 pointer, EditorTuning tuning);

    /// <summary>
    /// Whether the shader reads the clock the editor's clip timeline hands it
    /// (<see cref="UseClock"/>), so scrubbing the timeline moves it. A preview
    /// that keeps the host's time shows the timeline following that time instead.
    /// </summary>
    bool TakesClock => false;

    /// <summary>Every frame before <see cref="Update"/>: the time the shader should see, in seconds and song beats.</summary>
    void UseClock(float seconds, float beats) { }

    /// <summary>
    /// Every frame: the window the graph's probe nodes sample (<c>Slang.ProbeNodes</c>): from, span in seconds, beats a
    /// second. A preview that probes declares the window in what it compiles and sets it on its material.
    /// </summary>
    void UseProbeWindow(Vector4 window) { }

    /// <summary>
    /// What stops the shown code running, found while it runs rather than when it compiled (a graph whose values make
    /// it impossible); null when nothing does. Read every frame, listed with the problems.
    /// </summary>
    string? Problem => null;

    /// <summary>The preview's own GPU passes, before the canvas draws.</summary>
    void Render(MoonWorks.Graphics.CommandBuffer commands) { }
}
