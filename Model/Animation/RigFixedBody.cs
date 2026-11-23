namespace Xanadu.Model.Animation;

/// <summary>A simulated Verlet body: its own step, run at a fixed rate.</summary>
public abstract class RigFixedBody
{
    public abstract void Step(float delta);
}
