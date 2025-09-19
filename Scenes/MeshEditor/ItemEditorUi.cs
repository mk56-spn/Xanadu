using Godot;
using System.Collections.Generic;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class ItemEditorUi : VBoxContainer
    {
        private readonly IItemEditor editor;
        private readonly ButtonGroup layerButtonGroup;
        private Button addLayerButton;
        private CheckBox lockHandlesCheckBox;
        private CheckBox showAllLayersCheckBox;
        private Button doneButton;

        private const string layer_panel_name = "LayerPanel";
        private const string layer_buttons_container_name = "LayerButtonsContainer";

        public ItemEditorUi(IItemEditor editor)
        {
            this.editor = editor;
            this.editor.MeshLayersChanged += refreshLayerButtons;
            this.editor.ActiveMeshSelectionChanged += refreshHandleLockCheckbox;

            PanelContainer layerPanel = new PanelContainer { Name = layer_panel_name, MouseFilter = MouseFilterEnum.Stop };
            AddChild(layerPanel);

            VBoxContainer layerButtonsContainer = new VBoxContainer { Name = layer_buttons_container_name };
            layerPanel.AddChild(layerButtonsContainer);

            layerButtonGroup = new ButtonGroup();

            addLayerButton = new Button { Text = "Add New Mesh Layer" };
            addLayerButton.Pressed += OnAddLayerButtonPressed;

            VBoxContainer miscContainer = new VBoxContainer();
            AddChild(miscContainer);

            lockHandlesCheckBox = new CheckBox { Text = "Lock Handles" };
            lockHandlesCheckBox.Toggled += OnHandlesLockedToggled;
            miscContainer.AddChild(lockHandlesCheckBox);

            showAllLayersCheckBox = new CheckBox { Text = "Show All Layers" };
            showAllLayersCheckBox.Toggled += OnShowAllLayersToggled;
            miscContainer.AddChild(showAllLayersCheckBox);

            doneButton = new Button { Text = "Done" };
            doneButton.Pressed += OnDoneButtonPressed;
            miscContainer.AddChild(doneButton);

            refreshLayerButtons();
            refreshHandleLockCheckbox();
            showAllLayersCheckBox.ButtonPressed = editor.LayerManager.GetShowAllLayers();
        }

        private void refreshLayerButtons()
        {
            var layerButtonsContainer = GetNode<PanelContainer>(layer_panel_name).GetNode<VBoxContainer>(layer_buttons_container_name);

            if (addLayerButton.GetParent() == layerButtonsContainer)
            {
                layerButtonsContainer.RemoveChild(addLayerButton);
            }

            foreach (Node child in layerButtonsContainer.GetChildren())
            {
                child.QueueFree();
            }

            IReadOnlyList<string> layerNames = editor.LayerManager.GetMeshLayerNames();
            for (int i = 0; i < layerNames.Count; i++)
            {
                var layerControlContainer = new HBoxContainer();
                layerButtonsContainer.AddChild(layerControlContainer);

                int layerIndex = i;

                var layerButton = new Button { Text = layerNames[i], ToggleMode = true, ButtonGroup = layerButtonGroup };
                layerButton.Pressed += () => OnLayerButtonPressed(layerIndex);
                layerControlContainer.AddChild(layerButton);

                var moveUpButton = new Button { Text = "▲" };
                moveUpButton.Pressed += () => OnMoveLayerUpPressed(layerIndex);
                layerControlContainer.AddChild(moveUpButton);

                var moveDownButton = new Button { Text = "▼" };
                moveDownButton.Pressed += () => OnMoveLayerDownPressed(layerIndex);
                layerControlContainer.AddChild(moveDownButton);

                var colorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(100, 100), Color = editor.LayerManager.GetLayerColor(layerIndex) };
                colorPicker.ColorChanged += (newColor) => OnLayerColorChanged(layerIndex, newColor);
                layerControlContainer.AddChild(colorPicker);
            }

            layerButtonsContainer.AddChild(addLayerButton);

            if (editor.LayerManager.ActiveMeshIndex != -1 && editor.LayerManager.ActiveMeshIndex < layerNames.Count)
            {
                var buttons = layerButtonGroup.GetButtons();
                if (editor.LayerManager.ActiveMeshIndex < buttons.Count)
                {
                    buttons[editor.LayerManager.ActiveMeshIndex].ButtonPressed = true;
                }
            }
        }

        private void refreshHandleLockCheckbox()
        {
            bool hasSelectedPoint = editor.LayerManager.HasActiveMeshSelectedBezierPoint();
            lockHandlesCheckBox.Visible = hasSelectedPoint;
            if (hasSelectedPoint)
            {
                lockHandlesCheckBox.SetDeferred("button_pressed", editor.LayerManager.GetActiveMeshHandlesLockedState());
            }
        }

        private void OnLayerButtonPressed(int index)
        {
            editor.LayerManager.SetActiveMeshLayer(index);
        }

        private void OnAddLayerButtonPressed()
        {
            editor.LayerManager.AddNewMeshEntity();
        }

        private void OnHandlesLockedToggled(bool toggled)
        {
            editor.LayerManager.SetActiveMeshHandlesLockedState(toggled);
        }

        private void OnShowAllLayersToggled(bool toggled)
        {
            editor.LayerManager.SetShowAllLayers(toggled);
        }

        private void OnMoveLayerUpPressed(int index)
        {
            editor.LayerManager.MoveLayerUp(index);
            refreshLayerButtons();
        }

        private void OnMoveLayerDownPressed(int index)
        {
            editor.LayerManager.MoveLayerDown(index);
            refreshLayerButtons();
        }

        private void OnLayerColorChanged(int layerIndex, Color newColor)
        {
            editor.LayerManager.SetLayerColor(layerIndex, newColor);
        }

        private void OnDoneButtonPressed()
        {
            editor.TriggerSave();
        }

        public override void _Draw()
        {
            base._Draw();
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.2f, 0.2f, 0.2f, 0.5f));
        }
    }
}
