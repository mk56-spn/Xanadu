using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.StageSelection;

public partial class StageSelection : Control
{
    public override void _Ready()
    {
        base._Ready();

        VBoxContainer trackList = GetNode<VBoxContainer>("TrackList");

        trackList.AddChild(new StageSelectionPanel(ResourceLoader.Load<StageInfo>("res://Resources/Stages/Stage 1/Stage 1.tres")));
        trackList.AddChild(new StageSelectionPanel(ResourceLoader.Load<StageInfo>("res://Resources/Stages/Stage 2/Stage 2.tres")));
    }
}