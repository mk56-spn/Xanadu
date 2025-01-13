// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;
using XanaduProject.ECSComponents.Animation;

namespace XanaduProject.Composer.TrackVisualiser
{
    public partial class ColorTrackVisualizer : TrackVisualiser
    {
       private ColorKeyFrame nearestFrame() => Entity.GetComponent<ColorTrack>().KeyFrames
           .MinBy<ColorKeyFrame, object>(x => Mathf.Abs(GetLocalMousePosition().X - x.Time* 200 - 20));

       public override void _GuiInput(InputEvent @event)
       {
           QueueRedraw();

           var frames = Entity.GetComponent<ColorTrack>().KeyFrames;

           if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
               SelectedIndex = Array.IndexOf(frames,nearestFrame());

           if (@event is not InputEventMouseMotion mouseMotion || !Input.IsMouseButtonPressed(MouseButton.Left)) return;

           frames[SelectedIndex] = new ColorKeyFrame((GetLocalMousePosition().X -20) / 200F,frames[SelectedIndex].Color);

           var v = frames[SelectedIndex];
           Array.Sort(frames, (x, y) => x.Time.CompareTo(y.Time));
           SelectedIndex = Array.IndexOf(frames, v);
       }

       public override void _Input(InputEvent @event)
       {
           if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Right }) return;

           SelectedIndex = -1;
           QueueRedraw();
       }


       public override void _Draw()
        {
            int offset = 20;

            var keyFrames = Entity.GetComponent<ColorTrack>().KeyFrames;

            if ( keyFrames.Length == 0 ) return;

            DrawSetTransform(new Vector2(offset, Size.Y / 2f));

            DrawString(ThemeDB.GetDefaultTheme().DefaultFont, Vector2.Zero, SelectedIndex.ToString());

            if (SelectedIndex != -1)
                DrawRect(new Rect2(new Vector2(keyFrames[SelectedIndex].Time * 200, 0) -new Vector2(8,8), new Vector2(16, 16)),  Colors.Yellow);

            DrawPolylineColors(
                keyFrames.Select(c => new Vector2(c.Time * 200, 0))
                    .Prepend(new Vector2(keyFrames[0].Time, 0)).ToArray(),
                keyFrames.Select(c => c.Color.Darkened(.5f)).Prepend(keyFrames[0].Color).ToArray());

            var mouse = GetLocalMousePosition();
            var nearest = nearestFrame();

            var rectSize = new Vector2(10, 10);

            for (int index = 0; index < keyFrames.Length; index++)
            {
                var frame = keyFrames[index];
                DrawSetTransform(new Vector2(offset + 200 * frame.Time, Size.Y / 2f));

                DrawString(ThemeDB.FallbackFont, Vector2.Zero, index.ToString());
                DrawRect(new Rect2(-rectSize / 2, rectSize), frame.Color);

                if (Math.Abs(nearest.Time - frame.Time) < 0.01 && !Exited)
                    DrawRect(new Rect2(-rectSize / 2, rectSize), Colors.White, false, 2);
            }

            Exited = false;
        }
    }
}
