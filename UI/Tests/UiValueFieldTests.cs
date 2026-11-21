using System.Collections.Generic;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Tests.Support;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Ui;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Xanadu.Ui.Tests;

/// <summary>
/// Value fields end to end: shapes from type names, typing, scrubbing,
/// switches, steppers, edits announced through the native component event, and
/// a field bound to a material's value, matrices included.
/// </summary>
[TestFixture]
public sealed class UiValueFieldTests : EcsSystemTest
{
    private ScriptedPointer _pointer = null!;
    private UiNode _layer;
    private readonly List<(float[] Values, bool Live)> _heard = new();

    protected override void OnEcsSetUp()
    {
        _pointer = new ScriptedPointer();
        _heard.Clear();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new TextFieldFocusSystem());
        Root.Add(new ToggleSystem());
        Root.Add(new UiValueFieldSystem(_pointer));
        _layer = UiNode.Layer(Store, default, 5, new FakeVisualRidFactory());
    }

    private Entity Build(string type, float[] values)
    {
        Entity field = _layer.Column().ValueField(type, UiValueShape.Parse(type)!.Value, values, 300).Entity;
        field.OnValueChanged((now, live) => _heard.Add(((float[])now.Clone(), live)));
        Frame();
        return field;
    }

    private static Entity Slot(Entity field, int index) => field.GetComponent<UiValueField>().Slots[index];

    private static float[] Values(Entity field) => field.GetComponent<UiValueField>().Values;

    [TestCase("float", UiScalar.Float, 1, 1)]
    [TestCase("int3", UiScalar.Int, 1, 3)]
    [TestCase("uint2", UiScalar.UInt, 1, 2)]
    [TestCase("bool4", UiScalar.Bool, 1, 4)]
    [TestCase("float3x3", UiScalar.Float, 3, 3)]
    [TestCase("float2x4", UiScalar.Float, 2, 4)]
    [TestCase("half2", UiScalar.Float, 1, 2)]
    public void TypeNamesGiveTheirShape(string type, UiScalar scalar, int rows, int columns)
    {
        UiValueShape shape = UiValueShape.Parse(type)!.Value;
        Assert.That((shape.Scalar, shape.Rows, shape.Columns), Is.EqualTo((scalar, rows, columns)));
    }

    [Test]
    public void ColoursAndNonsenseParse()
    {
        Assert.That(UiValueShape.Parse("colour")!.Value.Colour, Is.True);
        Assert.That(UiValueShape.Parse("float5"), Is.Null);
        Assert.That(UiValueShape.Parse("Texture2D"), Is.Null);
    }

    [Test]
    public void AMatrixIsAGridOfOneSlotPerNumber()
    {
        Entity field = Build("float2x3", [1, 2, 3, 4, 5, 6]);

        Assert.That(field.GetComponent<UiValueField>().Slots, Has.Length.EqualTo(6));
        Assert.That(Slot(field, 5).GetComponent<TextField>().Value, Is.EqualTo("6"));
        Assert.That(Slot(field, 4).GetComponent<UiValueSlot>().Bar.IsNull, Is.False);
    }

    [Test]
    public void TypingAppliesAsItParsesAndIsFinalWhenFocusLeaves()
    {
        Entity field = Build("float3", [0, 1, 0]);
        Entity y = Slot(field, 1);

        y.AddTag<TextFieldFocused>();
        y.GetComponent<TextField>().Value = "2.5";
        Frame();
        Assert.That(Values(field)[1], Is.EqualTo(2.5f));
        Assert.That(_heard[^1].Live, Is.True);

        y.GetComponent<TextField>().Value = "2.5x";
        Frame();
        Assert.That(Values(field)[1], Is.EqualTo(2.5f), "text that doesn't parse leaves the value");

        y.RemoveTag<TextFieldFocused>();
        Frame();
        Assert.That(_heard[^1].Live, Is.False);
        Assert.That(_heard[^1].Values, Is.EqualTo(new[] { 0f, 2.5f, 0f }));
        Assert.That(y.GetComponent<TextField>().Value, Is.EqualTo("2.5"));
    }

    [Test]
    public void DraggingAWholeNumberStepsItAndTakesNoTyping()
    {
        Entity field = Build("int2", [3, -2]);
        Entity x = Slot(field, 0);

        _pointer.Current = new PointerInput(new Vector2(100, 10), true);
        x.AddTag<Pressed>();
        Frame();
        _pointer.Current = new PointerInput(new Vector2(100 + UiValueFieldSystem.StepPixels * 3 + 1, 10), true);
        Frame();

        Assert.That(Values(field)[0], Is.EqualTo(6f));
        Assert.That(x.Tags.Has<TextFieldFocused>(), Is.False);
        Assert.That(_heard[^1].Live, Is.True);

        x.RemoveTag<Pressed>();
        _pointer.Current = new PointerInput(new Vector2(130, 10), false);
        Frame();
        Assert.That(_heard[^1].Live, Is.False, "letting go makes the drag one final edit");
        Assert.That(Values(field)[0], Is.EqualTo(6f));
    }

    [Test]
    public void ScrubbingAFloatCrossesItsRangeInTheScrubSpan()
    {
        Entity field = _layer.Column().ValueField("v", UiValueShape.Float, [0], 300, range: (0, 10)).Entity;
        Frame();

        _pointer.Current = new PointerInput(new Vector2(50, 10), true);
        Slot(field, 0).AddTag<Pressed>();
        Frame();
        _pointer.Current = new PointerInput(new Vector2(50 + UiValueFieldSystem.ScrubSpan / 2, 10), true);
        Frame();

        Assert.That(Values(field)[0], Is.EqualTo(5f).Within(1e-4f));
    }

    [Test]
    public void ASwitchFlipsWithItsToggleAndFollowsCode()
    {
        Entity field = Build("bool3", [1, 0, 1]);
        Entity y = Slot(field, 1);

        y.AddTag<Pressed>();
        Frame(2);
        y.RemoveTag<Pressed>();
        Frame();
        Assert.That(Values(field), Is.EqualTo(new[] { 1f, 1f, 1f }));
        Assert.That(_heard[^1].Values, Is.EqualTo(new[] { 1f, 1f, 1f }));

        int heard = _heard.Count;
        UiValueFields.Set(field, [0, 0, 0]);
        Frame();
        Assert.That(y.GetComponent<Toggle>().Value, Is.False);
        Assert.That(_heard, Has.Count.EqualTo(heard), "a value set in code is not an edit");
    }

    [Test]
    public void AnUnsignedNumberStepsButNeverBelowZero()
    {
        Entity field = Build("uint", [1]);
        Entity less = field.GetComponent<UiValueField>().Less;

        for (int i = 0; i < 3; i++)
        {
            less.AddTag<Pressed>();
            Frame();
            less.RemoveTag<Pressed>();
            Frame();
        }

        Assert.That(Values(field)[0], Is.EqualTo(0f));
    }

    [Test]
    public void ABoundMatrixLandsOnTheMaterialRowByRow()
    {
        var material = new CanvasMaterial("test.slang");
        var param = new MaterialParam("shear", 3, false, default, Rows: 2);
        Entity field = _layer.Column().UniformField(material, param, 300).Entity;
        Frame();

        Assert.That(field.GetComponent<UiValueField>().Shape, Is.EqualTo(UiValueShape.Matrix(2, 3)));
        // Unset, it shows the identity the renderer uses.
        Assert.That(Values(field), Is.EqualTo(new[] { 1f, 0, 0, 0, 1, 0 }));

        Entity rowOneColumnTwo = Slot(field, 5);
        rowOneColumnTwo.AddTag<TextFieldFocused>();
        rowOneColumnTwo.GetComponent<TextField>().Value = "0.5";
        Frame();

        Matrix4x4 written = material.Matrices["shear"];
        Assert.That(written[1, 2], Is.EqualTo(0.5f));
        Assert.That(written[0, 0], Is.EqualTo(1f));
    }

    [Test]
    public void ABoundColourIsAVectorOnTheMaterial()
    {
        var material = new CanvasMaterial("test.slang");
        material.Set("tint", new Vector4(1, 0, 0, 1));
        Entity field = _layer.Column().UniformField(material, new MaterialParam("tint", 4, false, default), 300).Entity;
        Frame();

        Assert.That(field.GetComponent<UiValueField>().Shape.Colour, Is.True);
        Entity g = Slot(field, 1);
        g.AddTag<TextFieldFocused>();
        g.GetComponent<TextField>().Value = "2";
        Frame();

        Assert.That(material.Get("tint"), Is.EqualTo(new Vector4(1, 1, 0, 1)), "a colour channel stays within 0..1");
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }
}
