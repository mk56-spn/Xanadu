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
            layerPanel.MouseFilter = MouseFilterEnum.Stop; // <--- Added this line
            AddChild(layerPanel);

            // VBoxContainer for layer buttons and add button
            VBoxContainer layerButtonsContainer = new VBoxContainer();
            layerButtonsContainer.Name = layer_buttons_container_name;
            layerPanel.AddChild(layerButtonsContainer);

            // Setup ButtonGroup
            layerButtonGroup = new ButtonGroup();

            // Add New Layer Button
            addLayerButton = new Button();
            addLayerButton.Text = "Add New Mesh Layer";
            addLayerButton.Pressed += OnAddLayerButtonPressed;
            layerButtonsContainer.AddChild(addLayerButton);

            // VBoxContainer for handle locking checkbox
            VBoxContainer handleLockContainer = new VBoxContainer();
            AddChild(handleLockContainer);

            // Handle locking CheckBox
            lockHandlesCheckBox = new CheckBox();
            lockHandlesCheckBox.Text = "Lock Handles";
            lockHandlesCheckBox.Toggled += OnHandlesLockedToggled;
            handleLockContainer.AddChild(lockHandlesCheckBox);

            // Initial refresh
            refreshLayerButtons();
            refreshHandleLockCheckbox();
        }

        private void refreshLayerButtons()
        {
            // Get the layerButtonsContainer from the PanelContainer
            VBoxContainer layerButtonsContainer = GetNode<PanelContainer>(layer_panel_name).GetNode<VBoxContainer>(layer_buttons_container_name);

            // Clear existing layer buttons (except the Add button)
            // Iterate children in reverse to safely remove them
            for (int i = layerButtonsContainer.GetChildCount() - 1; i >= 0; i--)
            {
                Node child = layerButtonsContainer.GetChild(i);
                if (child is Button button && button != addLayerButton)
                {
                    button.QueueFree(); // Safely remove old buttons
                }
            }

            IReadOnlyList<string> layerNames = editor.GetMeshLayerNames();
            for (int i = 0; i < layerNames.Count; i++)
            {
                Button layerButton = new Button();
                layerButton.Text = layerNames[i];
                layerButton.ToggleMode = true;
                layerButton.ButtonGroup = layerButtonGroup;
                layerButton.Pressed += () => OnLayerButtonPressed(layerButton, i);
                layerButtonsContainer.AddChild(layerButton);
            }

            // Ensure the active layer button is toggled
            if (editor.ActiveMeshIndex != -1 && editor.ActiveMeshIndex < layerButtonGroup.GetButtons().Count)
            {
                layerButtonGroup.GetButtons()[editor.ActiveMeshIndex].ButtonPressed = true;
            }
        }

        private void refreshHandleLockCheckbox()
        {
            bool hasSelectedPoint = editor.HasActiveMeshSelectedBezierPoint();
            lockHandlesCheckBox.Visible = hasSelectedPoint;
            if (hasSelectedPoint)
            {
                lockHandlesCheckBox.SetDeferred("button_pressed", editor.GetActiveMeshHandlesLockedState());
            }
        }

        private void OnLayerButtonPressed(Button button, int index)
        {
            editor.SetActiveMeshLayer(index);
        }

        private void OnAddLayerButtonPressed()
        {
            editor.AddNewMeshLayer();
        }

        private void OnHandlesLockedToggled(bool toggled)
        {
            editor.SetActiveMeshHandlesLockedState(toggled);
        }
    }
}
