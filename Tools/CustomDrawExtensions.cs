using System.Collections.Generic;
using System.Linq;
using Godot;

namespace XanaduProject.Tools
{
    public static class CustomDrawExtensions
    {
        /// <summary>
        /// [Extension Method] Draws a horizontal line on a CanvasItem that transitions
        /// between a start and end height using a smooth curve.
        /// </summary>
        /// <param name="node">The CanvasItem node to draw on.</param>
        /// <param name="totalWidth">The total horizontal length of the line.</param>
        /// <param name="startHeight">The starting Y position of the line.</param>
        /// <param name="endHeight">The ending Y position of the line.</param>
        /// <param name="curveLength">The horizontal length over which the curve transition occurs.</param>
        /// <param name="color">The color of the line.</param>
        /// <param name="curveCenterBias">The horizontal position of the curve (0.0=start, 0.5=center, 1.0=end).</param>
        /// <param name="width">The thickness of the line.</param>
        /// <param name="antialiased">If true, the line will be drawn with anti-aliasing.</param>
        /// <param name="fill">If true, the area above or below the line will be filled.</param>
        /// <param name="fillColor">The color to use for the fill. If null, a semi-transparent version of the line color is used.</param>
        /// <param name="fillAbove">If true, the area above the line is filled. Otherwise, the area below is filled.</param>
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
            // Ensure the curve can physically fit within the total width.
            if (totalWidth < curveLength)
            {
                return;
            }

            // Clamp the bias value to ensure it's always within the valid 0-1 range.
            curveCenterBias = Mathf.Clamp(curveCenterBias, 0f, 1f);

            // Calculate the total horizontal space available for the straight line segments.
            float totalStraightLength = totalWidth - curveLength;

            // Calculate the length of the first straight segment based on the bias.
            float straightSegmentA = totalStraightLength * curveCenterBias;

            // Define the key points for the entire line using the new biased position.
            var startPoint = new Vector2(0, startHeight);
            var curveStartPoint = new Vector2(straightSegmentA, startHeight);
            var curveEndPoint = new Vector2(straightSegmentA + curveLength, endHeight);
            var endPoint = new Vector2(totalWidth, endHeight);

            // Create the Curve2D resource to define the S-shaped transition.
            var curve = new Curve2D();

            // The horizontal control points ensure the curve begins and ends moving horizontally,
            // creating a seamless connection to the straight line segments.
            Vector2 controlPointOffset = new Vector2(curveLength / 2.0f, 0);
            curve.AddPoint(curveStartPoint, -controlPointOffset, controlPointOffset);
            curve.AddPoint(curveEndPoint, -controlPointOffset, controlPointOffset);

            // Build the final list of vertices for the polyline.
            var allPoints = new List<Vector2>
            {
                startPoint,
                curveStartPoint
            };

            allPoints.AddRange(curve.Tessellate()); // Add the points that make up the curve.
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
                else // Fill below
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
    }
}
