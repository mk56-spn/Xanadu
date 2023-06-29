using Godot;
using XanaduProject.Singletons;

namespace XanaduProject.Tests;

[GlobalClass]
public partial class AudioTest : CenterContainer
{
	private ProgressBar progressBar = new ProgressBar{ CustomMinimumSize = new Vector2(200, 0) };
	
	private AudioSource audioSource = null!;

	public override void _Ready()
	{
		AddChild(progressBar);

		audioSource = GetNode<AudioSource>("/root/GlobalAudio");

		audioSource.Player.Stream = ResourceLoader.Load("res://Resources/Helblinde - Heaven's Fall.ogg") as AudioStream;
		audioSource.Player.Play();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		progressBar.Value = audioSource.SongProgressPercentage();
	}
}