// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Audio
{
	/// <summary>
	///     The track manager is a source of truth for audio timing across a gameplay instance.
	/// </summary>
	public partial class TrackHandler : Node
	{
		private readonly AudioStreamPlayer audio = new();

		private double lastNoteTime;

		private int positionInBeats;

		public TrackHandler(TrackInfo info)
		{
			audio.Stream = info.Track;
			Bpm = info.Bpm;
			SecondsPerBeat = 60f / info.Bpm;
			ProcessPriority = -1;
			AddChild(audio);
		}

		/// <summary>
		///     The time in seconds between every 1/1 beat;
		/// </summary>
		public double SecondsPerBeat { get; private set; }

		/// <summary>
		///     The furthest beat measure that has been passed so far in the playback of the track.
		/// </summary>
		public int LastPlayedBeat { get; private set; }

		/// <summary>
		///     The BPM of the track.
		/// </summary>
		public double Bpm { get; private set; }

		/// <summary>
		///     The current position in the song.
		/// </summary>
		public double TrackPosition { get; private set; }

		/// <summary>
		///     The current 4/4 measure.
		/// </summary>
		public int Measure { get; private set; } = 1;

		/// <summary>
		///     How many measures there are per every major measure.
		/// </summary>
		public int Measures { get; private set; } = 4;

		/// <summary>
		///     The length of this track in seconds
		/// </summary>
		public double TrackLength => audio.Stream.GetLength();

		/// <summary>
		///     Whether the audio is currently playing.
		/// </summary>
		public bool Playing => audio.Playing;

		public double SongProgressPercentage =>
			Math.Round(TrackPosition / TrackLength, 2) * 100;

		private double offset => SecondsPerBeat * 4;

		public event Action Stopped;

		/// <summary>
		///     Called when the songs position changes, checked during physics processing.
		/// </summary>
		public event Action<double>? SongPositionChanged;

		/// <summary>
		///     Called on every 1/1 beat
		/// </summary>
		public event EventHandler<int>? OnBeat;

		/// <summary>
		///     Called after the beats preempting the actual song start have all been played
		/// </summary>
		public event EventHandler? OnPreemptComplete;

		/// <summary>
		///     Called before preempt beats, when the call to start the song is made
		/// </summary>
		public event Action? OnSongCommence;

		/// <summary>
		///     Returns true if a track is being played.
		/// </summary>
		/// <returns></returns>
		public void TogglePlayback()
		{
			if (audio.Playing)
				audio.SetStreamPaused(true);
			else
			{
				audio.Play((float)TrackPosition);
			}
		}

		public Vector2[] Buffer = [];

		private double timeDelay;

		/// <summary>
		/// Starts track, should not be called outside node tree.
		/// </summary>
		public void StartTrack()
		{
			audio.Stop();

			if (Buffer.Length == 0)
			{
				AudioServer.Lock();

				audio.Play();
				Buffer = audio.GetStreamPlayback().MixAudio(44, 1000000) ?? [];
				audio.Stop();

				AudioServer.Unlock();
			}

			var timer = new Timer();

			AddChild(timer);
			timer.WaitTime = offset / 4;
			timer.Start();

			OnSongCommence?.Invoke();

			int i = 0;
			timer.Timeout += () =>
			{
				i++;

				OnBeat?.Invoke(null, 0);
				if (i < 4) return;
				OnPreemptComplete?.Invoke(this, EventArgs.Empty);

				timeDelay = AudioServer.GetTimeToNextMix() + AudioServer.GetOutputLatency();
				audio.Play();
				timer.Stop();
			};
		}

		/// <summary>
		/// Stops playback of the loaded track.
		/// </summary>
		public void StopTrack()
		{
			TrackPosition = 0;
			audio.Stop();

			SongPositionChanged?.Invoke(TrackPosition);
			Stopped.Invoke();

			GD.PrintRich("[code][color=red] Stopped");
		}

		public AudioStreamPlayback? GetPlayback()
		{
			return audio.Playing ? audio.GetStreamPlayback() : null;
		}

		public override void _PhysicsProcess(double delta)
		{
			if (!audio.Playing) return;

			double time = audio.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix();
			// Compensate for output latency.
			TrackPosition = time;
		}

		private void reportBeat()
		{
			if (lastNoteTime > TrackPosition) return;

			if (Measure > Measures)
				Measure = 1;

			OnBeat?.Invoke(this, positionInBeats);

			LastPlayedBeat = positionInBeats;
			lastNoteTime = (LastPlayedBeat + 1) * SecondsPerBeat;

			Measure++;
		}
	}
}
