using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>Redraws every bar graph each frame, since its values change continuously.</summary>
public sealed class BarGraphDrawSystem : QuerySystem<UiBarGraph, Drawable, VisualItem>
{
    private const float CapHeight = 3;
    private const float MinHeight = 2;

    private readonly IRectCanvas _canvas;

    public BarGraphDrawSystem(IRectCanvas canvas) => _canvas = canvas;

    protected override void OnUpdate()
    {
        Query.Each(new DrawBars(_canvas));
    }

    /// <summary>The line's height at <paramref name="x"/>, clamped to its ends.</summary>
    internal static float AnchorY(Vector2[] anchor, float x)
    {
        if (x <= anchor[0].X)
            return anchor[0].Y;

        for (int i = 1; i < anchor.Length; i++)
        {
            if (x > anchor[i].X)
                continue;

            float span = anchor[i].X - anchor[i - 1].X;
            float t = span <= 0 ? 1 : (x - anchor[i - 1].X) / span;
            return float.Lerp(anchor[i - 1].Y, anchor[i].Y, t);
        }

        return anchor[^1].Y;
    }

    private readonly record struct DrawBars(IRectCanvas Canvas) : IEach<UiBarGraph, Drawable, VisualItem>
    {
        public void Execute(ref UiBarGraph graph, ref Drawable _, ref VisualItem visual)
        {
            Canvas.Clear(visual.Value);
            Vector2[] anchor = graph.Anchor;
            int count = graph.Values.Length;
            if (anchor.Length < 2 || count == 0)
                return;

            float left = anchor[0].X + graph.BarWidth / 2;
            float right = anchor[^1].X - graph.BarWidth / 2;
            for (int i = 0; i < count; i++)
            {
                float x = count == 1 ? left : left + (right - left) * i / (count - 1);
                float barLeft = x - graph.BarWidth / 2;
                float barRight = x + graph.BarWidth / 2;

                // The top edge follows the line, so a bar on a slope sits flush
                // against it instead of its corners poking through.
                float topLeft = AnchorY(anchor, barLeft);
                float topRight = AnchorY(anchor, barRight);
                float height = MinHeight +
                               Math.Clamp(graph.Values[i], 0, 1) * (graph.MaxHeight - MinHeight);
                float bottom = Math.Max(
                    AnchorY(anchor, x) + height,
                    Math.Max(topLeft, topRight) + MinHeight);

                if (topLeft == topRight)
                {
                    Canvas.DrawRect(
                        visual.Value,
                        new Rect2(barLeft, topLeft, graph.BarWidth, bottom - topLeft),
                        graph.Color);
                }
                else
                {
                    Canvas.DrawPolygon(
                        visual.Value,
                        [
                            new Vector2(barLeft, topLeft),
                            new Vector2(barRight, topRight),
                            new Vector2(barRight, bottom),
                            new Vector2(barLeft, bottom),
                        ],
                        graph.Color);
                }

                Canvas.DrawRect(
                    visual.Value,
                    new Rect2(barLeft, bottom - CapHeight, graph.BarWidth, CapHeight),
                    graph.Cap);
            }
        }
    }
}
