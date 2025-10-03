// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Buttons
{
    public partial class AnimatedToggleButton : CheckButton
    {
        private float currentNubX;
        private Color currentBgColor;

        private readonly float transitionSpeed = 15.0f;

        private readonly Color nubColor = Colors.White;
        private readonly Color offColor = Colors.Gray.Darkened(0.5f);
        private readonly Color onColor = Colors.Gold;
        private readonly Color disabledColor = Colors.Gray;

        public AnimatedToggleButton(Vector2? size = null)
        {
            CustomMinimumSize = size ?? new Vector2(50, 30);
        }

        public override void _Ready()
        {
            // Set initial state without animation
            currentNubX = getTargetNubX();
            currentBgColor = getTargetBgColor();
        }

        public override void _Process(double delta)
        {
            var targetNubX = getTargetNubX();
            var targetBgColor = getTargetBgColor();

            // Check if an update is needed to avoid unnecessary redraws
            if (Mathf.IsEqualApprox(currentNubX, targetNubX) && currentBgColor.IsEqualApprox(targetBgColor))
            {
                return;
            }

            var t = (float)delta * transitionSpeed;
            currentNubX = Mathf.Lerp(currentNubX, targetNubX, t);
            currentBgColor = currentBgColor.Lerp(targetBgColor, t);

            QueueRedraw();
        }

        public override void _Draw()
        {
            // Background
            DrawRect(new Rect2(Vector2.Zero, Size), currentBgColor);

            // Border
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black with { A = 0.5f }, false, 2);

            // Nub
            var margin = 4f;
            var nubSize = Size.Y - margin * 2;
            var nubRect = new Rect2(currentNubX, margin, nubSize, nubSize);
            DrawRect(nubRect, nubColor);
            DrawRect(nubRect, Colors.Black with { A = 0.5f }, false, 2);
        }

        private float getTargetNubX()
        {
            var margin = 4f;
            var nubSize = Size.Y - margin * 2;
            return ButtonPressed ? Size.X - nubSize - margin : margin;
        }

        private Color getTargetBgColor()
        {
            if (Disabled)
            {
                return disabledColor;
            }

            return ButtonPressed ? onColor : offColor;
        }
    }
}
