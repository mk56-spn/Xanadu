
// Copyright (c) mk56_spn.
// Licensed under the GNU General Public Licence v2.0.
//
// Simplified, robust version: This clock design relies on the Godot
// AudioStreamPlayer's built-in position tracking for simplicity and
// reliability. A dedicated thread ensures the audio buffer is consistently
// fed, providing smooth playback.

using System;
using System.Diagnostics;
using System.Threading;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Audio
{
	/// <summary>
	/// A hybrid master clock that provides smooth, jitter-free timekeeping.
	/// It uses the audio stream as the "source of truth" for state changes like
	/// starting, pausing, and seeking, but employs a high-resolution Stopwatch
	/// for calculating the playback time frame-to-frame. This combination offers
	/// the accuracy of the audio system with the smoothness of a monotonic clock.
	/// A background thread pre-feeds the audio buffer to ensure uninterrupted playback.
	/// </summary>
	public abstract partial class BaseClock : Node
	{
		public double TrackLength { get; private set; }
		public double PlaybackTime { get; private set; }
		public double CurrentBeat { get; protected set; }
		public bool Paused { get; private set; } = true;

		/// <summary>
		/// The lead-in time in seconds before the actual track starts.
		/// This time can be used for countdowns or preparatory elements.
		/// </summary>
		public double LeadInTime { get; set; } = 0.0;

		/// <summary>
		/// Gets the actual playback position including the lead-in time.
		/// Negative values indicate we're in the lead-in period.
		/// </summary>
		public double AdjustedPlaybackTime => PlaybackTime - LeadInTime;

		public void SetPaused(bool shouldPause)
		{
			if (Paused == shouldPause) return;

			Paused = shouldPause;
			player.StreamPaused = Paused;

			if (Paused)
			{
				// Pausing: Stop the stopwatch and update the time base to the exact
				// moment of pausing.
				stopwatch.Stop();
				timeBase += stopwatch.Elapsed.TotalSeconds;
			}
			else
			{
				// Unpausing: Restart the stopwatch to measure elapsed time from this point.
				stopwatch.Restart();
			}
		}


		private readonly AudioStreamPlayer player = new();
		private int sampleRate;
		private Vector2[] songData;
		private AudioStreamGeneratorPlayback playback = null!;

		// ─────────────────────────── Hybrid Clock state ─────────────────────────
		private readonly Stopwatch stopwatch = new();
		private double timeBase;

		// ─────────────────────────── Threading state ────────────────────────────
		private Thread? feedThread;
		private volatile bool stopThread;
		private int cursor;
		private readonly Lock audioLock = new();
		private readonly TrackInfo track;

		/// <summary>
		/// A hybrid master clock that provides smooth, jitter-free timekeeping.
		/// It uses the audio stream as the "source of truth" for state changes like
		/// starting, pausing, and seeking, but employs a high-resolution Stopwatch
		/// for calculating the playback time frame-to-frame. This combination offers
		/// the accuracy of the audio system with the smoothness of a monotonic clock.
		/// A background thread pre-feeds the audio buffer to ensure uninterrupted playback.
		/// </summary>
		protected BaseClock(TrackInfo track)
		{
			this.track = track;
			VorbisLoader.DecodeEntireFileIntoMemory(track.Track, out float[] pcm, out sampleRate);
			songData = toVector2Array(pcm);
		}

		// ─────────────────────────── Godot lifecycle ────────────────────────────
		public override void _EnterTree()
		{

			TrackLength = songData.Length / (double)sampleRate;

			var generator = new AudioStreamGenerator
			{
				MixRate = sampleRate,
				BufferLength = 0.01f
			};

			player.Stream = generator;
			AddChild(player);

			// Start the player so it's ready, then immediately pause.
			player.Play();
			playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
			SetPaused(true);
		}

		public override void _Ready()
		{
			feedThread = new Thread(audioFeedLoop)
			{
				IsBackground = true,
				Name = "AudioFeedThread",
				Priority = ThreadPriority.Highest
			};
			feedThread.Start();
		}

		public override void _ExitTree()
		{
			stopThread = true;
			feedThread?.Join();
		}

		// ───────────────────────── Per-frame callback ───────────────────────────
		public override void _Process(double delta)
		{
			// When playing, update time from the stopwatch for smooth progression.
			if (!Paused) PlaybackTime = timeBase + stopwatch.Elapsed.TotalSeconds;

			// If the track has finished, ensure time is capped and pause playback.
			if (PlaybackTime >= TrackLength + LeadInTime)
			{
				PlaybackTime = TrackLength + LeadInTime;
				SetPaused(true);
			}

			// Send adjusted playback time to shaders
			RenderingServer.GlobalShaderParameterSet("song_pos", AdjustedPlaybackTime);
		}

		/// <summary>
		/// Seeks to a specific time in the track, accounting for lead-in time.
		/// </summary>
		/// <param name="time">The time to seek to, where 0.0 is the start of the actual track (after lead-in)</param>
		public void Seek(double time)
		{
			// Adjust the seek time to account for lead-in
			SeekAbsolute(time + LeadInTime);
		}

		/// <summary>
		/// Seeks to an absolute time position including the lead-in period.
		/// </summary>
		/// <param name="absoluteTime">The absolute time to seek to, where 0.0 is the start of lead-in</param>
		public void SeekAbsolute(double absoluteTime)
		{
			bool wasPlaying = !Paused;
			SetPaused(true); // Pause and stop the stopwatch

			lock (audioLock)
			{
				absoluteTime = Math.Clamp(absoluteTime, 0.0, TrackLength + LeadInTime);

				// Stop/Play is the most reliable way to clear the audio buffer
				// and restart playback from a new position.
				player.Stop();
				player.Play();
				playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();

				// Calculate cursor position based on whether we're in lead-in or actual track
				if (absoluteTime < LeadInTime)
				{
					// In lead-in phase, don't play audio yet
					cursor = 0;
				}
				else
				{
					// Set the cursor position for the actual track audio
					double trackTime = absoluteTime - LeadInTime;
					cursor = (int)(trackTime * sampleRate);
				}

				// Update timekeeping to the new source-of-truth time.
				timeBase = absoluteTime;
				PlaybackTime = absoluteTime;
				stopwatch.Reset();
			}

			if (wasPlaying) SetPaused(false); // Resume playback
		}

		protected void Reset()
		{
			// Pause playback, which stops the audio feeder thread and the stopwatch.
			SetPaused(true);

			// Lock to ensure thread-safe manipulation of audio resources.
			lock (audioLock)
			{
				player.Stop(); // This resets playback position to 0.
				player.Play(); // A new stream playback is generated. We must re-acquire it.
				playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
				cursor = 0;
			}

			// Reset timekeeping properties.
			stopwatch.Reset();
			timeBase = 0.0;
			PlaybackTime = 0.0;
			CurrentBeat = 0.0;
		}

		// ───────────────────────────── Audio feeder ─────────────────────────────
		private void audioFeedLoop()
		{
			while (!stopThread)
			{
				// Only feed buffer if playing and not at the end of the song,
				// and we're past the lead-in time
				if (!Paused)
					lock (audioLock)
					{
						// Only play audio if we're past the lead-in period
						if (PlaybackTime >= LeadInTime && cursor < songData.Length)
						{
							int framesAvailable = playback.GetFramesAvailable();
							if (framesAvailable > 0)
							{
								int framesToCopy = Math.Min(framesAvailable, songData.Length - cursor);
								var slice = new Span<Vector2>(songData, cursor, framesToCopy);

								if (playback.PushBuffer(slice)) cursor += framesToCopy;
							}
						}
					}

				// Wait briefly to avoid pegging the CPU.
				Thread.Sleep(1);
			}
		}

		// ─────────────────────────── Helpers ────────────────────────────
		private static Vector2[] toVector2Array(float[] src)
		{
			if (src.Length % 2 != 0)
				throw new ArgumentException("PCM sample count must be even (stereo).", nameof(src));

			var dst = new Vector2[src.Length / 2];
			for (int i = 0; i < dst.Length; ++i)
				dst[i] = new Vector2(src[i * 2], src[i * 2 + 1]);
			return dst;
		}
	}
}
