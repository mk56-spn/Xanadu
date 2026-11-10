using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>Applies typed colours when a colour field loses focus and picked ones when its picker is let go, and keeps its text, swatch and picker current.</summary>
public sealed class UiColorFieldSystem : QuerySystem<UiColorField, TextField>
{
    protected override void OnUpdate()
    {
        // Marking a swatch dirty adds a tag, which a running query forbids.
        foreach (Entity entity in Query.ToEntityList())
            Update(entity);
    }

    private static void Update(Entity entity)
    {
        ref UiColorField field = ref entity.GetComponent<UiColorField>();
        ref TextField text = ref entity.GetComponent<TextField>();
        bool focused = entity.Tags.Has<TextFieldFocused>();

        // A colour dragged in from another swatch lands like a picked one.
        Color? landed = UiColorDrop.Take(entity, out Color onField) ? onField
            : UiColorDrop.Take(field.Swatch, out Color onSwatch) ? onSwatch
            : null;
        if (landed is { } color && color != field.Value)
        {
            field.Value = color;
            field.Committed = true;
            field.Dropped = true;
        }

        bool valid = SelfModulate.TryParse(text.Value, out Color typed);

        if (!focused && field.WasFocused && valid && typed != field.Value)
        {
            field.Value = typed;
            field.Committed = true;
        }
        field.WasFocused = focused;

        bool dragged = !field.Picker.IsNull && field.Picker.GetComponent<UiColorPicker>().Dragging;
        if (field.WasDragged && !dragged)
        {
            Color picked = field.Picker.GetComponent<UiColorPicker>().Color;
            if (picked != field.Value)
            {
                field.Value = picked;
                field.Committed = true;
            }
        }
        field.WasDragged = dragged;

        Color shown = dragged ? field.Picker.GetComponent<UiColorPicker>().Color
            : focused && valid ? typed
            : field.Value;
        if (!focused)
            text.Value = SelfModulate.Format(shown);
        if (!dragged && !field.Picker.IsNull)
        {
            ref UiColorPicker picker = ref field.Picker.GetComponent<UiColorPicker>();
            if (!picker.Color.IsEqualApprox(shown))
                picker.Color = shown;
        }

        UiText.SetFill(field.Swatch, shown);
    }
}
