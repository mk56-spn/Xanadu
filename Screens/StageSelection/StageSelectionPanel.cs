// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelectionPanel : PanelContainer
    {
        public StageInfo Info { get; }
        private Tween? focusTween;

        private ColorRect focusRect = new()
        {
            Color = Colors.Transparent,
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            CustomMinimumSize = new Vector2(0, 10)
        };

        public StageSelectionPanel(StageInfo info)
        {
            GD.Print(info.StageName);
            SizeFlagsVertical = SizeFlags.ShrinkCenter;

            Info = info;
            FocusMode = FocusModeEnum.All;
            CustomMinimumSize = new Vector2(300, 300);

            AddChild(focusRect);
            AddChild(new Label
            {
                Text = Info.StageName,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });


            FocusEntered += focusVisibility;
            FocusExited += focusVisibility;

            void focusVisibility()
            {
                // Invalidate any running tween to avoid problems with final color state.
                focusTween?.Kill();
                focusTween = CreateTween();
                focusTween.TweenProperty(focusRect, "color", HasFocus() ? Colors.White : Colors.Transparent,
                    HasFocus() ? 0 : 0.3);
            }
        }
    }
}
