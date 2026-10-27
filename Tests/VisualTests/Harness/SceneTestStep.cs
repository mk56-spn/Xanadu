using System;

namespace Xanadu.Tests.VisualTests;

public enum SceneTestStepState
{
    Pending,
    Running,
    Passed,
    Failed,
}

/// <summary>One step in a scene test. Tick returns true when the step is done.</summary>
public abstract class SceneTestStep(string description)
{
    public string Description { get; } = description;

    public SceneTestStepState State { get; internal set; }

    public string? Error { get; internal set; }

    public double Elapsed { get; internal set; }

    public virtual void Reset()
    {
        State = SceneTestStepState.Pending;
        Error = null;
        Elapsed = 0;
    }

    public abstract bool Tick(SceneTest test, double delta);
}

public sealed class ActionTestStep(string description, Action action)
    : SceneTestStep(description)
{
    public override bool Tick(SceneTest test, double delta)
    {
        action();
        return true;
    }
}

public sealed class AssertTestStep(
    string description,
    Func<bool> assertion,
    string? extendedDescription = null)
    : SceneTestStep(description)
{
    public override bool Tick(SceneTest test, double delta)
    {
        if (assertion())
            return true;

        throw new SceneTestAssertionException(
            string.IsNullOrEmpty(extendedDescription)
                ? Description
                : $"{Description}: {extendedDescription}");
    }
}

public sealed class UntilTestStep(
    string description,
    Func<bool> condition,
    double timeoutSeconds)
    : SceneTestStep(description)
{
    public override bool Tick(SceneTest test, double delta)
    {
        Elapsed += delta;

        if (condition())
            return true;

        if (Elapsed >= timeoutSeconds)
            throw new TimeoutException($"\"{Description}\" timed out after {timeoutSeconds:0.0}s");

        return false;
    }
}

public sealed class WaitTestStep : SceneTestStep
{
    private readonly int _frames;
    private int _remaining;

    public WaitTestStep(string description, int frames) : base(description)
    {
        _frames = frames;
        _remaining = frames;
    }

    public override void Reset()
    {
        base.Reset();
        _remaining = _frames;
    }

    public override bool Tick(SceneTest test, double delta) =>
        --_remaining <= 0;
}

public sealed class SceneTestAssertionException(string message) : Exception(message);
