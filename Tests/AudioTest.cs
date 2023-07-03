using Godot;
using XanaduProject.Singletons;

namespace XanaduProject.Tests;

[GlobalClass]
public partial class AudioTest : CenterContainer
{
	private ProgressBar progressBar = new ProgressBar{ CustomMinimumSize = new Vector2(200, 0) };
	private AudioSource audioSource = null!;
	private ColorRect beatRect = new ColorRect { CustomMinimumSize = new Vector2(200, 200) };

	public override void _Ready()
	{
		audioSource = GetNode<AudioSource>("/root/GlobalAudio");
		audioSource.SetTrack(new TestTrackInfo());

		AddChild(beatRect);
		AddChild(progressBar);

		audioSource.OnNewBeat += (_,_) =>
		{
			if (audioSource.Measure != 1) return;

			Tween t = CreateTween();
			t.TweenProperty(beatRect, "modulate", Colors.RebeccaPurple, 0);
			t.TweenProperty(beatRect, "modulate", Colors.Black, 0.1);
		};

		GD.Print($"{audioSource.Stream == null}");
		audioSource.Play();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if ( audioSource.Stream == null )return;

		progressBar.Value = audioSource.SongProgressPercentage();
	}
}