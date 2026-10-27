using System.IO;
using NUnit.Framework;
using Xanadu.Systems.Shaders;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class ShaderParamStoreTests
{
    private string _path = null!;

    [SetUp]
    public void SetUp() =>
        _path = Path.Combine(Path.GetTempPath(), "shader-params-" + Path.GetRandomFileName() + ".json");

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Test]
    public void SavedValuesComeBackAfterReload()
    {
        var store = new ShaderParamStore(_path);
        store.Set("res://a.gdshader", "speed", [0.7f]);
        store.Set("res://a.gdshader", "tint", [1f, 0.5f, 0.25f, 1f]);
        store.Save();

        var reloaded = new ShaderParamStore(_path);
        Assert.That(reloaded.Get("res://a.gdshader", "speed"), Is.EqualTo(new[] { 0.7f }));
        Assert.That(reloaded.Get("res://a.gdshader", "tint"), Has.Length.EqualTo(4));
        Assert.That(reloaded.Get("res://a.gdshader", "missing"), Is.Null);
        Assert.That(reloaded.Get("res://b.gdshader", "speed"), Is.Null);
    }

    [Test]
    public void MissingFileStartsEmpty() =>
        Assert.That(new ShaderParamStore(_path).Get("res://a.gdshader", "speed"), Is.Null);

    [Test]
    public void GuessedRangesBracketTheCurrentValue()
    {
        (float min, float max) = ShaderUniforms.GuessRange(UniformKind.Float, 250);
        Assert.That((min, max), Is.EqualTo((0f, 1000f)));

        (min, max) = ShaderUniforms.GuessRange(UniformKind.Float, 0.4f);
        Assert.That(max, Is.EqualTo(1.6f).Within(1e-5f));

        (min, max) = ShaderUniforms.GuessRange(UniformKind.Float, -3);
        Assert.That(min, Is.LessThan(-3));

        Assert.That(ShaderUniforms.GuessRange(UniformKind.Color, 0.5f), Is.EqualTo((0f, 1f)));
    }
}
