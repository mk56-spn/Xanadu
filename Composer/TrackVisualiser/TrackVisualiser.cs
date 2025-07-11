// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.Animation2;
using ZLinq;
using static XanaduProject.Composer.AnimationTracksManager;

namespace XanaduProject.Composer.TrackVisualiser
{
    public abstract partial class TrackVisualiser<T> : Panel
    {
        public int Index = 0;
        protected abstract ref T[] Values();

        protected virtual T DefaultValue()
        {
            return default!;
        }

        public const int OFFSET = 20;

        public required Entity Entity;
        protected bool Exited = true;

        private float getMouseAsSeconds()
        {
            return (GetLocalMousePosition().X - OFFSET) / SPACING;
        }

        public override void _GuiInput(InputEvent @event)
        {
            QueueRedraw();

            if (@event is InputEventMouseMotion)
            {
                if (!Input.IsMouseButtonPressed(MouseButton.Left)) return;
                updateKeyframePosition();
            }

            switch (@event)
            {
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                {
                    selectNearestFrameIndex();
                    if (SelectedIndex == -1)
                    {
                        addFrame();
                        sortKeyframes();
                    }

                    KeyFramePopup();
                    break;
                }
                case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }:
                    sortKeyframes();
                    selectNearestFrameIndex();
                    break;
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                {
                    if (SelectedIndex != -1)
                    {
                        removeFrame();
                        SelectedIndex = -1;
                    }

                    break;
                }
            }
        }

        protected abstract void KeyFramePopup();

        private void addFrame()
        {
            ref var values = ref Values();
            ref float[] frames = ref Entity.GetComponent<FloatArrayEcs>().Points;

            frames = frames.AsValueEnumerable().Append(getMouseAsSeconds()).ToArray();
            values = values.AsValueEnumerable().Append(DefaultValue()).ToArray();
        }

        private void removeFrame()
        {
            ref var values = ref Values();
            ref float[] frames = ref Entity.GetComponent<FloatArrayEcs>().Points;

            var valuesTemp = values.ToList();
            var framesTemp = frames.ToList();

            valuesTemp.RemoveAt(SelectedIndex);
            framesTemp.RemoveAt(SelectedIndex);

            values = valuesTemp.ToArray();
            frames = framesTemp.ToArray();
        }

        private void sortKeyframes()
        {
            ref float[] frames = ref Entity.GetComponent<FloatArrayEcs>().Points;
            ref var values = ref Values();

            var combinedKeyframes = frames.AsValueEnumerable().Zip(values,
                    (frameValue, colorValue) => new { Frame = frameValue, ColorVal = colorValue })
                .ToList();

            var sortedKeyframes = combinedKeyframes.AsValueEnumerable()
                .OrderBy(keyframe => keyframe.Frame)
                .ToList();

            frames = sortedKeyframes.AsValueEnumerable().Select(keyframe => keyframe.Frame).ToArray();
            values = sortedKeyframes.AsValueEnumerable().Select(keyframe => keyframe.ColorVal).ToArray();
        }

        private void updateKeyframePosition()
        {
            if (SelectedIndex == -1) return;

            ref var frame = ref Entity.GetComponent<FloatArrayEcs>();
            frame.Points[SelectedIndex] = getMouseAsSeconds();
        }

        private void selectNearestFrameIndex()
        {
            float[] array = Entity.GetComponent<FloatArrayEcs>().Points;

            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < array.Length; i++)
            {
                float distance = Math.Abs(array[i] - getMouseAsSeconds());
                if (!(distance < nearestDistance)) continue;
                nearestDistance = distance;
                nearestIndex = i;
            }

            SelectedIndex = nearestDistance > 0.05f ? -1 : nearestIndex;
        }


        protected int SelectedIndex = -1;

        public override void _EnterTree()
        {
            base._EnterTree();
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            CustomMinimumSize = new Vector2(0, 25);

            MouseExited += () =>
            {
                QueueRedraw();
            };
        }
    }
}
