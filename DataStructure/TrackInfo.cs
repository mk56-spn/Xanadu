using Godot;

namespace XanaduProject.DataStructure;

[GlobalClass]
public partial class TrackInfo : Resource
{
    [Export]
    public double[] TimingPoints { get; set; } = null!;
    [Export]
    public double Bpm { get; set; }
    [Export]
    public int Measures { get; set; }
    [Export(PropertyHint.ResourceType)]
    public AudioStream Track { get; set; } = null!;
}
    
