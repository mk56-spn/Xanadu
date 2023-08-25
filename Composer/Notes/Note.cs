// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.DataStructure;

namespace XanaduProject.Composer.Notes
{
    [SuperNode(typeof(Dependent))]
    public partial class Note : Node2D
    {
        public override partial void _Notification(int what);

        /// <summary>
        /// The current state this node is in;
        /// </summary>
        public NoteState State { private set; get; } = NoteState.Inactive;

        /// <summary>
        /// Triggered when the note is activated
        /// </summary>
        public event Action? OnHit;

        [Export]
        private AnimationPlayer animation { get; set; } = null!;

        [Export]
        private Label judgementText { get; set; } = null!;

        [Export]
        public float PositionInTrack { get; set; }

        [Dependency] private TrackHandler trackHandler => DependOn<TrackHandler>();

        public Note ()
        {
            AddToGroup("Notes");

            OnHit += () => State = NoteState.Hit;
        }

        public override void _Ready()
        {
            base._Ready();

            // Hacky way of ensuring text is always centered during animation;
            judgementText.CustomMinimumSize = new Vector2(300, 0);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            // Keeps label scaling from center regardless of the size of the judgement text;
            judgementText.PivotOffset = judgementText.Size / 2;
        }

        public void Activate()
        {
            double millisecondDeviation = TimeSpan.FromSeconds(PositionInTrack - trackHandler.TrackPosition).TotalMilliseconds;

            GD.Print($"Note hit with position {PositionInTrack} seconds");
            GD.Print($"Deviation of {millisecondDeviation} milliseconds");

            var judgement = JudgementInfo.GetJudgement(Math.Abs(millisecondDeviation));

            judgementText.Text = $"{JudgementInfo.GetJudgmentText(judgement).ToUpper()}\n{(millisecondDeviation < 0 ? "late" : "early" )}";

            animation.AssignedAnimation = "Animate";
            animation.Play();

            OnHit?.Invoke();
        }

        public enum NoteState
        {
            Inactive,
            Active,
            Hit,
        }
    }
}
