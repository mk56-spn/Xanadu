    // Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.UiElements
{
    public partial class AnimatedExpandableDropdown : Control
    {
        private readonly AnimatedHoverButton toggleButton;
        private readonly Label arrowLabel;
        private readonly Container contentContainer;
        private readonly Control expandablePanel;

        private bool isExpanded = false;
        private float currentExpansion = 0f;
        private float targetExpansion = 0f;
        private float expansionSpeed = 10f;

        private float currentArrowRotation = Mathf.Pi / 2; // Start at down (90 degrees)
        private float targetArrowRotation = Mathf.Pi / 2;
        private float rotationSpeed = 10f;

        public bool IsExpanded => isExpanded;

        public AnimatedExpandableDropdown(string buttonText, Container content, int fontSize = 50, Font font = null)
        {
            contentContainer = content;

            // Create horizontal container to hold button and expandable content
            var hBox = new HBoxContainer();
            AddChild(hBox);

            // Create the toggle button
            toggleButton = new AnimatedHoverButton(buttonText, fontSize, font);
            toggleButton.Pressed += ToggleExpansion;
            hBox.AddChild(toggleButton);

            // Create arrow label
            arrowLabel = new Label
            {
                Text = "▶",
                LabelSettings = new LabelSettings
                {
                    FontSize = fontSize,
                    Font = font ?? FontSource.PLASTIC_SLANTED
                },
                PivotOffset = new Vector2(fontSize / 4f, fontSize / 4f)
            };
            toggleButton.AddChild(arrowLabel);

            // Create expandable panel
            expandablePanel = new Control
            {
                ClipContents = true,
                CustomMinimumSize = new Vector2(0, 0)
            };
            hBox.AddChild(expandablePanel);

            // Add content to expandable panel
            expandablePanel.AddChild(contentContainer);
            contentContainer.Position = Vector2.Zero;
        }

        public override void _Ready()
        {
            // Position arrow on the right side of the button
            arrowLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight);
            arrowLabel.Position = new Vector2(-20, 0);

            // Start collapsed
            expandablePanel.CustomMinimumSize = new Vector2(0, 0);
            expandablePanel.Size = new Vector2(0, 0);
            currentExpansion = 0f;
            targetExpansion = 0f;
        }

        public override void _Process(double delta)
        {
            // Smoothly interpolate expansion
            currentExpansion = Mathf.Lerp(currentExpansion, targetExpansion, (float)delta * expansionSpeed);
            expandablePanel.CustomMinimumSize = new Vector2(currentExpansion, 0);

            // Smoothly interpolate arrow rotation
            currentArrowRotation = Mathf.Lerp(currentArrowRotation, targetArrowRotation, (float)delta * rotationSpeed);
            arrowLabel.Rotation = currentArrowRotation;
        }

        private void ToggleExpansion()
        {
            isExpanded = !isExpanded;

            if (isExpanded)
            {
                // Expand: arrow points left (0 degrees), show content
                targetArrowRotation = 0f;
                targetExpansion = contentContainer.GetCombinedMinimumSize().X;
            }
            else
            {
                // Collapse: arrow points down (90 degrees), hide content
                targetArrowRotation = Mathf.Pi / 2;
                targetExpansion = 0f;
            }
        }

        public void SetExpanded(bool expanded)
        {
            if (isExpanded != expanded)
            {
                ToggleExpansion();
            }
        }

        public void SetExpansionSpeed(float speed)
        {
            expansionSpeed = speed;
        }

        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = speed;
        }
    }
}
