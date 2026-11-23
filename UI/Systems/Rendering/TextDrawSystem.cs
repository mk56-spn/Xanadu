using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>Draws dirty entity text into the entity canvas item, cut to its box when it has <see cref="TextFit"/>.</summary>
public sealed class TextDrawSystem : QuerySystem<Text, Rectangle, Drawable, VisualItem>
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
        Query.EachEntity(new DrawTexts(_canvas, Query.Store, CommandBuffer));
    }

    private readonly record struct DrawTexts(IRectCanvas Canvas, EntityStore Store, CommandBuffer Buffer) : IEachEntity<Text, Rectangle, Drawable, VisualItem>
    {
        public void Execute(ref Text text, ref Rectangle rect, ref Drawable _, ref VisualItem visual, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (string.IsNullOrEmpty(text.Value) ||
                rect.Bounds.Size.X <= 0 ||
                rect.Bounds.Size.Y <= 0)
            {
                return;
            }

            if (entity.HasComponent<TextStyle>())
            {
                TextStyle style = entity.GetComponent<TextStyle>();
                if (entity.Tags.Has<TextWrap>())
                {
                    DrawWrapped(entity, text.Value, rect.Bounds, style, ref visual);
                    Buffer.RemoveTag<Dirty>(id);
                    return;
                }

                string shown = entity.Tags.Has<TextFit>()
                    ? UiTextFit.Cut(text.Value, style.FontSize, rect.Bounds.Size.X)
                    : text.Value;
                TextAlign align = entity.HasComponent<TextAlignment>()
                    ? entity.GetComponent<TextAlignment>().Align
                    : TextAlign.Center;

                if (entity.Tags.Has<TextMono>())
                {
                    Canvas.DrawTextMono(
                        visual.Value,
                        rect.Bounds,
                        shown,
                        style.Color,
                        style.FontSize,
                        align);
                }
                else if (entity.HasComponent<Glow>())
                {
                    Canvas.DrawTextGlow(
                        visual.Value,
                        rect.Bounds,
                        shown,
                        style.Color,
                        style.FontSize,
                        align,
                        style.Bold,
                        entity.GetComponent<Glow>(),
                        entity.TryGetComponent(out TextOutline glowOutline)
                            ? glowOutline
                            : null);
                }
                else
                {
                    if (entity.TryGetComponent(out TextOutline outline))
                    {
                        Canvas.DrawTextOutline(
                            visual.Value,
                            rect.Bounds,
                            shown,
                            outline.Color,
                            style.FontSize,
                            align,
                            style.Bold,
                            outline.Size);
                    }

                    Canvas.DrawTextWeighted(
                        visual.Value,
                        rect.Bounds,
                        shown,
                        style.Color,
                        style.FontSize,
                        align,
                        style.Bold);
                }
            }
            else
            {
                Canvas.DrawText(visual.Value, rect.Bounds, text.Value, TextColor);
            }

            Buffer.RemoveTag<Dirty>(id);
        }

        /// <summary>One line under another from the top of the box, each aligned within the box's width.</summary>
        private void DrawWrapped(Entity entity, string value, Rect2 bounds, TextStyle style, ref VisualItem visual)
        {
            TextAlign align = entity.TryGetComponent(out TextAlignment alignment) ? alignment.Align : TextAlign.Left;
            float lineHeight = UiTextFit.LineHeight(style.FontSize);
            float y = bounds.Position.Y;
            foreach (string line in UiTextFit.WrapWords(value, style.FontSize, bounds.Size.X))
            {
                var box = new Rect2(bounds.Position.X, y, bounds.Size.X, lineHeight);
                if (entity.Tags.Has<TextMono>())
                    Canvas.DrawTextMono(visual.Value, box, line, style.Color, style.FontSize, align);
                else
                    Canvas.DrawTextWeighted(visual.Value, box, line, style.Color, style.FontSize, align, style.Bold);
                y += lineHeight;
            }
        }
    }
}
