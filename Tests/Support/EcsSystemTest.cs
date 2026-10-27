using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;

namespace Xanadu.Tests.Support;

/// <summary>
/// Owns the common EntityStore/SystemRoot pair used by system-level tests.
/// Derivations can override OnEcsSetUp to build their systems.
/// </summary>
public abstract class EcsSystemTest
{
    protected EntityStore Store { get; private set; } = null!;
    protected SystemRoot Root { get; private set; } = null!;

    private float _time;

    [SetUp]
    public void EcsSystemSetUp()
    {
        Store = new EntityStore();
        Root = new SystemRoot(Store);
        _time = 0;
        OnEcsSetUp();
    }

    protected virtual void OnEcsSetUp() { }

    protected void Frame(int frames = 1, float secondsPerFrame = 1f / 60f)
    {
        for (int i = 0; i < frames; i++)
        {
            _time += secondsPerFrame;
            Root.Update(new UpdateTick(secondsPerFrame, _time));
        }
    }
}
