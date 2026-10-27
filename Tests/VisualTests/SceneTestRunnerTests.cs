using NUnit.Framework;
using Xanadu.Tests.VisualTests;

namespace Xanadu.Tests.VisualTests;

[TestFixture]
public sealed class SceneTestRunnerTests
{
    [Test]
    public void RunsStepsInOrderAndPasses()
    {
        var test = new SequenceTest();
        var runner = new SceneTestRunner();
        runner.Start(test);

        VisualTestResult? result = Drive(runner);

        Assert.That(result?.Passed, Is.True);
        Assert.That(test.StepRan, Is.True);
        Assert.That(test.StepCount, Is.EqualTo(4));
        Assert.That(
            test.Steps[0].State,
            Is.EqualTo(SceneTestStepState.Passed));
        Assert.That(
            test.Steps[3].State,
            Is.EqualTo(SceneTestStepState.Passed));
    }

    [Test]
    public void FailedAssertStopsTheRun()
    {
        var test = new FailingTest();
        var runner = new SceneTestRunner();
        runner.Start(test);

        VisualTestResult? result = Drive(runner);

        Assert.That(result?.Passed, Is.False);
        Assert.That(result?.Message, Does.Contain("intentional failure"));
        Assert.That(
            test.Steps[1].State,
            Is.EqualTo(SceneTestStepState.Failed));
    }

    [Test]
    public void UntilStepTimesOut()
    {
        var test = new TimeoutTest();
        var runner = new SceneTestRunner();
        runner.Start(test);

        VisualTestResult? result = Drive(runner, 1.0);

        Assert.That(result?.Passed, Is.False);
        Assert.That(result?.Message, Does.Contain("timed out"));
    }

    private static VisualTestResult? Drive(
        SceneTestRunner runner,
        double maxSeconds = 2.0)
    {
        double elapsed = 0;
        const double delta = 1.0 / 60.0;

        while (runner.Result is null && elapsed < maxSeconds)
        {
            runner.Update(delta);
            elapsed += delta;
        }

        return runner.Result;
    }

    private sealed class SequenceTest : SceneTest
    {
        public bool StepRan;
        public int StepCount;

        public override string Name => "Sequence";
        public override string Slug => "sequence";

        protected override void SetUpSteps()
        {
            AddStep("step", () => StepRan = true);
            AddWaitStep("wait", 1);
            AddUntilStep("until", () => StepRan);
            AddAssert("assert", () => StepRan);
            StepCount = Steps.Count;
        }
    }

    private sealed class FailingTest : SceneTest
    {
        public override string Name => "Failure";
        public override string Slug => "failure";

        protected override void SetUpSteps()
        {
            AddStep("first", () => { });
            AddAssert(
                "intentional failure",
                () => false,
                "expected false");
        }
    }

    private sealed class TimeoutTest : SceneTest
    {
        public override string Name => "Timeout";
        public override string Slug => "timeout";

        protected override void SetUpSteps()
        {
            AddUntilStep("never", () => false, timeoutSeconds: 0.05);
        }
    }
}
