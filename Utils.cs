// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Godot;

namespace XanaduProject
{
    public static class Utils
    {
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
