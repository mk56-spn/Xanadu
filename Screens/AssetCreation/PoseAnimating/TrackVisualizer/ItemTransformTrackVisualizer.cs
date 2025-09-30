// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.Stage.Masters.Composer.TrackVisualiser;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating.TrackVisualizer
{
    public partial class ItemTransformTrackVisualizer : StandardTrackVisualizer<Transform2D>
    {
        public ItemTransformTrackVisualizer(Container container)
        {
            container.AddChild(this);
            Color = Colors.Orange;
        }

        public override void _Draw()
        {
            base._Draw();
            if (!Entity.TryGetComponent<ItemTransformTrackEcs>(out var track)) return;

            DrawSetTransform(new Vector2(OFFSET, Size.Y / 2f));

            // Draw lines connecting keyframes
            if (track.Keyframes.Length > 1)
            {
                var points = track.Keyframes.Select(k => new Vector2(k.Time * AnimationTracksManager.SPACING, 0)).ToArray();
                DrawPolyline(points, Color);
            }

            // Draw keyframe points
            foreach (var keyframe in track.Keyframes)
            {
                var color = keyframe.IsVisible ? Color : Colors.Gray;
                DrawCircle(new Vector2(keyframe.Time * AnimationTracksManager.SPACING, 0), 5, color);
            }
        }

        protected override ref Transform2D[] Values()
        {
            throw new System.NotImplementedException();
        }

        protected override void KeyFramePopup()
        {
            throw new System.NotImplementedException();
        }
    }
}
