// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Buttons
{
    public partial class AnimatedHoverButton : Button
    {
        private Color currentColour = Colors.Gold;
        private Color targetColour = Colors.Gold.Darkened(0.5f);
        public readonly Color MainColour = Colors.Gold;
        private readonly Color disabledColor = Colors.Gray;
        private readonly float transitionSpeed = 3.0f; // Adjust this value to control transition speed

        public readonly LabelSettings LabelSettings;
        private readonly Label textLabel = new() { LabelSettings = new LabelSettings() };

        public new string Text
        {
            get => textLabel.Text;
            set
            {
                textLabel.Text = value;
                if (!IsInsideTree())
                {
                    return;
                }
                recalculateSize();
            }
        }

        public AnimatedHoverButton(string text, int fontSize = 50, Font? font = null)
        {
            LabelSettings = textLabel.LabelSettings;
            LabelSettings.FontSize = fontSize;
            LabelSettings.Font = font ?? FontSource.PLASTIC_SLANTED;
            textLabel.Text = text;
        }

        public override void _Ready()
        {
            AddChild(textLabel);
            textLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            recalculateSize();
        }

        private void recalculateSize()
        {
            CustomMinimumSize = textLabel.GetMinimumSize() + new Vector2(20, 20);
        }

        public override void _Process(double delta)
        {
            if (Disabled)
            {
                targetColour = disabledColor;
            }
            else
            {
                targetColour = IsHovered() ? MainColour : MainColour.Darkened(0.5f);
            }

            QueueRedraw();
            // Smoothly interpolate between current color and target color
            currentColour = currentColour.Lerp(targetColour, (float)delta * transitionSpeed);
        }

        public override void _Draw()
        {
            // Draw border with animated color
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black with { A = 0.5f });
            DrawRect(new Rect2(Vector2.Zero, Size), currentColour, false, 2);
        }
    }
}
