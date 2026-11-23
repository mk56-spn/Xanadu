using NUnit.Framework;
using Xanadu.Core.Rendering.Rhythm;

namespace Xanadu.Slang.Tests;

/// <summary>Declares the game's rhythm globals once for every test here, as each exe does at start-up.</summary>
[SetUpFixture]
public sealed class RhythmGlobalsSetUp
{
    [OneTimeSetUp]
    public void Declare() => RhythmGlobals.Declare();
}
