// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems.Widgets
{
    public class NoteTypeWidgetSystem : ComposerSystem
    {
        private readonly HBoxContainer container = new();

        public NoteTypeWidgetSystem()
        {
            Visuals.AddToFloatingBar(container);

            foreach (var v in Enum.GetValues<NoteType>())
            {
                var button = new Button
                {
                    Text = v.ToString()
                };
                button.Pressed += () => Composer.SelectedNoteType = v;

                var fontColor = new Color(1, 0, 0); // Red color (RGB values between 0 and 1)

                // Apply the font color to the button
                button.AddThemeColorOverride("font_color", fontColor);

                container.AddChild(button);
            }
        }

        protected override void OnUpdate()
        {
            container.Visible = Composer.SelectedTemplateEntity.HasComponent<NoteEcs>();
        }
    }
}
