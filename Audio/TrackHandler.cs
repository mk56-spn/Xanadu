// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Audio
{
    /// <summary>
    /// The track manager is a source of truth for audio timing across a gameplay instance.
    /// </summary>
    public partial class TrackHandler : Node
    {
        /// <summary>
        /// Called when the songs position changes, checked during physics processing.
        /// </summary>
        public event Action<double>? SongPositionChanged;

        /// <summary>
        /// Called on every 1/1 beat
        /// </summary>
        public event EventHandler<int>? OnBeat;

        /// <summary>
        /// Called after the beats preempting the actual song start have all been played
        /// </summary>
        public event EventHandler OnPreemptComplete = null!;

        /// <summary>
        /// Called before preempt beats, when the call to start the song is made
        /// </summary>
        public event Action OnSongCommence = null!;
        /// <summary>
        /// The time in seconds between every 1/1 beat;
        /// </summary>
        public double SecondsPerBeat { get; private set; }

        /// <summary>
        /// The furthest beat measure that has been passed so far in the playback of the track.
        /// </summary>
        public int LastPlayedBeat { get; private set; }

        /// <summary>
        /// The BPM of the track.
        /// </summary>
        public double Bpm { get; private set; } = 120;

        /// <summary>
        /// The current position in the song.
        /// </summary>
        public double TrackPosition { get; private set; }

        /// <summary>
        /// The current 4/4 measure.
        /// </summary>
        public int Measure { get; private set; } = 1;

        /// <summary>
        /// How many measures there are per every major measure.
        /// </summary>
        public int Measures { get; private set; } = 4;

        /// <summary>
        /// The length of this track in seconds
        /// </summary>
        public double TrackLength => audio.Stream.GetLength();

        /// <summary>
        /// Whether the audio is currently playing.
        /// </summary>
        public bool Playing => audio.Playing;

        /// <summary>
        /// Returns true if a track is being played.
        /// </summary>
        /// <returns></returns>
        public void TogglePlayback()
        {
            if (audio.Playing)
                audio.Stop();
            else audio.Play((float)TrackPosition);
        }

        public void StartTrack()
        {
            audio.Stop();
            Timer timer = new Timer();

            AddChild(timer);
            timer.WaitTime = offset / 4;
            timer.Start();

            OnSongCommence.Invoke();

            int i = 0;
            timer.Timeout += () =>
            {
                i++;

                OnBeat?.Invoke(null, 0);
                if (i < 4 ) return;
                OnPreemptComplete.Invoke(this, EventArgs.Empty);
                audio.Play();
                timer.Paused = true;
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
        }

        private double lastNoteTime;
        public double SongProgressPercentage =>
            Math.Round(TrackPosition / TrackLength, 2) * 100;

        private int positionInBeats;

        private double offset => SecondsPerBeat * 4;

        private readonly AudioStreamPlayer audio = new AudioStreamPlayer();

        public TrackHandler ()
        {
            ProcessPriority = -1;
            AddChild(audio);
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!audio.Playing) return;
            double oldTrackPosition = TrackPosition;

            TrackPosition = audio.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix();
            TrackPosition -= AudioServer.GetOutputLatency();
            positionInBeats = (int)Math.Floor(TrackPosition / SecondsPerBeat);

            reportBeat();

            if (TrackPosition.Equals(oldTrackPosition)) return;
            SongPositionChanged?.Invoke(TrackPosition);
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

        public void SetTrack(TrackInfo info)
        {
            audio.Stream = info.Track;

            Bpm = info.Bpm;
            SecondsPerBeat = 60f / info.Bpm;
        }
    }
}
