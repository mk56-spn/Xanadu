// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems.Widgets
{
    public class NoteDirectionWidgetSystem : ComposerSystem
    {
        private readonly HBoxContainer container = new();
        public NoteDirectionWidgetSystem()
        {

            Visuals.AddToFloatingBar(container);
            populateDirectionButtons();
        }
        protected override void OnUpdate()
        {
            container.Visible = Composer.SelectedTemplateEntity.HasComponent<NoteEcs>();
        }

        private readonly ButtonGroup group = new();

        private void populateDirectionButtons()
        {
            // Neutral Direction
            var neutralButton = new Button { Text = "▧", ButtonGroup = group };
            neutralButton.Pressed += () => Composer.SelectedDirection = null;
            container.AddChild(neutralButton);

            // Cardinal (and diagonal) directions
            foreach (Direction dir in System.Enum.GetValues<Direction>())
            {
                var dirButton = new Button
                {
                    Text = directionToArrow(dir),
                    ButtonGroup = group
                };
                dirButton.Pressed += () => Composer.SelectedDirection = dir;
                container.AddChild(dirButton);
            }
        }

        /// <summary>
        /// Maps a Direction enum value to a Unicode arrow symbol.
        /// Extend this switch expression if more directions are added.
        /// </summary>
        private static string directionToArrow(Direction dir) => dir switch
        {
            Direction.Up        => "↑",
            Direction.Down      => "↓",
            Direction.Left      => "←",
            Direction.Right     => "→",
            Direction.UpLeft    => "↖",
            Direction.UpRight   => "↗",
            _                   => dir.ToString().Substring(0, 1) // Fallback
        };
    }
}
