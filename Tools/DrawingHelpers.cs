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

    public struct Bevel
    {
        public float BevelRadius { get; set; }
        public Sides BevelSides { get; set; }
    }

    public struct Inset
    {
        public float InsetDepth;
        public float OuterLength;
        public float InnerLength;
        public Sides InsetSides;
        public float InsetCurveTension;

        public Inset()
        {
            InsetDepth = 0;
            OuterLength = 0;
            InnerLength = 0;
            InsetSides = Sides.All;
            InsetCurveTension = 0.5f;
        }
    }

    public struct ShapeStyle
    {
        public bool Fill;
        public Color FillColor;
        public bool Outline;
        public Color OutlineColor;
        public int OutlineWidth;

        public ShapeStyle()
        {
            Fill = true;
            FillColor = Colors.White;
            Outline = false;
            OutlineColor = Colors.White;
            OutlineWidth = 1;
        }
    }

    public static class DrawingHelpers
    {
        public static Godot.Collections.Array<Vector2[]> GenerateInsetBeveledSquare(Rect2 rect, Inset inset, Bevel bevel)
        {
            var insetPoints = generatePoints(rect, inset, new Bevel { BevelRadius = 0 });
            var beveledPoints = generatePoints(rect, new Inset(), bevel);

            return Geometry2D.IntersectPolygons(insetPoints, beveledPoints);
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
    }
}
