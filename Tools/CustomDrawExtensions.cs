using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace XanaduProject.Tools
{
    public static class CustomDrawExtensions
    {
        private static readonly GradientTexture2D circular_glow_texture;

        static CustomDrawExtensions()
        {
            var gradient = new Gradient { InterpolationMode = Gradient.InterpolationModeEnum.Cubic };
            gradient.Colors = [Colors.White, new Color(1, 1, 1, 0)];

            circular_glow_texture = new GradientTexture2D
            {
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(0.5f,1f),
                Gradient = gradient,
                Fill = GradientTexture2D.FillEnum.Radial
            };
        }

        #region DrawShapes

        public static void DrawCircularGlow(this CanvasItem node, Vector2 center, float radius, Color color)
        {
            var rect = new Rect2(center - new Vector2(radius, radius), new Vector2(radius, radius) * 2);
            node.DrawTextureRect(circular_glow_texture, rect, false, color);
        }

        public static void DrawTransitionLine(
            this CanvasItem node,
            float totalWidth,
            float startHeight,
            float endHeight,
            float curveLength,
            Color color,
            float curveCenterBias = 0.5f,
            float width = 1.0f,
            bool antialiased = true,
            bool fill = false,
            Color? fillColor = null,
            bool fillAbove = false)
        {
            if (totalWidth < curveLength) return;

            curveCenterBias = Mathf.Clamp(curveCenterBias, 0f, 1f);
            float totalStraightLength = totalWidth - curveLength;
            float straightSegmentA = totalStraightLength * curveCenterBias;

            var startPoint = new Vector2(0, startHeight);
            var curveStartPoint = new Vector2(straightSegmentA, startHeight);
            var curveEndPoint = new Vector2(straightSegmentA + curveLength, endHeight);
            var endPoint = new Vector2(totalWidth, endHeight);

            var curve = new Curve2D();
            Vector2 controlPointOffset = new Vector2(curveLength / 2.0f, 0);
            curve.AddPoint(curveStartPoint, -controlPointOffset, controlPointOffset);
            curve.AddPoint(curveEndPoint, -controlPointOffset, controlPointOffset);

            var allPoints = new List<Vector2> { startPoint, curveStartPoint };
            allPoints.AddRange(curve.Tessellate());
            allPoints.Add(curveEndPoint);
            allPoints.Add(endPoint);

            if (fill)
            {
                var polygonPoints = new List<Vector2>(allPoints);
                Color finalFillColor = fillColor ?? Colors.Black;

                if (fillAbove)
                {
                    polygonPoints.Add(new Vector2(totalWidth, 0));
                    polygonPoints.Add(new Vector2(0, 0));
                }
                else
                {
                    float bottomY = node.GetViewportRect().Size.Y;
                    polygonPoints.Add(new Vector2(totalWidth, bottomY));
                    polygonPoints.Add(new Vector2(0, bottomY));
                }

                var colors = Enumerable.Repeat(finalFillColor, polygonPoints.Count).ToArray();
                node.DrawPolygon(polygonPoints.ToArray(), colors);
            }
            node.DrawPolyline(allPoints.ToArray(), color, width, antialiased);
        }

        public static void DrawSquareWithInsets(
            this CanvasItem node,
            Rect2 rect,
            Inset inset,
            Bevel bevel,
            ShapeStyle style,
            bool antialiased = true,
            Texture2D? texture = null)
        {
            var finalPolygons = DrawingHelpers.GenerateInsetBeveledSquare(rect, inset, bevel);

            foreach (var points in finalPolygons)
            {
                if (points.Length < 3) continue;

                if (style.Fill)
                {
                    var uvs = points.Select(p => (p - rect.Position) / rect.Size).ToArray();
                    var fillColors = Enumerable.Repeat(style.FillColor, points.Length).ToArray();
                    node.DrawPolygon(points, fillColors, uvs, texture ?? null);
                }

                if (style.Outline)
                {
                    node.DrawPolyline(points.Append(points[0]).ToArray(), style.OutlineColor, style.OutlineWidth, antialiased);
                }
            }
        }

        #endregion
    }
}
