// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Buttons;

namespace XanaduProject.UiElements
{
    public partial class SelectableButton(string text, int fontSize = 50, Font? font = null)
        : AnimatedHoverButton(text, fontSize, font)
    {
        private bool isSelected;
        private readonly Color selectedColor = Colors.DodgerBlue.Lightened(0.3f);
        private readonly float selectedOffset = 10f;
        private float currentOffset = 0f;

        public Color? UnselectedColor { get; set; }


        public override void _Ready()
        {
            base._Ready();
            TransitionSpeed = 10.0f;
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
        }

        public override void _Process(double delta)
        {
            if (Disabled)
            {
                TargetColour = DisabledColor;
            }
            else if (isSelected)
            {
                TargetColour = selectedColor;
            }
            else
            {
                if (UnselectedColor.HasValue) {
                    TargetColour = IsHovered() ? UnselectedColor.Value.Lightened(0.2f) : UnselectedColor.Value;
                }
                else {
                    TargetColour = IsHovered() ? MainColour : MainColour.Darkened(0.5f);
                }
            }

            // Lerp offset
            float targetOffset = isSelected ? selectedOffset : 0f;
            currentOffset = Mathf.Lerp(currentOffset, targetOffset, (float)delta * TransitionSpeed);

            QueueRedraw();
            CurrentColour = CurrentColour.Lerp(TargetColour, (float)delta * TransitionSpeed);
        }

        public override void _Draw()
        {
            // Draw background with offset
            var rect = new Rect2(new Vector2(currentOffset, 0), Size);
            DrawRect(rect, Colors.Black with { A = 0.5f });

            // Draw border with thicker width when selected
            float borderWidth = isSelected ? 4f : 2f;
            DrawRect(rect, CurrentColour, false, borderWidth);
        }
    }
}
