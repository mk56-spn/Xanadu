using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>Applies typed colours when a colour field loses focus, and keeps its text and swatch current.</summary>
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
        bool valid = SelfModulate.TryParse(text.Value, out Color typed);

        if (!focused && field.WasFocused && valid && typed != field.Value)
        {
            field.Value = typed;
            field.Committed = true;
        }
        field.WasFocused = focused;

        if (!focused)
            text.Value = SelfModulate.Format(field.Value);

        Color shown = focused && valid ? typed : field.Value;
        ref FillColor fill = ref field.Swatch.GetComponent<FillColor>();
        if (fill.Value != shown)
        {
            fill.Value = shown;
            field.Swatch.AddTag<Dirty>();
        }
    }
}
