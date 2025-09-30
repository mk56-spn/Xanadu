// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Animation2;

namespace XanaduProject.Stage.Masters.Composer.TrackVisualiser
{
    public abstract partial class StandardTrackVisualizer<T> : TrackVisualiser<T>
    {

        protected Color Color = Colors.Red;
        public float TrackSpacing
        {
            get => Spacing;
            set
            {
                Spacing = value;
                QueueRedraw();
            }
        }
        public override void _Draw()
        {
            base._Draw();
            float[] points = Entity.GetComponent<FloatArrayEcs>().Points;
            if (points.Length == 0) return;
            DrawSetTransform(new Vector2(OFFSET, Size.Y / 2f));

            if (points.Length > 1)
            {
                DrawPolyline(points.Select(c => new Vector2(c * Spacing, 0)).ToArray(), Color);
            }

            foreach (float variable in points)
            {
                DrawCircle(new Vector2(variable * Spacing, 0), 5, Color);
            }
        }
    }
}
