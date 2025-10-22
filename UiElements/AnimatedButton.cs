// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;

namespace XanaduProject.UiElements
{
    public partial class AnimatedHoverButton : Button
    {
        protected Color CurrentColour = Colors.Gold;
        protected Color TargetColour = Colors.Gold.Darkened(0.5f);
        public Color MainColour = Colors.Gold;
        protected readonly Color DisabledColor = Colors.Gray;
        protected float TransitionSpeed = 3.0f; // Adjust this value to control transition speed

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

        public AnimatedHoverButton(string text, int fontSize = 50, Font? font = null,Action? pressed = null)
        {
            Pressed += () => pressed?.Invoke();
            LabelSettings = textLabel.LabelSettings;
            LabelSettings.FontSize = fontSize;
            LabelSettings.Font = font ?? FontSource.PLASTIC_SLANTED;
            textLabel.Text = text;

            // Disable default theming
            AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
            AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
            AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());
            AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
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
                TargetColour = DisabledColor;
            }
            else
            {
                TargetColour = IsHovered() ? MainColour : MainColour.Darkened(0.5f);
            }

            QueueRedraw();
            // Smoothly interpolate between current color and target color
            CurrentColour = CurrentColour.Lerp(TargetColour, (float)delta * TransitionSpeed);
        }

        public override void _Draw()
        {
            // Draw border with animated color
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black with { A = 0.5f });
            DrawRect(new Rect2(Vector2.Zero, Size), CurrentColour, false, 2);
        }
    }
}
