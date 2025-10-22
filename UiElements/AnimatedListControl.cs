// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Godot;

namespace XanaduProject.UiElements
{
    public partial class AnimatedListControl : VBoxContainer
    {
        private readonly List<SelectableButton> items = new();
        private int selectedIndex = -1;

        public int SelectedIndex => selectedIndex;

        public AnimatedListControl()
        {
            AddThemeConstantOverride("separation", 5);
        }

        public void AddItem(string text, Action onPressed, int fontSize = 40, Font? font = null)
        {
            var item = new SelectableButton(text, fontSize, font ?? FontSource.PLASTIC_SLANTED);
            int index = items.Count;

            item.Pressed += () =>
            {
                SetSelected(index);
                onPressed?.Invoke();
            };

            items.Add(item);
            AddChild(item);
        }

        public void SetSelected(int index)
        {
            if (index < 0 || index >= items.Count)
            {
                return;
            }

            // Deselect previous item
            if (selectedIndex >= 0 && selectedIndex < items.Count)
            {
                items[selectedIndex].SetSelected(false);
            }

            selectedIndex = index;
            items[selectedIndex].SetSelected(true);
        }

        public void ClearItems()
        {
            foreach (var item in items)
            {
                item.QueueFree();
            }

            items.Clear();
            selectedIndex = -1;
        }
    }
}
