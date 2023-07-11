using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Singletons;

namespace XanaduProject.Screens.StageSelection;

public partial class StageSelectionPanel : PanelContainer
{
    private readonly StageInfo stageInfo;

    public StageSelectionPanel(StageInfo stageInfo) =>
        this.stageInfo = stageInfo;

    public override void _Ready()
    {
        base._Ready();

        Label label = new Label { Text = stageInfo.Title };
        Button selectButton = new Button { Text = "PLAY" };
        VBoxContainer container = new VBoxContainer { CustomMinimumSize = new Vector2(150, 0) };

        AddChild(container);
        container.AddChild(label);
        container.AddChild(selectButton);

        selectButton.Pressed += () =>
        {
            GetNode<AudioSource>("/root/GlobalAudio").SetTrack(stageInfo.TrackInfo);
            GetTree().ChangeSceneToPacked(stageInfo.Stage);
        };
    }
}