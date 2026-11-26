using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>
/// Before any test compiles: the suites compile every shipped stack several ways, side by side, so they take more Slang
/// sessions than the host's four (<see cref="SlangNative.MostSessions"/>; about 200 MB each, kept until the run ends).
/// </summary>
[SetUpFixture]
public sealed class TestSessions
{
    [OneTimeSetUp]
    public void TakeMoreSessions() => Environment.SetEnvironmentVariable("XANADU_SLANG_SESSIONS", "8");
}
