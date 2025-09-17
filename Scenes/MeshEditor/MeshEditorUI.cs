using Godot;
using System.Collections.Generic;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshEditorUi : VBoxContainer
    {
        private readonly IMeshEditor editor;
        private readonly ButtonGroup layerButtonGroup;
        private Button addLayerButton;
        private CheckBox lockHandlesCheckBox;
        private CheckBox showAllLayersCheckBox;

        private const string layer_panel_name = "LayerPanel";
        private const string layer_buttons_container_name = "LayerButtonsContainer";

        public MeshEditorUi(IMeshEditor editor)
        {
            this.editor = editor;
            this.editor.MeshLayersChanged += refreshLayerButtons;
            this.editor.ActiveMeshSelectionChanged += refreshHandleLockCheckbox;

            // PanelContainer to block input for layer buttons
            PanelContainer layerPanel = new PanelContainer();
            layerPanel.Name = layer_panel_name;
            layerPanel.MouseFilter = MouseFilterEnum.Stop;
            AddChild(layerPanel);

            // VBoxContainer for layer buttons and add button
            VBoxContainer layerButtonsContainer = new VBoxContainer();
            layerButtonsContainer.Name = layer_buttons_container_name;
            layerPanel.AddChild(layerButtonsContainer);

            // Setup ButtonGroup
            layerButtonGroup = new ButtonGroup();

            // Add New Layer Button (will be re-added in refreshLayerButtons)
            addLayerButton = new Button();
            addLayerButton.Text = "Add New Mesh Layer";
            addLayerButton.Pressed += OnAddLayerButtonPressed;

            // VBoxContainer for handle locking checkbox
            VBoxContainer handleLockContainer = new VBoxContainer();
            AddChild(handleLockContainer);

            // Handle locking CheckBox
            lockHandlesCheckBox = new CheckBox();
            lockHandlesCheckBox.Text = "Lock Handles";
            lockHandlesCheckBox.Toggled += OnHandlesLockedToggled;
            handleLockContainer.AddChild(lockHandlesCheckBox);

            // NEW: CheckBox for showing all layers
            showAllLayersCheckBox = new CheckBox();
            showAllLayersCheckBox.Text = "Show All Layers";
            showAllLayersCheckBox.Toggled += OnShowAllLayersToggled;
            AddChild(showAllLayersCheckBox); // Add it to the main VBoxContainer

            // Initial refresh
            refreshLayerButtons();
            refreshHandleLockCheckbox();
            showAllLayersCheckBox.ButtonPressed = editor.LayerManager.GetShowAllLayers();
        }

        private void refreshLayerButtons()
        {
            VBoxContainer layerButtonsContainer = GetNode<PanelContainer>(layer_panel_name).GetNode<VBoxContainer>(layer_buttons_container_name);

            // Temporarily remove addLayerButton if it's already a child
            if (addLayerButton.GetParent() == layerButtonsContainer)
            {
                layerButtonsContainer.RemoveChild(addLayerButton);
            }

            // Clear all existing layer control containers
            foreach (Node child in layerButtonsContainer.GetChildren())
            {
                child.QueueFree();
            }

            IReadOnlyList<string> layerNames = editor.LayerManager.GetMeshLayerNames();
            for (int i = 0; i < layerNames.Count; i++)
            {
                HBoxContainer layerControlContainer = new HBoxContainer();
                layerButtonsContainer.AddChild(layerControlContainer);

                int layerIndex = i; // Capture the current value of i

                Button layerButton = new Button();
                layerButton.Text = layerNames[i];
                layerButton.ToggleMode = true;
                layerButton.ButtonGroup = layerButtonGroup;
                layerButton.Pressed += () => OnLayerButtonPressed(layerButton, layerIndex);
                layerControlContainer.AddChild(layerButton);

                // Add Move Up button
                Button moveUpButton = new Button();
                moveUpButton.Text = "▲";
                moveUpButton.Pressed += () => OnMoveLayerUpPressed(layerIndex);
                layerControlContainer.AddChild(moveUpButton);

                // Add Move Down button
                Button moveDownButton = new Button();
                moveDownButton.Text = "▼";
                moveDownButton.Pressed += () => OnMoveLayerDownPressed(layerIndex);
                layerControlContainer.AddChild(moveDownButton);
            }

            // Add the "Add New Mesh Layer" button back at the end
            layerButtonsContainer.AddChild(addLayerButton);

            // Ensure the active layer button is toggled
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

        private void OnLayerButtonPressed(Button button, int index)
        {
            editor.LayerManager.SetActiveMeshLayer(index);
        }

        private void OnAddLayerButtonPressed()
        {
            editor.LayerManager.AddNewMeshLayer();
        }

        private void OnHandlesLockedToggled(bool toggled)
        {
            editor.LayerManager.SetActiveMeshHandlesLockedState(toggled);
        }

        // NEW: Handler for "Show All Layers" checkbox
        private void OnShowAllLayersToggled(bool toggled)
        {
            editor.LayerManager.SetShowAllLayers(toggled);
        }

        // NEW: Handlers for moving layers
        private void OnMoveLayerUpPressed(int index)
        {
            editor.LayerManager.MoveLayerUp(index);
            refreshLayerButtons(); // Refresh UI after reordering
        }

        private void OnMoveLayerDownPressed(int index)
        {
            editor.LayerManager.MoveLayerDown(index);
            refreshLayerButtons(); // Refresh UI after reordering
        }

        public override void _Draw()
        {
            base._Draw();
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.2f, 0.2f, 0.2f, 0.5f));
        }
    }
}
