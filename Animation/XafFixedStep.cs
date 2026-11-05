using System;
using Godot;

namespace Xanadu.Animation;

/// <summary>
/// Runs a Verlet body's own <c>Step</c> at a fixed rate, several times when the
/// frame was long. Every simulated body, chain or pendulum, steps this way.
/// </summary>
internal static class XafFixedStep
{
    public static void Run(float delta, float fixedStep, Action<float> step)
    {
        if (fixedStep <= 0f)
            return;

        int steps = Mathf.Max(1, Mathf.RoundToInt(delta / fixedStep));
        for (int i = 0; i < steps; i++)
            step(fixedStep);
    }
}
