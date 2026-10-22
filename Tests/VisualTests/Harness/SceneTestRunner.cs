using System;

namespace Writer.Tests.VisualTests;

/// <summary>Runs scene test steps one at a time and reports the first failure.</summary>
public sealed class SceneTestRunner
{
    private const double StepDelaySeconds = 0.02;

    private SceneTest? _test;
    private int _index;
    private double _delay;

    public SceneTest? Test => _test;

    public SceneTestStep? CurrentStep =>
        _test is not null && _index >= 0 && _index < _test.Steps.Count
            ? _test.Steps[_index]
            : null;

    public VisualTestResult? Result { get; private set; }

    public void Clear()
    {
        _test = null;
        _index = 0;
        _delay = 0;
        Result = null;
    }

    public void Start(SceneTest test)
    {
        _test = test;
        _index = 0;
        _delay = 0;
        Result = null;
        test.ResetSteps();
    }

    public VisualTestResult? Update(double delta)
    {
        if (_test is null || Result.HasValue)
            return Result;

        if (_delay > 0)
        {
            _delay -= delta;
            return null;
        }

        if (_index >= _test.Steps.Count)
        {
            Result = VisualTestResult.Pass("all steps passed");
            return Result;
        }

        SceneTestStep step = _test.Steps[_index];

        if (step.State == SceneTestStepState.Pending)
            step.State = SceneTestStepState.Running;

        try
        {
            if (!step.Tick(_test, delta))
                return null;

            step.State = SceneTestStepState.Passed;
            _index++;
            _delay = StepDelaySeconds;
        }
        catch (Exception exception)
        {
            step.State = SceneTestStepState.Failed;
            step.Error = exception.Message;
            Result = VisualTestResult.Fail(
                $"{step.Description}: {exception.Message}");
        }

        return Result;
    }
}
