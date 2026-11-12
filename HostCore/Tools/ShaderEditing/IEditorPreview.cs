using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Slang;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>What a compile hands back: whatever the preview needs to show it, and the values it can tune.</summary>
internal sealed record PreviewBuild(object Result, ShaderParam[] Params);

/// <summary>
/// What the shader editor turns its code into and draws on the right. The editor
/// owns the text, the problems, completion and the layout; a preview owns the
/// compile and the picture. Labs has a level shader slot and the spawner;
/// the shader tool has whole shipped effects.
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

    /// <summary>The preview's own GPU passes, before the canvas draws.</summary>
    void Render(MoonWorks.Graphics.CommandBuffer commands) { }
}
