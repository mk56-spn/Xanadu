// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.ComposerUI
{
    /// <summary>
    /// A <see cref="Button"/> which updates the selected <see cref="Composer"/>> item when pressed.
    /// </summary>
    public partial class ItemButton : Button
    {
        public ItemButton (ItemType itemType)
        {
            CustomMinimumSize = CustomMinimumSize with { Y = 30 };
            Text = itemType.ToString();

            ButtonUp += () => GetParent<ItemBar>().Selected = itemType;
        }
    }
}

