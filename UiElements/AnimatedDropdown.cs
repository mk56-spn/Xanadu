// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Godot;

namespace XanaduProject.UiElements
{
    public partial class CustomAnimatedDropdown : Control
    {
        [Signal]
        public delegate void ItemSelectedEventHandler(int index);

        private readonly AnimatedHoverButton mainButton;
        private readonly PopupPanel popup;
        private readonly VBoxContainer itemContainer;
        private readonly List<string> items;
        private int selectedIndex = -1;
        private readonly int fontSize;
        private readonly Font font;

        public CustomAnimatedDropdown(string placeholder, List<string> items, int fontSize = 50, Font? font = null)
        {
            this.items = items;
            this.fontSize = fontSize;
            this.font = font ?? FontSource.PLASTIC_SLANTED;

            mainButton = new AnimatedHoverButton(placeholder, fontSize, this.font);
            mainButton.Pressed += OnMainButtonPressed;
            AddChild(mainButton);

            popup = new PopupPanel();



            popup.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

            itemContainer = new VBoxContainer();
            popup.AddChild(itemContainer);
            AddChild(popup);

            populateItems();
        }

        private void populateItems()
        {
            for (int i = 0; i < items.Count; i++)
            {
                var itemButton = new UiElements.AnimatedHoverButton(items[i], fontSize, font);
                int index = i; // Capture index for the lambda
                itemButton.Pressed += () => OnItemSelected(index);
                itemContainer.AddChild(itemButton);
            }
        }

        private void OnMainButtonPressed()
        {
            if (popup.Visible)
            {
                popup.Hide();
            }
            else
            {
                // Set the width of the container to match the button, the height will be automatic
                itemContainer.CustomMinimumSize = new Vector2(mainButton.Size.X, 0);
                popup.Position = new Vector2I(0, (int)mainButton.Size.Y);
                popup.Popup();
            }
        }

        private void OnItemSelected(int index)
        {
            if (selectedIndex == index)
            {
                popup.Hide();
                return;
            }
            selectedIndex = index;
            mainButton.Text = items[index];
            popup.Hide();
            EmitSignal(SignalName.ItemSelected, index);
        }
    }
}
