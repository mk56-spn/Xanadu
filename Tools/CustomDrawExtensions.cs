using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace XanaduProject.Tools
{
    [Flags]
    public enum Sides
    {
            Top = 1,
            Right = 2,
            Bottom = 4,
            Left = 8,
            All = Top | Right | Bottom | Left,
            BottomLeft = Bottom | Left,
            BottomRight = Bottom | Right,
            TopLeft = Top | Left,
            TopRight = Top | Right,
    }

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
            bool antialiased = true
            , Texture2D? texture = null)
        {
            var insetPoints = generatePoints(rect, inset, new Bevel { BevelRadius = 0 });
            var beveledPoints = generatePoints(rect, new Inset(), bevel);

            var finalPolygons = Geometry2D.IntersectPolygons(insetPoints, beveledPoints);

            foreach (var points in finalPolygons)
            {
                if (points.Length < 3) continue;


                if (style.Fill)
                {
                    var uvs = points.Select(p => (p - rect.Position) / rect.Size).ToArray();
                    var fillColors = Enumerable.Repeat(style.FillColor, points.Length).ToArray();
                    node.DrawPolygon(points, fillColors, uvs, texture ?? null);;
                }

                if (style.Outline)
                {
                    node.DrawPolyline(points.Append(points[0]).ToArray(), style.OutlineColor, style.OutlineWidth, antialiased);
                }
            }
        }

        private static Vector2[] generatePoints(Rect2 rect, Inset inset, Bevel bevel)
        {
            var allPoints = new List<Vector2>();
            var size = rect.Size;
            var pos = rect.Position;

            float bevelRadius = Mathf.Max(0f, bevel.BevelRadius);
            bevelRadius = Mathf.Min(bevelRadius, Mathf.Min(size.X / 2f, size.Y / 2f));

            float tlbr = (bevel.BevelSides & Sides.TopLeft) == Sides.TopLeft ? bevelRadius : 0f;
            float trbr = (bevel.BevelSides & Sides.TopRight) == Sides.TopRight ? bevelRadius : 0f;
            float blbr = (bevel.BevelSides & Sides.BottomLeft) == Sides.BottomLeft ? bevelRadius : 0f;
            float brbr = (bevel.BevelSides & Sides.BottomRight) == Sides.BottomRight ? bevelRadius : 0f;

            float maxInsetLengthHTop = size.X - (tlbr + trbr);
            float maxInsetLengthHBottom = size.X - (blbr + brbr);
            float maxInsetLengthVRight = size.Y - (trbr + brbr);
            float maxInsetLengthVLeft = size.Y - (tlbr + blbr);

            var curve = new Curve2D();

            var pLt = new Vector2(pos.X, pos.Y + tlbr);
            var pTl = new Vector2(pos.X + tlbr, pos.Y);
            var pTr = new Vector2(pos.X + size.X - trbr, pos.Y);
            var pRt = new Vector2(pos.X + size.X, pos.Y + trbr);
            var pRb = new Vector2(pos.X + size.X, pos.Y + size.Y - brbr);
            var pBr = new Vector2(pos.X + size.X - brbr, pos.Y + size.Y);
            var pBl = new Vector2(pos.X + blbr, pos.Y + size.Y);
            var pLb = new Vector2(pos.X, pos.Y + size.Y - blbr);

            // Top Left Corner
            allPoints.Add(pLt);
            if (tlbr > 0) allPoints.Add(pTl);

            // Top Side
            if (inset.InsetSides.HasFlag(Sides.Top) && inset.OuterLength > 0 && inset.OuterLength <= maxInsetLengthHTop && inset.OuterLength >= inset.InnerLength)
            {
                float straightHLength = (maxInsetLengthHTop - inset.OuterLength) / 2f;
                var insetStartPoint = new Vector2(pos.X + tlbr + straightHLength, pos.Y);
                allPoints.Add(insetStartPoint);

                if (inset.InnerLength < inset.OuterLength)
                {
                    float slopeLength = (inset.OuterLength - inset.InnerLength) / 2f;
                    var slope1End = new Vector2(insetStartPoint.X + slopeLength, pos.Y + inset.InsetDepth);
                    var flatEnd = new Vector2(slope1End.X + inset.InnerLength, slope1End.Y);
                    var slope2End = new Vector2(flatEnd.X + slopeLength, pos.Y);

                    var controlOut = new Vector2(slopeLength * inset.InsetCurveTension, 0);
                    var controlIn = new Vector2(-slopeLength * inset.InsetCurveTension, 0);

                    curve.ClearPoints();
                    curve.AddPoint(insetStartPoint, Vector2.Zero, controlOut);
                    curve.AddPoint(slope1End, controlIn, Vector2.Zero);
                    allPoints.AddRange(curve.Tessellate().Skip(1));

                    if (inset.InnerLength > 0) allPoints.Add(flatEnd);

                    curve.ClearPoints();
                    curve.AddPoint(flatEnd, Vector2.Zero, controlOut);
                    curve.AddPoint(slope2End, controlIn, Vector2.Zero);
                    allPoints.AddRange(curve.Tessellate().Skip(1));
                }
                else
                {
                    allPoints.Add(new Vector2(insetStartPoint.X, insetStartPoint.Y + inset.InsetDepth));
                    allPoints.Add(new Vector2(insetStartPoint.X + inset.OuterLength, insetStartPoint.Y + inset.InsetDepth));
                    allPoints.Add(new Vector2(insetStartPoint.X + inset.OuterLength, insetStartPoint.Y));
                }
            }

            // Top Right Corner
            allPoints.Add(pTr);
            if (trbr > 0) allPoints.Add(pRt);

            // Right Side
            if (inset.InsetSides.HasFlag(Sides.Right) && inset.OuterLength > 0 && inset.OuterLength <= maxInsetLengthVRight && inset.OuterLength >= inset.InnerLength)
            {
                float straightVLength = (maxInsetLengthVRight - inset.OuterLength) / 2f;
                var insetStartPoint = new Vector2(pos.X + size.X, pos.Y + trbr + straightVLength);
                allPoints.Add(insetStartPoint);

                if (inset.InnerLength < inset.OuterLength)
                {
                    float slopeLength = (inset.OuterLength - inset.InnerLength) / 2f;
                    var slope1End = new Vector2(pos.X + size.X - inset.InsetDepth, insetStartPoint.Y + slopeLength);
                    var flatEnd = new Vector2(slope1End.X, slope1End.Y + inset.InnerLength);
                    var slope2End = new Vector2(pos.X + size.X, flatEnd.Y + slopeLength);

                    var controlOut = new Vector2(0, slopeLength * inset.InsetCurveTension);
                    var controlIn = new Vector2(0, -slopeLength * inset.InsetCurveTension);

                    curve.ClearPoints();
                    curve.AddPoint(insetStartPoint, Vector2.Zero, controlOut);
                    curve.AddPoint(slope1End, controlIn, Vector2.Zero);
                    allPoints.AddRange(curve.Tessellate().Skip(1));

                    if (inset.InnerLength > 0) allPoints.Add(flatEnd);

                    curve.ClearPoints();
                    curve.AddPoint(flatEnd, Vector2.Zero, controlOut);
                    curve.AddPoint(slope2End, controlIn, Vector2.Zero);
                    allPoints.AddRange(curve.Tessellate().Skip(1));
                }
                else
                {
                    allPoints.Add(new Vector2(insetStartPoint.X - inset.InsetDepth, insetStartPoint.Y));
                    allPoints.Add(new Vector2(insetStartPoint.X - inset.InsetDepth, insetStartPoint.Y + inset.OuterLength));
                    allPoints.Add(new Vector2(insetStartPoint.X, insetStartPoint.Y + inset.OuterLength));
                }
            }

            // Bottom Right Corner
            allPoints.Add(pRb);
            if (brbr > 0) allPoints.Add(pBr);

            // Bottom Side
            if (inset.InsetSides.HasFlag(Sides.Bottom) && inset.OuterLength > 0 && inset.OuterLength <= maxInsetLengthHBottom && inset.OuterLength >= inset.InnerLength)
            {
                float straightHLength = (maxInsetLengthHBottom - inset.OuterLength) / 2f;
                var insetStartPoint = new Vector2(pos.X + size.X - brbr - straightHLength, pos.Y + size.Y);
                allPoints.Add(insetStartPoint);

                if (inset.InnerLength < inset.OuterLength)
                {
                    float slopeLength = (inset.OuterLength - inset.InnerLength) / 2f;
                    var slope1End = new Vector2(insetStartPoint.X - slopeLength, pos.Y + size.Y - inset.InsetDepth);
                    var flatEnd = new Vector2(slope1End.X - inset.InnerLength, slope1End.Y);
                    var slope2End = new Vector2(flatEnd.X - slopeLength, pos.Y + size.Y);

                    var controlOut = new Vector2(-slopeLength * inset.InsetCurveTension, 0);
                    var controlIn = new Vector2(slopeLength * inset.InsetCurveTension, 0);

                    curve.ClearPoints();
                    curve.AddPoint(insetStartPoint, Vector2.Zero, controlOut);
                    curve.AddPoint(slope1End, controlIn, Vector2.Zero);
                    allPoints.AddRange(curve.Tessellate().Skip(1));

                    if (inset.InnerLength > 0) allPoints.Add(flatEnd);

                    curve.ClearPoints();
                    curve.AddPoint(flatEnd, Vector2.Zero, controlOut);
                    curve.AddPoint(slope2End, controlIn, Vector2.Zero);
                    allPoints.AddRange(curve.Tessellate().Skip(1));
                }
                else
                {
                    allPoints.Add(new Vector2(insetStartPoint.X, insetStartPoint.Y - inset.InsetDepth));
                    allPoints.Add(new Vector2(insetStartPoint.X - inset.OuterLength, insetStartPoint.Y - inset.InsetDepth));
                    allPoints.Add(new Vector2(insetStartPoint.X - inset.OuterLength, insetStartPoint.Y));
                }
            }

            // Bottom Left Corner
            allPoints.Add(pBl);
            if (blbr > 0) allPoints.Add(pLb);

            // Left Side
            if (inset.InsetSides.HasFlag(Sides.Left) && inset.OuterLength > 0 && inset.OuterLength <= maxInsetLengthVLeft && inset.OuterLength >= inset.InnerLength)
            {
                float straightVLength = (maxInsetLengthVLeft - inset.OuterLength) / 2f;
                var insetStartPoint = new Vector2(pos.X, pos.Y + size.Y - blbr - straightVLength);
                allPoints.Add(insetStartPoint);

                if (inset.InnerLength < inset.OuterLength)
                {
                    float slopeLength = (inset.OuterLength - inset.InnerLength) / 2f;
                    var slope1End = new Vector2(pos.X + inset.InsetDepth, insetStartPoint.Y - slopeLength);
                    var flatEnd = new Vector2(slope1End.X, slope1End.Y - inset.InnerLength);
                    var slope2End = new Vector2(pos.X, flatEnd.Y - slopeLength);

                    var controlOut = new Vector2(0, -slopeLength * inset.InsetCurveTension);
                    var controlIn = new Vector2(0, slopeLength * inset.InsetCurveTension);

                    curve.ClearPoints();
                    curve.AddPoint(insetStartPoint, Vector2.Zero, controlOut);
                    curve.AddPoint(slope1End, controlIn, Vector2.Zero);
                    allPoints.AddRange(curve.Tessellate().Skip(1));

                    if (inset.InnerLength > 0) allPoints.Add(flatEnd);

                    curve.ClearPoints();
                    curve.AddPoint(flatEnd, Vector2.Zero, controlOut);
                    curve.AddPoint(slope2End, controlIn, Vector2.Zero);
                    allPoints.AddRange(curve.Tessellate().Skip(1));
                }
                else
                {
                    allPoints.Add(new Vector2(insetStartPoint.X + inset.InsetDepth, insetStartPoint.Y));
                    allPoints.Add(new Vector2(insetStartPoint.X + inset.InsetDepth, insetStartPoint.Y - inset.OuterLength));
                    allPoints.Add(new Vector2(insetStartPoint.X, insetStartPoint.Y - inset.OuterLength));
                }
            }

            allPoints.Add(pLt);

            return allPoints.ToArray();
        }

        #endregion


    }

    public struct Bevel
    {
        public float BevelRadius { get; set; }
        public Sides BevelSides { get; set; }
    }
    public struct Inset()
    {
        public float InsetDepth = 0;
        public float OuterLength = 0;
        public float InnerLength = 0;
        public Sides InsetSides = Sides.All;
        public float InsetCurveTension = 0.5f;
    }

    public struct ShapeStyle()
    {
        public bool Fill = true;
        public Color FillColor = Colors.White;
        public bool Outline = false;
        public Color OutlineColor = Colors.White;
        public int OutlineWidth = 1;
    }
}
