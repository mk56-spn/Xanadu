// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.StageSelection
{
    [GlobalClass]
    public partial class StageSelectionPanel : PanelContainer
    {
        public StageInfo StageInfo { get; private set; }

        private ColorRect focusRect = new ColorRect
        {
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            Color = Colors.Transparent, CustomMinimumSize = new Vector2(0,10)
        };

        public StageSelectionPanel(StageInfo stageInfo)
        {
            FocusMode = FocusModeEnum.All;
            StageInfo = stageInfo;
            CustomMinimumSize = new Vector2(200, 200);

            var label = new Label
            {
                CustomMinimumSize = CustomMinimumSize,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,

                Text = StageInfo.Title.ToUpper()
            };

            AddChild(focusRect);
            AddChild(label);

            FocusEntered += () => CreateTween().TweenProperty(focusRect, "color", Colors.White, 0.3);
            FocusExited += () => CreateTween().TweenProperty(focusRect, "color", Colors.Transparent, 0.3);
        }
    }
}
