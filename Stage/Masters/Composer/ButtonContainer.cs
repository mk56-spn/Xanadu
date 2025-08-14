// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.Stage.Masters.Composer
{
    /// <summary>
    /// Manages the UI container for composer buttons, including note types, directions, and blocks.
    /// </summary>
    internal partial class ButtonContainer : VBoxContainer
    {
        private ButtonGroup noteTypeGroup = null!;
        private ButtonGroup directionGroup = null!;
        private ButtonGroup blockShaderGroup = null!;

        private CategoryContainer blocksContainer = null!;

        public readonly IComposer Composer = DiProvider.Get<IComposer>();

        public override void _Ready()
        {
            initializeButtonGroups();
            populateButtons();
            setInitialSelection();
        }

        #region Initialization

        private void initializeButtonGroups()
        {
            noteTypeGroup = new ButtonGroup { AllowUnpress = false };
            directionGroup = new ButtonGroup { AllowUnpress = false };
            blockShaderGroup = new ButtonGroup { AllowUnpress = false };
        }


        private void populateButtons()
        {
            populateEntityButtons();
            populateBlockShaderButtons();
        }

        private void populateEntityButtons()
        {
            EntityTemplate.GET_ENTITIES.ForEachEntity((ref NameEcs name, Entity entity) =>
            {
                var button = new ItemButton(name.Name, entity) { ButtonGroup = noteTypeGroup };
                button.Pressed += () => Composer.SelectedTemplateEntity = button.Entity;

            });

        }



        private void populateBlockShaderButtons()
        {
            foreach (BlockShaderId id in System.Enum.GetValues<BlockShaderId>())
            {
                var button = new HighlightableButton
                {
                    Text = id.ToString(),
                    ButtonGroup = blockShaderGroup
                };
                button.Pressed += () => Composer.SelectedBlockShaderId = id;
                blocksContainer.AddElement(button);
            }
        }

        private void setInitialSelection()
        {
            // Select the first note type that matches the composer's initially selected entity.
            var initialNoteButton = noteTypeGroup.GetButtons()
                                                 .FirstOrDefault(b => b is ItemButton ib && ib.Entity == Composer.SelectedTemplateEntity);
            if (initialNoteButton != null)
            {
                initialNoteButton.ButtonPressed = true;
            }

            // Select the neutral direction by default.
            var initialDirectionButton = directionGroup.GetButtons().FirstOrDefault();
            if (initialDirectionButton != null)
            {
                initialDirectionButton.ButtonPressed = true;
            }

            var initialBlockShader = blockShaderGroup.GetButtons().FirstOrDefault();
            if(initialBlockShader != null)
            {
                initialBlockShader.ButtonPressed = true;
            }
        }

        #endregion

        #region UI Elements

        /// <summary>
        /// A container for a category of buttons, with a title and a grid layout.
        /// </summary>
        private partial class CategoryContainer : VBoxContainer
        {
            public GridContainer MainElements { get; } = new() { Columns = 3 };

            public CategoryContainer(string category)
            {
                Name = $"{category}Category";
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
            private static readonly Color hover_color = Colors.White.Lightened(0.2f);
            private static readonly Color selected_color = Colors.White;
            private static readonly Color deselected_color = Colors.Black.Lightened(0.4f);
            private const float hover_tween_duration = 0.2f;

            public HighlightableButton()
            {
                ToggleMode = true;
                CustomMinimumSize = new Vector2(50, 30);
            }

            public override void _Ready()
            {
                MouseEntered += OnMouseEntered;
                MouseExited += OnMouseExited;
            }

            public override void _EnterTree()
            {
                base._EnterTree();
                if (ButtonGroup != null)
                {
                    ButtonGroup.Pressed += OnButtonGroupPressed;
                    // Set initial highlight state
                    updateHighlight(ButtonGroup.GetPressedButton());
                }
            }

            public override void _ExitTree()
            {
                base._ExitTree();
                if (ButtonGroup != null)
                {
                    ButtonGroup.Pressed -= OnButtonGroupPressed;
                }
                MouseEntered -= OnMouseEntered;
                MouseExited -= OnMouseExited;
            }

            private void OnMouseEntered()
            {
                CreateTween().TweenProperty(this, "self_modulate", hover_color, hover_tween_duration).FromCurrent();
            }

            private void OnMouseExited()
            {
                // Re-apply selection highlight on exit, as the hover effect overwrites it.
                updateHighlight();
            }

            private void OnButtonGroupPressed(BaseButton button)
            {
                // Update highlight for all buttons in the group when one is pressed.
                updateHighlight(button);
            }

            private void updateHighlight(BaseButton? pressedButton = null)
            {
                if (ButtonGroup == null) return;
                pressedButton ??= ButtonGroup.GetPressedButton();

                // Use SelfModulate to show selection state, which won't conflict with the base Modulate color.
                SelfModulate = pressedButton == this ? selected_color : deselected_color;
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
                Text = text;
                Entity = entity;
            }
        }

        #endregion
    }
}
