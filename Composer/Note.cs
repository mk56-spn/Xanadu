// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.DataStructure;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Dependent))]
    public partial class Note : Node2D
    {
        public override partial void _Notification(int what);

        [Export]
        private Area2D hitBox { get; set; } = null!;

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
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            // Keeps label scaling from center regardless of the size of the judgement text;
            judgementText.PivotOffset = judgementText.Size / 2;
        }

        public void Activate()
        {
            hitBox.Monitorable = false;

            double millisecondDeviation = TimeSpan.FromSeconds(PositionInTrack - trackHandler.TrackPosition).TotalMilliseconds;

            GD.Print($"Note hit with position {PositionInTrack} seconds");
            GD.Print($"Deviation of {millisecondDeviation} milliseconds");

            var judgement = JudgementInfo.GetJudgement(Math.Abs(millisecondDeviation));

            judgementText.Text = $"{JudgementInfo.GetJudgmentText(judgement).ToUpper()}\n{(millisecondDeviation < 0 ? "late" : "early" )}";

            animation.AssignedAnimation = "Animate";
            animation.Play();
        }
    }
}
