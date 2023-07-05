using Godot;

namespace XanaduProject.DataStructure;

[GlobalClass]
public partial class StageInfo : Resource
{
    [Export]
    public int Difficulty { get; set; }
    [Export]
    public TrackInfo TrackInfo { get; set; } = null!;
    [Export]
    public string[] Designers { get; set; } = null!;
    [Export]
    public string Description { get; set; } = null!;
}