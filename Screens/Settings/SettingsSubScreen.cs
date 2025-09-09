// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Buttons;
using XanaduProject.Character;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Screens.Settings
{
    public partial class SettingsSubScreen : SubScreen
    {
        // Shell categories to be implemented later
        private CategoryControl displaysCategory;
        private CategoryControl keybindingsCategory;
        private CategoryControl audioCategory;


        public SettingsSubScreen()
        {
            // It's better to load settings once at game startup,
            // but for this screen, we'll load them here to ensure they are up to date.
            GameSettings.LoadSettings();

            // Main container with margin
            var mainContainer = new MarginContainer();
            mainContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            mainContainer.AddThemeConstantOverride("margin_left", 20);
            mainContainer.AddThemeConstantOverride("margin_right", 20);
            mainContainer.AddThemeConstantOverride("margin_top", 20);
            mainContainer.AddThemeConstantOverride("margin_bottom", 20);
            AddChild(mainContainer);

            // Categories container (vertical)
            var categoriesContainer = new VBoxContainer();
            categoriesContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            mainContainer.AddChild(categoriesContainer);

            // Add categories
            displaysCategory = new CategoryControl("Displays");
            categoriesContainer.AddChild(displaysCategory);

            keybindingsCategory = new CategoryControl("Keybindings");
            categoriesContainer.AddChild(keybindingsCategory);

            audioCategory = new CategoryControl("Audio");
            categoriesContainer.AddChild(audioCategory);

            // Add window settings to displays category
            var windowSettings = new WindowSettingsControl("💻", "Window Settings");
            displaysCategory.AddSettingsControl(windowSettings);
            audioCategory.AddSettingsControl(new OffsetControl("<< ⏳ >>", "Offset"));
        }
    }

    // Class for collapsible category sections
    public partial class CategoryControl : VBoxContainer
    {
        private AnimatedHoverButton toggleButton;
        private VBoxContainer contentContainer;

        public CategoryControl(string categoryName)
        {
            toggleButton = new AnimatedHoverButton(categoryName)
            {
                FocusMode = FocusModeEnum.None,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(toggleButton);

            contentContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(contentContainer);

            toggleButton.Pressed += toggleCategory;
        }

        public void AddSettingsControl(Control control) =>
            contentContainer.AddChild(control);

        private void toggleCategory()=>
            contentContainer.Visible = !contentContainer.Visible;
    }
}
