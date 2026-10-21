using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;

namespace Writer.Systems.Rendering;

/// <summary>Draws dirty linear-gradient rectangles in thirty-two bands.</summary>
public sealed class GradientDrawSystem : QuerySystem<Rectangle, Drawable, VisualRid, LinearGradient>
{
    private const int Bands = 32;

    private readonly IRectCanvas _canvas;

    public GradientDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref rect, ref _, ref visual, ref gradient, entity) =>
        {
            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            _canvas.Clear(visual.Value);

            for (int i = 0; i < Bands; i++)
            {
                float t = i / (float)(Bands - 1);
                Color color = gradient.Start.Lerp(gradient.End, t);
                Rect2 band;

                if (gradient.Horizontal)
                {
                    float width = rect.Bounds.Size.X / Bands;
                    band = new Rect2(
                        rect.Bounds.Position.X + width * i,
                        rect.Bounds.Position.Y,
                        width + 1,
                        rect.Bounds.Size.Y);
                }
                else
                {
                    float height = rect.Bounds.Size.Y / Bands;
                    band = new Rect2(
                        rect.Bounds.Position.X,
                        rect.Bounds.Position.Y + height * i,
                        rect.Bounds.Size.X,
                        height + 1);
                }

                _canvas.DrawRect(visual.Value, band, color);
            }

            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }
}
