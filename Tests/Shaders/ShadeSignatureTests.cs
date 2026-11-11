using NUnit.Framework;
using Xanadu.Rendering;

namespace Xanadu.Tests.Shaders;

[TestFixture]
public sealed class ShadeSignatureTests
{
    [TestCase("float4 shade(ItemInput i) { return i.colour; }", "i")]
    [TestCase("float4 shade(Canvas canvas) { return 1; }", "i.canvas")]
    [TestCase("float4 shade(ItemInput input, Canvas c) { return 1; }", "i, i.canvas")]
    [TestCase("float4 shade( Canvas canvas , ItemInput i ) { return 1; }", "i.canvas, i")]
    [TestCase("float4 shade() { return 1; }", "")]
    [TestCase("// float4 shade(float2 p)\nfloat4 shade(Canvas canvas) { return 1; }", "i.canvas")]
    public void TheWrapperPassesWhatShadeAsksFor(string source, string expected) =>
        Assert.That(ShadeSignature.Arguments(source, out _), Is.EqualTo(expected));

    [TestCase("float4 shade(float2 p) { return 1; }")]
    [TestCase("float4 colourise(Canvas canvas) { return 1; }")]
    [TestCase("float3 shade(Canvas canvas) { return 1; }")]
    public void AnythingElseIsAProblem(string source)
    {
        Assert.That(ShadeSignature.Arguments(source, out string problem), Is.Null);
        Assert.That(problem, Is.Not.Empty);
    }
}
