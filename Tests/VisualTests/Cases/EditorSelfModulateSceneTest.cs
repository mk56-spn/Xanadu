using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// An item's self modulate in the real editor: the sidebar's colour field shows
/// the selection's colour, typing one tints the selection, and the swatch follows.
/// </summary>
public sealed class EditorSelfModulateSceneTest : SceneTest
{
    private Entity _block;

    public override string Name => "Editor self modulate";
    public override string Slug => "editor-self-modulate";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the colour field",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorSelfModulateField>().Count == 1);

        AddStep("place and select a block", () =>
        {
            // Placed by screen position, so they sit in view whatever the playfield's size.
            PlayfieldView view = Editor().GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            var size = new Vector2(180, 120) / view.Scale;
            EditorItems.Create(Context.Entities, new Block(At(650, 500), size));
            _block = EditorItems.Create(Context.Entities, new Block(At(900, 500), size));
            Entity note = EditorItems.Create(Context.Entities, new Note(0, At(760, 300)));
            SelfModulate.Put(note, new Color("#00ff80"));
            EditorItems.Create(Context.Entities, new Note(0, At(900, 300)));
            Editor().GetComponent<NoteSelection>().Note = _block;
        });
        AddUntilStep("the field shows white", () => Text() == "#ffffff");

        AddStep("focus the field", () => Field().AddTag<TextFieldFocused>());
        AddStep("type a colour", () => Field().GetComponent<TextField>().Value = "#00ff80");
        AddStep("leave the field", () => Field().RemoveTag<TextFieldFocused>());
        AddUntilStep("the block takes the colour", () => SelfModulate.Of(_block) == new Color("#00ff80"));
        AddAssert("the swatch follows", () =>
            Field().GetComponent<UiColorField>().Swatch.GetComponent<FillColor>().Value == new Color("#00ff80"));

        // Regression: letting go of a picker drag put the colour back to white.
        AddStep("press the picker's square", () =>
        {
            _picked = default;
            Context.Pointer.Click(SquareAt(0.8f, 0.3f));
        });
        AddWaitStep("hold on the square", 2);
        AddStep("drag across the square", () => Context.Pointer.Position = SquareAt(0.6f, 0.2f));
        AddWaitStep("drag settles", 2);
        AddStep("note the picked colour", () => _picked = Picker().GetComponent<UiColorPicker>().Color);
        AddStep("let go", () => Context.Pointer.Release());
        AddWaitStep("frames after letting go", 6);
        AddAssert("the picked colour is not white", () => !_picked.IsEqualApprox(Colors.White));
        AddAssert("the block keeps the picked colour", () => SelfModulate.Of(_block).IsEqualApprox(_picked));
        AddAssert("the field keeps the picked colour", () =>
            Field().GetComponent<UiColorField>().Value.IsEqualApprox(_picked));
        AddAssert("the picker keeps the picked colour", () =>
            Picker().GetComponent<UiColorPicker>().Color.IsEqualApprox(_picked));

        // The same for the outline's picker, and for a hue bar drag.
        DragAndCheck("outline", OutlineField, () => OutlineOf(_block));
        DragHueAndCheck("colour", Field, () => SelfModulate.Of(_block));
        DragHueAndCheck("outline", OutlineField, () => OutlineOf(_block));
    }

    private Entity OutlineField() => Editor().GetComponent<EditorBlockOutlinePanel>().Color;

    private static Color OutlineOf(Entity block) => block.GetComponent<Block>().OutlineColor;

    private void DragAndCheck(string name, System.Func<Entity> field, System.Func<Color> applied)
    {
        AddStep($"press the {name} square", () =>
            Context.Pointer.Click(PointOn(field(), area => area.Position + area.Size * new Vector2(0.7f, 0.4f))));
        AddWaitStep($"hold the {name} square", 2);
        AddStep($"drag the {name} square", () =>
            Context.Pointer.Position = PointOn(field(), area => area.Position + area.Size * new Vector2(0.5f, 0.25f)));
        AddWaitStep($"{name} drag settles", 2);
        Check(name, field, applied);
    }

    private void DragHueAndCheck(string name, System.Func<Entity> field, System.Func<Color> applied)
    {
        AddStep($"press the {name} hue bar", () =>
            Context.Pointer.Click(PointOnHue(field(), 0.2f)));
        AddWaitStep($"hold the {name} hue bar", 2);
        AddStep($"drag the {name} hue bar", () => Context.Pointer.Position = PointOnHue(field(), 0.45f));
        AddWaitStep($"{name} hue drag settles", 2);
        Check(name + " hue", field, applied);
    }

    private void Check(string name, System.Func<Entity> field, System.Func<Color> applied)
    {
        Color picked = default;
        AddStep($"note the {name} colour", () =>
            picked = field().GetComponent<UiColorField>().Picker.GetComponent<UiColorPicker>().Color);
        AddStep($"let go of the {name} picker", () => Context.Pointer.Release());
        AddWaitStep($"frames after the {name} drag", 6);
        AddAssert($"the {name} colour is not white", () => !picked.IsEqualApprox(Colors.White));
        AddAssert($"the block keeps the {name} colour", () => applied().IsEqualApprox(picked));
        AddAssert($"the {name} field keeps it", () => field().GetComponent<UiColorField>().Value.IsEqualApprox(picked));
        AddAssert($"the {name} picker keeps it", () =>
            field().GetComponent<UiColorField>().Picker.GetComponent<UiColorPicker>().Color.IsEqualApprox(picked));
    }

    private static Vector2 PointOn(Entity field, System.Func<Rect2, Vector2> at)
    {
        Entity square = field.GetComponent<UiColorField>().Picker;
        Vector2 center = UiTransform.WorldPosition(square, square.GetComponent<Xanadu.Ecs.Position>().Value);
        return at(ColorPickerLayout.Square(center));
    }

    private static Vector2 PointOnHue(Entity field, float down)
    {
        Entity square = field.GetComponent<UiColorField>().Picker;
        Vector2 center = UiTransform.WorldPosition(square, square.GetComponent<Xanadu.Ecs.Position>().Value);
        Rect2 bar = ColorPickerLayout.HueBar(center);
        return new Vector2(bar.GetCenter().X, bar.Position.Y + bar.Size.Y * down);
    }

    private Color _picked;

    private Entity Picker() => Field().GetComponent<UiColorField>().Picker;

    /// <summary>A point on the picker's square, as fractions across and down it.</summary>
    private Vector2 SquareAt(float across, float down)
    {
        Entity square = Picker();
        Vector2 center = UiTransform.WorldPosition(square, square.GetComponent<Xanadu.Ecs.Position>().Value);
        Rect2 area = ColorPickerLayout.Square(center);
        return area.Position + area.Size * new Vector2(across, down);
    }

    private Entity Editor() => Context.Entities.Query<EditorSelfModulateField>().ToEntityList()[0];

    private Entity Field() => Editor().GetComponent<EditorSelfModulateField>().Field;

    private string Text() => Field().GetComponent<TextField>().Value;
}
