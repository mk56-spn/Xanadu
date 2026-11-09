using System.Collections.Generic;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Editor;
using Xanadu.Timeline;

namespace Xanadu.Tests.Editor;

/// <summary>
/// Editing shader keys from the group panel's key lane and the layers panel:
/// retiming, easing, deleting, typed values, and changing the track's shader.
/// </summary>
[TestFixture]
public sealed class EditorShaderKeyEditsTests
{
    private static readonly InstanceUniform Tint = new("tint", 4, new Vector4(1, 1, 1, 1));
    private static readonly InstanceUniform Speed = new("speed", 1, new Vector4(2, 0, 0, 0));

    private static List<ShaderKey> Keys(params (string Param, float Time, float X)[] keys) =>
        keys.Select(key => new ShaderKey(key.Param, key.Time, new Vector4(key.X, 0, 0, 0), 1, EasePreset.Linear)).ToList();

    [Test]
    public void RetimingMovesTheKeyAndReplacesOneAlreadyThere()
    {
        List<ShaderKey> keys = Keys(("speed", 1f, 1f), ("speed", 2f, 2f), ("tint", 2f, 9f));

        Assert.That(EditorShaderKeyEdits.Retime(keys, "speed", 1f, 2f), Is.True);

        Assert.That(EditorShaderKeyEdits.Lane(keys, "speed").Select(key => (key.Time, key.Value.X)), Is.EqualTo(new[] { (2f, 1f) }));
        Assert.That(keys.Count(key => key.Param == "tint"), Is.EqualTo(1), "other settings' keys are left alone");
        Assert.That(EditorShaderKeyEdits.Retime(keys, "speed", 5f, 6f), Is.False, "no key there");
    }

    [Test]
    public void SetEaseAndDeleteActOnlyOnThatKey()
    {
        List<ShaderKey> keys = Keys(("speed", 1f, 1f), ("speed", 2f, 2f));

        Assert.That(EditorShaderKeyEdits.SetEase(keys, "speed", 1f, EasePreset.BounceOut), Is.True);
        Assert.That(keys.Single(key => key.Time == 1f).Ease, Is.EqualTo(EasePreset.BounceOut));
        Assert.That(keys.Single(key => key.Time == 2f).Ease, Is.EqualTo(EasePreset.Linear));
        Assert.That(EditorShaderKeyEdits.SetEase(keys, "speed", 5f, EasePreset.Step), Is.False, "no key there");

        Assert.That(EditorShaderKeyEdits.Delete(keys, "speed", 2f), Is.True);
        Assert.That(keys.Select(key => key.Time), Is.EqualTo(new[] { 1f }));
    }

    [Test]
    public void KeyingAtAKeysMomentKeepsItsEasing()
    {
        List<ShaderKey> keys = Keys(("speed", 1f, 1f));
        EditorShaderKeyEdits.SetEase(keys, "speed", 1f, EasePreset.CubicIn);
        EasePreset eased = keys[0].Ease;

        EditorShaderKeyEdits.KeyValue(keys, Speed, 0, 0.05f, 1f);

        Assert.That(keys.Single().Value.X, Is.EqualTo(1.05f).Within(0.0001f));
        Assert.That(keys.Single().Ease, Is.EqualTo(eased));
    }

    [Test]
    public void ChangingTheShaderKeepsKeysOfSettingsItStillDeclares()
    {
        List<ShaderKey> keys = Keys(("speed", 1f, 1f), ("gone", 1f, 1f));

        EditorShaderKeyEdits.KeepFor(keys, new[] { new InstanceUniform("speed", 2) });

        Assert.That(keys.Select(key => (key.Param, key.Size)), Is.EqualTo(new[] { ("speed", 2) }));
    }

    [Test]
    public void TypedValuesReadPerChannelSpreadOrAsAColour()
    {
        Assert.That(EditorShaderKeyEdits.TryParse("0.25 0.5, 0.75 1", Tint, out Vector4 four), Is.True);
        Assert.That(four, Is.EqualTo(new Vector4(0.25f, 0.5f, 0.75f, 1f)));

        Assert.That(EditorShaderKeyEdits.TryParse("0.5", Tint, out Vector4 spread), Is.True);
        Assert.That(spread, Is.EqualTo(new Vector4(0.5f, 0.5f, 0.5f, 0.5f)));

        Assert.That(EditorShaderKeyEdits.TryParse("#ff0000", Tint, out Vector4 red), Is.True);
        Assert.That(red, Is.EqualTo(new Vector4(1f, 0f, 0f, 1f)));

        Assert.That(EditorShaderKeyEdits.TryParse("1 2", Tint, out _), Is.False, "two values for four channels");
        Assert.That(EditorShaderKeyEdits.TryParse("#ff0000", Speed, out _), Is.False, "a colour needs three channels");
        Assert.That(EditorShaderKeyEdits.TryParse("fast", Speed, out _), Is.False);
        Assert.That(EditorShaderKeyEdits.Format(four, 4), Is.EqualTo("0.25 0.5 0.75 1"));
    }

    [Test]
    public void TheSlotChoiceNamesEverySlot()
    {
        LevelShaders shaders = LevelShaders.Create();
        shaders.Slots[0] = new ShaderSlot { Name = "Blur", Source = "shader_type canvas_item;" };

        string[] labels = ShaderSlotChoice.Labels(shaders);

        Assert.That(labels, Has.Length.EqualTo(ShaderLimits.Count + 1));
        Assert.That(labels[0], Is.EqualTo("OFF"));
        Assert.That(labels[1], Is.EqualTo("1  BLUR"));
        Assert.That(labels[2], Is.EqualTo("2  (empty)"));
        Assert.That(ShaderSlotChoice.Button(shaders, 1), Is.EqualTo("SHD 1 BLUR ▾"));
        Assert.That(ShaderSlotChoice.Button(shaders, 0), Is.EqualTo("SHD OFF ▾"));
    }
}
