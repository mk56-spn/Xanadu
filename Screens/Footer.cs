// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens
{

    public partial class Footer : PanelContainer
    {
        private readonly HBoxContainer itemContainer = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        private readonly VBoxContainer flow = new();
        public Footer()
        {
            CustomMinimumSize = new Vector2(0, 50); // Adjust height as needed

            AddChild(new ColorRect
            {
                Color = Colors.Black with { A = 0.5f },
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            });

            AddChild(flow);
            flow.AddChild(new ColorRect
            {
                CustomMinimumSize = new Vector2(0,10),
                Color = Colors.Gold,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            });
            flow.AddChild(itemContainer);

            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        }

        public void AddButton(Button button)
        {
            itemContainer.AddChild(button);
        }
    }
}
