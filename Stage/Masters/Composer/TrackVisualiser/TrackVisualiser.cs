// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.Editor.Input;
using XanaduProject.Singleton;
using XanaduProject.Tools;
using XanaduProject.UiElements;
using ZLinq;

namespace XanaduProject.Stage.Masters.Composer.TrackVisualiser
{
    public abstract partial class TrackVisualiser<T> : BaseInputHandler
    where T : notnull
    {
        public float Spacing = AnimationTracksManager.SPACING;

        public int Index = 0;

        public const int OFFSET = 20;

        public required Entity Entity;
        protected bool Exited = true;

        private float getMouseAsSeconds()
        {
            float time = (GetLocalMousePosition().X - OFFSET) / Spacing;

            if (EditorState.SnappedTracks)
            {
                return Mathf.Round(time / 0.1f) * 0.1f;
            }

            return time;
        }

        public override void _GuiInput(InputEvent @event)
        {
            base._GuiInput(@event);

            QueueRedraw();

            switch (@event)
            {
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Middle }:
                {
                    SelectedIndex =
                        KeyframeManager<T>.SelectNearestFrameIndex(Entity.GetComponent<FloatArrayEcs>().Points,
                            getMouseAsSeconds());

                    if (SelectedIndex == -1) break;

                    var popup = new PopupMenu();
                    AddChild(popup);

                    var easingTypes = Enum.GetValues<EasingType>();
                    for (var i = 0; i < easingTypes.Length; i++)
                    {
                        var type = (EasingType)easingTypes.GetValue(i);
                        popup.AddItem(type.ToString());
                        popup.SetItemAsRadioCheckable(i, true);
                        if (Entity.HasComponent<EasingArrayEcs>())
                        {
                            var easingPoints = Entity.GetComponent<EasingArrayEcs>().Points;
                            if (SelectedIndex >= 0 && SelectedIndex < easingPoints.Length)
                            {
                                popup.SetItemChecked(i, easingPoints[SelectedIndex] == type);
                            }
                        }
                    }

                    popup.IndexPressed += index =>
                    {
                        var type = (EasingType)easingTypes.GetValue(index);

                        if (!Entity.HasComponent<EasingArrayEcs>())
                        {
                            var pointCount = Entity.GetComponent<FloatArrayEcs>().Points.Length;
                            var newEasingPoints = new EasingType[pointCount];
                            for (var j = 0; j < pointCount; j++)
                            {
                                newEasingPoints[j] = EasingType.Linear;
                            }
                            Entity.AddComponent(new EasingArrayEcs { Points = newEasingPoints });
                        }

                        Entity.GetComponent<EasingArrayEcs>().Points[SelectedIndex] = type;
                        QueueRedraw();
                    };

                    popup.PopupOnParent(new Rect2I((Vector2I)GetGlobalMousePosition(), Vector2I.Zero));
                    break;
                }
            }
        }

        protected override void OnDrag(Vector2 delta)
        {
            if (SelectedIndex == -1) return;

            float time = getMouseAsSeconds();
            Entity.AddComponent(new UpdateKeyPosition() { Time = time, Index = SelectedIndex });

            float[] points = Entity.GetComponent<FloatArrayEcs>().Points;
            int newIndex = points.AsValueEnumerable().Where((_, i) => i != SelectedIndex).Count(t => t < time);

            SelectedIndex = newIndex;
        }

        protected override void HandleLeftPress(bool multiSelect)
        {
            Logger.AddLog(LogCategory.General, "Handle left press");
            SelectedIndex =
                KeyframeManager<T>.SelectNearestFrameIndex(Entity.GetComponent<FloatArrayEcs>().Points,
                    getMouseAsSeconds());
            if (SelectedIndex == -1)
            {
                Entity.AddComponent(new AddKeyFrame( getMouseAsSeconds(), default));
            }

            KeyFramePopup();
        }

        protected override void HandleLeftRelease()
        {
            // Don't recalculate SelectedIndex on release to prevent swapping when keyframes cross
            // The SelectedIndex should already be correct from the drag operation
            // SelectedIndex =
            //     KeyframeManager<T>.SelectNearestFrameIndex(Entity.GetComponent<FloatArrayEcs>().Points,
            //         getMouseAsSeconds());
        }

        protected override void OnRightClick()
        {
            if (SelectedIndex == -1) return;
            Entity.AddComponent(new RemoveKeyFrame { Index = SelectedIndex });
            SelectedIndex = -1;
        }


        protected abstract void KeyFramePopup();

        protected int SelectedIndex = -1;

        public override void _EnterTree()
        {
            base._EnterTree();
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            CustomMinimumSize = new Vector2(0, 25);

            MouseExited += QueueRedraw;
        }

        public override void _Draw()
        {
            base._Draw();

            DrawRect(new Rect2(Vector2.Zero, GetSize()),Colors.RoyalBlue.Darkened(0.3f), filled: false, width: 1);

            DrawRect(new Rect2(Vector2.Zero, GetSize()),Colors.RoyalBlue with{ A = 0.2f });

            if (!Entity.HasComponent<EasingArrayEcs>() || !Entity.HasComponent<FloatArrayEcs>()) return;

            var timePoints = Entity.GetComponent<FloatArrayEcs>().Points;
            var easingPoints = Entity.GetComponent<EasingArrayEcs>().Points;

            if (timePoints.Length != easingPoints.Length) return;

            for (var i = 0; i < timePoints.Length; i++)
            {
                if (easingPoints[i] != EasingType.Linear)
                {
                    var x = timePoints[i] * Spacing + OFFSET;
                    var position = new Vector2(x, GetSize().Y / 2);
                    DrawPrimitive(new Vector2[] { position + new Vector2(0, -5), position + new Vector2(-3, -10), position + new Vector2(3, -10) }, new Color[] { Colors.Yellow, Colors.Yellow, Colors.Yellow }, Array.Empty<Vector2>());
                }
            }
        }
    }
}
