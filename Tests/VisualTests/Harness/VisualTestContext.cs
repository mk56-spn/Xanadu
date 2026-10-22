using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Systems.Scenes;

namespace Writer.Tests.VisualTests;

/// <summary>Everything a visual test needs to boot and inspect a scene.</summary>
public sealed record VisualTestContext(
    EntityStore Entities,
    WriterSystemRoot Systems,
    Vector2 Screen,
    Control Host,
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
