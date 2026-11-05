namespace Xanadu.Animation;

/// <summary>A simulated Verlet body: its own step, run at a fixed rate.</summary>
public abstract class XafFixedBody
{
    public abstract void Step(float delta);

    public void StepFixed(float delta, float fixedStep = 1f / 60f) =>
        XafFixedStep.Run(delta, fixedStep, Step);
}
