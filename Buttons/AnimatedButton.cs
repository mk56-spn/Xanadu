// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Buttons
{
    public partial class AnimatedHoverButton : Button
    {
        private Color currentColour = Colors.Gold;
        private Color targetColour = Colors.Gold.Darkened(0.5f);
        private Color hoverColor = Colors.Gold;
        private Color normalColor = Colors.Gold.Darkened(0.5f);
        private float transitionSpeed = 3.0f; // Adjust this value to control transition speed

        private Label textLabel = new()
        {

            LabelSettings = new LabelSettings()
            {
                Font = FontSource.PLASTIC_SLANTED,
                FontSize = 50,
            }
        };
        public AnimatedHoverButton(string text)
        {
            textLabel.Text = text;
            MouseEntered += () => targetColour = hoverColor;
            MouseExited += () => targetColour = normalColor;
        }

        public override void _Ready()
        {
            AddChild(textLabel);
            textLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            CustomMinimumSize = textLabel.Size + new Vector2(20, 20);
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
            // Smoothly interpolate between current color and target color
            currentColour = currentColour.Lerp(targetColour, (float)delta * transitionSpeed);
        }

        public override void _Draw()
        {
            // Draw border with animated color
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black with{ A = 0.5f});
            DrawRect(new Rect2(Vector2.Zero, Size), currentColour, false, 2);
        }
    }
}
