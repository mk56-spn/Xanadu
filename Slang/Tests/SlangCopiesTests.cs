using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Every shipped stack without a graph has a copied form (SlangCopies) that compiles: its values read per copy.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class SlangCopiesTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    [TestCaseSource(typeof(ShippedEffectsTests), nameof(ShippedEffectsTests.Stacks))]
    public void CopiedFormCompiles(string folder)
    {
        CompiledShader effect = ShippedEffectsTests.Compile(folder);
        if (SlangCopies.Refusal(effect) is { } why)
            Assert.Ignore(why);

        CompiledCopies copies = SlangCopies.Compile(folder, effect);

        Assert.That(copies.Vertex.Spirv.Length, Is.GreaterThan(20));
        Assert.That(copies.Fragment.Spirv.Length, Is.GreaterThan(20));
        Assert.That(copies.ValuesSize, Is.EqualTo(effect.ParamsSize));
        // The values come per copy: no values block is left in either stage.
        Assert.That(copies.Fragment.UniformBuffers, Is.LessThanOrEqualTo(1));
        // Only real samplers count as samplers: the values buffer is a storage buffer, bound after the globals.
        Assert.That(copies.Vertex.Samplers, Has.Length.LessThanOrEqualTo(1));
        Assert.That(copies.Fragment.Samplers, Has.None.EqualTo("xanaduCopyValues"));
    }
}
