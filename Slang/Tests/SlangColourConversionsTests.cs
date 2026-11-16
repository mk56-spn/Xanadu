using System;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>
/// A float3 where a colour is wanted compiles as that colour, opaque, and the
/// places are reported for the editor's hints; a float3 for a plain float4 is
/// still Slang's error. Real Slang, real library.
/// </summary>
[TestFixture]
public sealed class SlangColourConversionsTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static string At(string source, ColourConversion conversion) =>
        source.Split('\n')[conversion.Line].Substring(conversion.Column, conversion.Length);

    [Test]
    public void AColourDeclarationTakesAFloat3()
    {
        const string source = """
            import xanadu;
            float4 shade(ItemInput i)
            {
                colour c = i.colour.rgb * 0.5;
                return c;
            }
            """;

        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test", source));
        Assert.That(SlangShaders.ColourConversionsOf(source).Select(c => At(source, c)), Is.EqualTo(new[] { "i.colour.rgb * 0.5" }));
    }

    [Test]
    public void AShadeReturningColourReturnsAFloat3()
    {
        const string source = """
            import xanadu;
            colour shade(ItemInput i) { return i.uv.xyx; }
            """;

        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test", source));
        Assert.That(SlangShaders.ColourConversionsOf(source).Select(c => At(source, c)), Is.EqualTo(new[] { "i.uv.xyx" }));
    }

    [Test]
    public void ColourArgumentsAndAssignmentsTakeFloat3s()
    {
        const string source = """
            import xanadu;
            float4 tinted(colour c, float k) { return c * k; }
            float4 shade(ItemInput i)
            {
                colour c = colours.gold;
                c = i.colour.rgb;
                return tinted(i.uv.xyx, 2.0) + colours.gold.over(float3(0.5));
            }
            """;

        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test", source));
        Assert.That(SlangShaders.ColourConversionsOf(source).Select(c => At(source, c)),
            Is.EquivalentTo(new[] { "i.colour.rgb", "i.uv.xyx", "float3(0.5)" }));
    }

    [Test]
    public void APlainFloat4StaysAnError()
    {
        const string source = """
            import xanadu;
            float4 shade(ItemInput i)
            {
                float4 c = i.colour.rgb;
                return c;
            }
            """;

        var error = Assert.Throws<Exception>(() => SlangShaders.CompileEffectBody("test", source));
        Assert.That(error!.Message, Does.Contain("vector<float,3>"));
        Assert.That(SlangShaders.ColourConversionsOf(source), Is.Empty);
    }

    [Test]
    public void ConversionsAreKeptWhenSomethingElseIsWrong()
    {
        const string source = """
            import xanadu;
            float4 shade(ItemInput i)
            {
                colour c = i.colour.rgb;
                return c * undefinedThing;
            }
            """;

        var error = Assert.Catch(() => SlangShaders.CompileEffectBody("test", source));
        Assert.That(error!.Message, Does.Contain("undefinedThing").And.Not.Contain("vector<float,3>"));
        Assert.That(SlangShaders.ColourConversionsOf(source).Select(c => At(source, c)), Is.EqualTo(new[] { "i.colour.rgb" }));
    }
}
