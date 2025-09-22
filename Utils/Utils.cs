// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Godot;
using XanaduProject.Factories;

namespace XanaduProject.Utils
{
    public static class UtilsGeneral
    {

        public static RenderRid GridCanvas(int spacing, Rid parent)
        {
            int lineCount = 32;
            var lineY = new Vector2[lineCount * 2];
            var lineX = new Vector2[lineCount * 2];


            for (int i = 0; i < lineCount; i++)
            {
                lineY[i * 2] = new Vector2(i * spacing, 0);
                lineY[i * 2 + 1] = new Vector2(i * spacing, 3000);
                lineX[i * 2] = new Vector2(0, i * spacing);
                lineX[i * 2 + 1] = new Vector2(5000, i * spacing);
            }

            return RenderRid.Create()
                .SetParent(parent)
                .SetModulate(Colors.Blue with { A = 0.2F })
                .SetZIndex(-10)
                .AddMultiline(lineX)
                .AddMultiline(lineY);
        }


        public static Rect2 GetNonNegativeRect(Vector2 start, Vector2 end, bool centered = false)
        {
            var rectSize = new Vector2(Mathf.Abs(end.X - start.X), Mathf.Abs(end.Y - start.Y));

            // After the transform we check to see if the position of the rectangle needs changing.
            var rectanglePosition = new Vector2(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));

            return new Rect2
                { Size = rectSize, Position = centered ? rectanglePosition + rectSize / 2 : rectanglePosition };
        }

        public static Rect2 PointBoundingBox(Vector2[] points)
        {
            var rectStart = Vector2.Inf;
            var rectEnd = -Vector2.Inf;

            foreach (var point in points)
            {
                rectStart.X = Mathf.Min(rectStart.X, point.X);
                rectStart.Y = Mathf.Min(rectStart.Y, point.Y);
                rectEnd.X = Mathf.Max(rectEnd.X, point.X);
                rectEnd.Y = Mathf.Max(rectEnd.Y, point.Y);
            }

            return new Rect2(rectStart, rectEnd - rectStart);
        }
    }
}
