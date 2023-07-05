using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Tests;

public partial class TestTrackInfo : TrackInfo
{
    public TestTrackInfo()
    {
        Bpm = 200;
        Measures = 4;
        Track = ResourceLoader.Load<AudioStream>("res://Resources/Helblinde - Heaven's Fall.ogg");
    }
}