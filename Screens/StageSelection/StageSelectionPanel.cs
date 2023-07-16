// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelectionPanel : PanelContainer
    {
        private Tween? focusTween;

        private ColorRect focusRect = new ColorRect
        {
            Color = Colors.Transparent,
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            CustomMinimumSize = new Vector2(0, 10)
        };

        public StageSelectionPanel(StageInfo stageInfo)
        {
            FocusMode = FocusModeEnum.All;
            CustomMinimumSize = new Vector2( 200, 200);

            AddChild(focusRect);
            AddChild(new Label
            {
                Text = stageInfo.Title, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            FocusEntered += () => focusVisibility(true);
            FocusExited += () => focusVisibility(false);

            void focusVisibility(bool focused)
            {
                // Invalidate any running tween to avoid problems with final color state.
                focusTween?.Kill();
                focusTween = CreateTween();
                focusTween.TweenProperty(focusRect, "color", focused ? Colors.White : Colors.Transparent,
                    focused ? 0 : 0.3);
            }
        }
    }
}
