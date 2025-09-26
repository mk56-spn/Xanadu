// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.ECSComponents.Animation2;

namespace XanaduProject.Stage.Masters.Composer.TrackVisualiser
{
    public partial class FloatTrackVisualizer(Container container) : StandardTrackVisualizer<float>
    {
        protected override ref float[] Values()
        {
            return ref Entity.GetComponent<AngleArrayEcs>().Points;
        }

        protected override void KeyFramePopup()
        {
            if (SelectedIndex == -1) return;
            var colorPicker = new SpinBox();
            colorPicker.Value = Values()[SelectedIndex];
            colorPicker.ValueChanged += c =>
            {
                Values()[SelectedIndex] = (float)c;
                QueueRedraw();
            };
            colorPicker.MinValue = -30;
            colorPicker.Step = 0.1;

            foreach (var child in container.GetChildren()) child.QueueFree();
            container.AddChild(colorPicker);
        }
    }
}
