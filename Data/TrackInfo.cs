using Godot;

namespace XanaduProject.Data;

public partial class TrackInfo : Resource
{
    public double Offset { get; set; }
    // Currently bpm changes are not supported.
    public double Bpm { get; set; }

    public int Measures { get; set; }

    public AudioStream Track { get; set; } = null!;
}
    
