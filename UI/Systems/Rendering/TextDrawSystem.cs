using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Rendering;

/// <summary>Draws dirty entity text into the entity canvas item.</summary>
public sealed class TextDrawSystem : QuerySystem<Text, Rectangle, Drawable, VisualRid>
{
    private static readonly Color TextColor = Colors.White;

    private readonly IRectCanvas _canvas;

    public TextDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref text, ref rect, ref _, ref visual, entity) =>
        {
            if (string.IsNullOrEmpty(text.Value) ||
                rect.Bounds.Size.X <= 0 ||
                rect.Bounds.Size.Y <= 0)
            {
                return;
            }

            if (entity.HasComponent<TextStyle>())
            {
                TextStyle style = entity.GetComponent<TextStyle>();
                TextAlign align = entity.HasComponent<TextAlignment>()
                    ? entity.GetComponent<TextAlignment>().Align
                    : TextAlign.Center;

                _canvas.DrawTextWeighted(
                    visual.Value,
                    rect.Bounds,
                    text.Value,
                    style.Color,
                    style.FontSize,
                    align,
                    style.Bold);
            }
            else
            {
                _canvas.DrawText(visual.Value, rect.Bounds, text.Value, TextColor);
            }

            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }
}
