// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Singleton;

namespace XanaduProject.Stage.Masters.Composer.TrackVisualiser
{
    public partial class VectorTrackVisualizer : StandardTrackVisualizer<Vector2>
    {
        private readonly Container container;


        public VectorTrackVisualizer(Container container)
        {
            this.container = container;
            Color = Colors.Green;


        }



        public override void _Process(double delta)
        {
                QueueRedraw();

        }

        protected override ref Vector2[] Values()
        {
            return ref Entity.GetComponent<VectorArrayEcs>().Points;
        }

        protected override void KeyFramePopup()
        {
            VBoxContainer v = new VBoxContainer();
            container.AddChild(v);
            if (SelectedIndex == -1) return;
            var vectorX = new SpinBox();

            vectorX.Value = Values()[SelectedIndex].X;
            vectorX.ValueChanged += c =>
            {
                Values()[SelectedIndex].X = (float)c;
                QueueRedraw();
            };
            vectorX.MinValue = -30;

            var vectorY = new SpinBox();
            vectorY.Value = Values()[SelectedIndex].Y;
            vectorY.ValueChanged += c =>
            {

                Values()[SelectedIndex].Y = (float)c;
                QueueRedraw();
            };
            vectorY.MinValue = -30;

            foreach (var child in container.GetChildren()) child.QueueFree();

            vectorX.CustomMinimumSize = new Vector2(50, 50);
            vectorY.CustomMinimumSize = new Vector2(50, 50);
            container.AddChild(vectorX);
            v.AddChild(vectorY);
        }
    }
}
