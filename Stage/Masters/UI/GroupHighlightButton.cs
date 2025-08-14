// Copyright (c) mk56_spn
// Distributed under the GPL-2.0 licence – see repository root for details.

using Godot;

namespace XanaduProject.Stage.Masters.UI
{
    /// <summary>
    /// Button that looks faded unless hovered and it is not the selected
    /// member of the supplied <see cref="ButtonGroup"/>.
    /// </summary>
    public partial class GroupHighlightButton : Button
    {
        // How transparent the button is when idle (0-1 range).
        private const float fade_alpha = 0.45f;

        // Exposed in the inspector so you can drag-&-drop a group,
        // OR you can supply one with the constructor below.
        private ButtonGroup group;

        public GroupHighlightButton(ButtonGroup group)
        {
            this.group = group;
            MouseEntered += updateVisual;
            MouseExited  += updateVisual;

            // First visual update:
            updateVisual();
        }

        private void updateVisual()
        {
            bool isHovered   = GetViewport().GuiGetHoveredControl() == this;
            bool isSelected  = group.GetPressedButton() == this;

            // Alpha is 1 for hovered/selected, otherwise faded.
            var c = SelfModulate;
            c.A = (isHovered || isSelected) ? 1f : fade_alpha;
            SelfModulate = c;
        }
        private void OnMouseExited()  => updateVisual();
    }
}
