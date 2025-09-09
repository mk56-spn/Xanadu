// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.ScreenStructure
{
    public partial class ScreenWithFooter : MainScreen
    {
        private readonly Footer footer;
        protected readonly Control Body;

        public ScreenWithFooter()
        {
            var mainContainer = new VBoxContainer();
            mainContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(mainContainer);


            Body = new Control
            {
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            mainContainer.AddChild(Body);

            footer = new Footer();
            mainContainer.AddChild(footer);
        }

        public void AddButtonToFooter(Button button)
        {
            footer.AddButton(button);
        }
    }
}
