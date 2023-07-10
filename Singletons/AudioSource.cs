using System;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Singletons;

public sealed partial class AudioSource : AudioStreamPlayer
{
	// For some reason Godot uses time in seconds. So this conversion is necessary.
	private double secondsPerBeat;
	private int positionInBeats;

	public int LastPlayedBeat { get; private set; }
	public double Bpm { get; private set; } = 120;
	public int Measures { get; private set; } = 4;

	/// <summary>
	/// If set to true starts the audio stream on the next physics process tic to ensure the beat actions are synced;
	/// </summary>
	public bool RequestPlay;

	public double TrackPosition { get; private set; }
	public int Measure { get; private set; }

	public event EventHandler<int>? OnNewBeat;

	public double SongProgressPercentage() =>
		Math.Round(GetPlaybackPosition() / Stream.GetLength(), 2) * 100;


	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (RequestPlay)
		{
			RequestPlay = false;
			Play();
		}
		if (!Playing) return;

		TrackPosition = GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix();
		TrackPosition -= AudioServer.GetOutputLatency();
		positionInBeats = (int)Math.Floor(TrackPosition / secondsPerBeat) + 1;

		ReportBeat();
	}

	private void ReportBeat()
	{
		if (LastPlayedBeat < positionInBeats is false)
			return;

		/*GD.Print(lastPlayedBeat, " / ", positionInBeats, $" / {TrackPosition}");*/

		if (Measure > Measures)
			Measure = 1;

		OnNewBeat?.Invoke(this, positionInBeats);

		LastPlayedBeat = positionInBeats;
		Measure++;
	}

	public void SetTrack(TrackInfo trackInfo)
	{
		Bpm = trackInfo.Bpm;
		Stream = trackInfo.Track;
		Measures = trackInfo.Measures;

		Measure = 1;

		secondsPerBeat = 60 / Bpm;
	}
}