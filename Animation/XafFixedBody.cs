namespace Xanadu.Animation;

/// <summary>A simulated Verlet body: its own step, run at a fixed rate.</summary>
public abstract class XafFixedBody
{
    public abstract void Step(float delta);
}
