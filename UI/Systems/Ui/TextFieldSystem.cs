using Friflo.Engine.ECS;
using Godot;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Systems.Ui;

/// <summary>Projects backing text-field values into drawable ECS text.</summary>
public sealed class TextFieldSystem : QuerySystem<TextField, Text, FillColor>
{
    private static readonly Color FocusColor = new("#172554");
    private static readonly Color NormalColor = Palette.SurfaceInset;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref field, ref text, ref fill, entity) =>
        {
            Color target = entity.Tags.Has<TextFieldFocused>()
                ? FocusColor
                : NormalColor;
            if (fill.Value != target)
            {
                fill.Value = target;
                buffer.AddTag<Dirty>(entity.Id);
            }

            if (text.Value == field.Value)
                return;

            text.Value = field.Value;
            buffer.AddTag<Dirty>(entity.Id);
        });
    }
}
