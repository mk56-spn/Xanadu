using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.StageSelection;

public partial class StageSelectionPanel : PanelContainer
{
    public StageSelectionPanel(StageInfo stageInfo)
    {
        Label label = new Label { Text = stageInfo.Title };
        Button selectButton = new Button { Text = "PLAY" };
        VBoxContainer container = new VBoxContainer { CustomMinimumSize = new Vector2(150, 0) };

        AddChild(container);
        container.AddChild(label);
        container.AddChild(selectButton);

        selectButton.Pressed += () => GetTree().ChangeSceneToPacked(stageInfo.Stage);
    }
}