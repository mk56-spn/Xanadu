using Friflo.Engine.ECS;
using Xanadu.Ecs;
using System.Collections.Generic;
using Xanadu.Editor;
using Xanadu.Systems.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>Everything a visual test needs to boot and inspect a scene.</summary>
/// <param name="Host">The canvas item the scene draws under, beside the sidebar.</param>
public sealed record VisualTestContext(
    EntityStore Entities,
    XanaduSystemRoot Systems,
    Vector2 Screen,
    Handle Host,
    ScriptedPointerInputSource Pointer,
    ScriptedKeyboardState Keyboard)
{
    public SceneSystem Scenes => Systems.Scenes;

    /// <summary>Editor overlays a test opened: ticked after the systems each frame and closed with the scene.</summary>
    public List<EditorOverlay> Overlays { get; } = new();
}

public readonly record struct VisualTestResult(bool Passed, string Message)
{
    public static VisualTestResult Pass(string message = "OK") =>
        new(true, message);

    public static VisualTestResult Fail(string message) =>
        new(false, message);
}
