using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>Everything a visual test needs to boot and inspect a scene.</summary>
public sealed record VisualTestContext(
    EntityStore Entities,
    XanaduSystemRoot Systems,
    Vector2 Screen,
    Node2D Host,
    ScriptedPointerInputSource Pointer,
    ScriptedKeyboardState Keyboard)
{
    public SceneSystem Scenes => Systems.Scenes;
}

public readonly record struct VisualTestResult(bool Passed, string Message)
{
    public static VisualTestResult Pass(string message = "OK") =>
        new(true, message);

    public static VisualTestResult Fail(string message) =>
        new(false, message);
}
