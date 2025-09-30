// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Animation2;

namespace XanaduProject.Stage.Masters.Composer.TrackVisualiser
{
    public abstract partial class TrackVisualiser<T> : Panel
    {
        public float Spacing = AnimationTracksManager.SPACING;

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
            return (GetLocalMousePosition().X - OFFSET) / Spacing;
        }

        public override void _GuiInput(InputEvent @event)
        {
            QueueRedraw();

            if (@event is InputEventMouseMotion)
            {
                if (!Input.IsMouseButtonPressed(MouseButton.Left)) return;
                if (SelectedIndex == -1) return;
                KeyframeManager<T>.UpdateKeyframePosition(ref Entity.GetComponent<FloatArrayEcs>().Points, SelectedIndex, getMouseAsSeconds());
            }

            switch (@event)
            {
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                {
                    SelectedIndex = KeyframeManager<T>.SelectNearestFrameIndex(Entity.GetComponent<FloatArrayEcs>().Points, getMouseAsSeconds());
                    if (SelectedIndex == -1)
                    {
                        KeyframeManager<T>.AddFrame(ref Values(), ref Entity.GetComponent<FloatArrayEcs>().Points, getMouseAsSeconds(), DefaultValue());
                        KeyframeManager<T>.SortKeyframes(ref Values(), ref Entity.GetComponent<FloatArrayEcs>().Points);
                    }

                    KeyFramePopup();
                    break;
                }
                case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }:
                    KeyframeManager<T>.SortKeyframes(ref Values(), ref Entity.GetComponent<FloatArrayEcs>().Points);
                    SelectedIndex = KeyframeManager<T>.SelectNearestFrameIndex(Entity.GetComponent<FloatArrayEcs>().Points, getMouseAsSeconds());
                    break;
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                {
                    if (SelectedIndex != -1)
                    {
                        KeyframeManager<T>.RemoveFrame(ref Values(), ref Entity.GetComponent<FloatArrayEcs>().Points, SelectedIndex);
                        SelectedIndex = -1;
                    }

                    break;
                }
            }
        }

        protected abstract void KeyFramePopup();

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
