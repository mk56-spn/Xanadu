// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Buttons
{
    public partial class AnimatedHoverButton : TextButton
    {
        private Color currentColour = Colors.White;
        private Color targetColour = Colors.White;
        private Color hoverColor = Colors.Red;
        private Color normalColor = Colors.White;
        private float transitionSpeed = 5.0f; // Adjust this value to control transition speed

        public AnimatedHoverButton(string text) : base(text)
        {
            MouseEntered += () => targetColour = hoverColor;
            MouseExited += () => targetColour = normalColor;
        }

        public override void _Process(double delta)
        {
            // Smoothly interpolate between current color and target color
            currentColour = currentColour.Lerp(targetColour, (float)delta * transitionSpeed);
            base._Process(delta);
        }

        public override void _Draw()
        {
            // Draw border with animated color
            DrawRect(new Rect2(Vector2.Zero, Size), currentColour, false, 3);
            // Call base to draw the text
            base._Draw();
        }
    }
}
