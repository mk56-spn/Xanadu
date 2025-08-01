 // Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Stateless;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.Composer
{
    internal partial class ButtonContainer : VBoxContainer
    {
        private ButtonGroup noteTypeGroup = null!;
        private ButtonGroup directionGroup = null!;

        public readonly IComposer Composer = DiProvider.Get<IComposer>();

        public override void _Ready()
        {
            noteTypeGroup = new ButtonGroup { AllowUnpress = false };
            directionGroup = new ButtonGroup { AllowUnpress = false };

            // --- Create UI Layout ---
            CategoryContainer notes = new("notes");
            AddChild(notes);

            CategoryContainer directions = new("directions");
            directions.MainElements.Columns = 5; // Arrange direction buttons in a row.
            AddChild(directions);

            CategoryContainer blocks = new("blocks");
            AddChild(blocks);

            // --- Populate Note and Block Buttons ---
            GD.Print(EntityTemplate.GET_ENTITIES.Count);
            EntityTemplate.GET_ENTITIES.ForEachEntity(( ref NameEcs name, Entity entity) =>
            {
                GD.Print("Entity");
                var b = new ItemButton(name.Name, entity) { ButtonGroup = noteTypeGroup };
                b.Pressed += () => { Composer.SelectedTemplateEntity = b.Entity; };

                if (entity.TryGetComponent(out NoteEcs note))
                {
                    GD.Print("Note");
                    notes.AddElement(b);
                    // We use Modulate for the base color and SelfModulate for selection highlighting.
                    b.Modulate = note.NoteType.NoteColor();
                    return;
                }

                if (entity.HasComponent<BlockEcs>())
                {
                    blocks.AddElement(b);
                    return;
                }

                // Fallback for other types
                AddChild(b);
            });

            // --- Populate Direction Buttons ---
            var neutralButton = new HighlightableButton { Text = "N", ButtonGroup = directionGroup };
            neutralButton.Pressed += () => Composer.SelectedDirection = null;
            directions.AddElement(neutralButton);

            foreach (Direction dir in System.Enum.GetValues<Direction>())
            {
                var dirButton = new HighlightableButton
                    { Text = dir.ToString().Substring(0, 1), ButtonGroup = directionGroup };
                dirButton.Pressed += () => Composer.SelectedDirection = dir;
                directions.AddElement(dirButton);
            }

            // --- Set Initial State ---
            // Select the first note type and the neutral direction by default.
            noteTypeGroup.GetButtons().First(b => b is ItemButton ib && ib.Entity == Composer.SelectedTemplateEntity)
                .ButtonPressed = true;
            directionGroup.GetButtons().First().ButtonPressed = true;

        }

        #region UI Elements

        private partial class CategoryContainer : VBoxContainer
        {
            public GridContainer MainElements { get; } = new() { Columns = 3 };

            public CategoryContainer(string category)
            {
                AddChild(new Label { Text = category.ToUpper(), HorizontalAlignment = HorizontalAlignment.Center });
                AddChild(MainElements);
                AddChild(new HSeparator());
            }

            public void AddElement(Node node)
            {
                MainElements.AddChild(node);
            }
        }

        /// <summary>
        /// A base button that efficiently handles selection highlighting within a ButtonGroup.
        /// </summary>
        private partial class HighlightableButton : Button
        {
            public HighlightableButton()
            {
                ToggleMode = true;
                CustomMinimumSize = new Vector2(50, 30);
                MouseEntered += () =>
                    CreateTween().TweenProperty(this, "self_modulate", Colors.White.Lightened(0.2f), 0.2f)
                        .FromCurrent();
                MouseExited += () => UpdateHighlight(); // Re-apply selection highlight on exit
            }

            public override void _EnterTree()
            {
                base._EnterTree();
                if (ButtonGroup != null)
                {
                    ButtonGroup.Pressed += OnButtonGroupPressed;
                    UpdateHighlight(ButtonGroup.GetPressedButton());
                }
            }

            public override void _ExitTree()
            {
                base._ExitTree();
                if (ButtonGroup != null) ButtonGroup.Pressed -= OnButtonGroupPressed;
            }

            private void OnButtonGroupPressed(BaseButton button)
            {
                // Update all buttons in the group when one is pressed.
                UpdateHighlight(button);
            }

            protected void UpdateHighlight(BaseButton? pressedButton = null)
            {
                if (ButtonGroup == null) return;
                pressedButton ??= ButtonGroup.GetPressedButton();

                // Use SelfModulate to show selection state, which won't conflict with the base Modulate color.
                SelfModulate = pressedButton == this
                    ? Colors.White
                    : Colors.Black.Lightened(0.4f);
            }
        }

        /// <summary>
        /// A specialized button that holds a reference to an ECS entity template.
        /// </summary>
        private partial class ItemButton : HighlightableButton
        {
            public readonly Entity Entity;

            public ItemButton(string text, Entity entity)
            {
                Entity = entity;
                Text = text;
            }
        }

        #endregion
    }
}
