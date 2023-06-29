using System;
using Godot;

namespace XanaduProject.Singletons;

public sealed partial class AudioSource : Node2D
{
	private AudioStreamPlayer player = new AudioStreamPlayer();
	/// <summary>
	/// The current globally active track.
	/// </summary>
	public AudioStreamPlayer Player
	{
		get => player;
		set => player = value;
	}

	public AudioSource()
	{
		AddChild(player);
	}
	
	public bool TrackIsNull() => 
		player.Stream == null;
	public double SongProgressPercentage() => 
		Math.Round(player.GetPlaybackPosition() / player.Stream.GetLength(), 2) * 100;
}