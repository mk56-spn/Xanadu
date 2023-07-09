using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Singletons;

namespace XanaduProject.Tests;

[GlobalClass]
public partial class AudioTest : CenterContainer
{
	private ProgressBar progressBar = new ProgressBar { CustomMinimumSize = new Vector2(200, 0) };
	private AudioSource audioSource = null!;
	private ColorRect beatRect = new ColorRect { CustomMinimumSize = new Vector2(200, 200) };
	private VBoxContainer container = new VBoxContainer { CustomMinimumSize = new Vector2(100, 0) };
	private Label measureText = new Label();

	public override void _Ready()
	{
		Setup();

		audioSource.OnNewBeat += (_, _) =>
		{

			measureText.Text = $"{audioSource.Measure}";
			Tween t = CreateTween();
			t.TweenProperty(beatRect, "modulate", audioSource.Measure == 1 ? Colors.LightGreen : Colors.MediumPurple, 0);
			t.TweenProperty(beatRect, "modulate", Colors.Black, 0.3);
		};

		GD.Print($"{audioSource.Stream == null}");

		audioSource.Play();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (audioSource.Stream == null) return;

		progressBar.Value = audioSource.SongProgressPercentage();
	}

	private void Setup()
	{
		audioSource = GetNode<AudioSource>("/root/GlobalAudio");
		audioSource.SetTrack(ResourceLoader.Load<TrackInfo>("res://Resources/TestTrack.tres"));

		AddChild(container);

		container.AddChild(beatRect);
		container.AddChild(progressBar);
		container.AddChild(measureText);
	}
}