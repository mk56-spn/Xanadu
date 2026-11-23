namespace Xanadu.Model.Animation;

/// <summary>Play/pause controls shared by the animator tool and preview hosts.</summary>
public sealed class AnimatorPlayback(AnimatorRuntime runtime)
{
    public AnimatorRuntime Runtime { get; private set; } = runtime;
    public bool IsPlaying => !Runtime.Paused;

	public void Play()
	{
		if (Runtime.CurrentTime >= Runtime.CurrentDuration - 0.0001f)
			Runtime.Restart();
		Runtime.SetPaused(false);
	}

	public void Stop() => Runtime.SetPaused(true);

	public void Update(float delta) => Runtime.Update(delta);

	public void Replace(AnimatorRuntime runtime)
	{
		Runtime = runtime;
	}
}
