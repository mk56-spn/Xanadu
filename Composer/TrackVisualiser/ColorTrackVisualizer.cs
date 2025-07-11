// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.ECSComponents.Animation2;
using ZLinq;
using static XanaduProject.Composer.AnimationTracksManager;


namespace XanaduProject.Composer.TrackVisualiser
{
    public partial class ColorTrackVisualizer(Container editContainer) : TrackVisualiser<Color>
    {
        protected override ref Color[] Values() => ref Entity.GetComponent<ColorArrayEcs>().Colors;
        protected override Color DefaultValue() => Colors.White;
        protected override void KeyFramePopup()
        {
            if (SelectedIndex == -1 ) return;
            ColorPickerButton colorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(50,50) };
            colorPicker.Color = Values()[SelectedIndex];
            colorPicker.ColorChanged+= c =>
            {
                Values()[SelectedIndex] = c;
                QueueRedraw();
            };

            foreach (var child in    editContainer.GetChildren())
            {
                child.QueueFree();
            }
            editContainer.AddChild(colorPicker);
        }

        public override void _Draw()
        {
            base._Draw();

            var colours = Entity.GetComponent<ColorArrayEcs>().Colors;
            float[] points = Entity.GetComponent<FloatArrayEcs>().Points;

            if ( points.Length == 0 ) return;

            DrawSetTransform(new Vector2(OFFSET, Size.Y / 2f));

            if (points.Length > 1)
              DrawPolylineColors(
                points.AsValueEnumerable().Select(c => new Vector2(c * SPACING, 0)).ToArray(),
                colours, width: 3);

            var rectSize = new Vector2(15, 15);

            if (SelectedIndex != -1)
                DrawRect(new Rect2(new Vector2(points[SelectedIndex] * SPACING, 0) - rectSize * 0.6f, rectSize * 1.2f),  Colors.Yellow);

            DrawString(font: ThemeDB.FallbackFont, new Vector2( - 25, 7), Index.ToString());
            for (int index = 0; index < points.Length; index++)
            {
                DrawSetTransform(new Vector2(OFFSET + SPACING * points[index], Size.Y / 2f));
                DrawRect(new Rect2(-rectSize / 2, rectSize), colours[index]);
            }

            Exited = false;
        }
    }
}
