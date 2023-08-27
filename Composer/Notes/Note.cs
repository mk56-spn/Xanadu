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

        private const double note_activation_preempt = 0.4;
        /// <summary>
        /// The current state this node is in;
        /// </summary>
        public NoteState State { private set; get; } = NoteState.Inactive;

        /// <summary>
        /// Called when the state is update
        /// </summary>
        public event EventHandler<NoteState>? OnStateChanged;

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

        public void OnResolved()
        {
            // Hacky way of ensuring text is always centered during animation;
            judgementText.CustomMinimumSize = new Vector2(300, 0);

            trackHandler.OnPreemptComplete += (_, _) =>
                GetTree().CreateTimer(PositionInTrack - note_activation_preempt, false, true).Timeout += () =>
                {
                    RequestState(NoteState.Active);
                    GetTree().CreateTimer(note_activation_preempt * 2, false, true).Timeout +=
                        () => RequestState(NoteState.Judged);
                };


            OnStateChanged += (_, state) =>
            {
                if (state == NoteState.Judged)
                    noteJudged();
            };
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            // Keeps label scaling from center regardless of the size of the judgement text;
            judgementText.PivotOffset = judgementText.Size / 2;
        }

        private void noteJudged()
        {
            animation.AssignedAnimation = "Animate";
            animation.Play();

            judgeNote();
        }

        private void judgeNote()
        {
            double millisecondDeviation = TimeSpan.FromSeconds(PositionInTrack - trackHandler.TrackPosition).TotalMilliseconds;
            var judgement = JudgementInfo.GetJudgement(Math.Abs(millisecondDeviation));

            judgementText.Text = $"{JudgementInfo.GetJudgmentText(judgement).ToUpper()}\n{(millisecondDeviation < 0 ? "late" : "early" )}";
        }

        /// <summary>
        /// Attempts to set a state on the note. Triggers an action if successful
        /// </summary>
        /// <param name="newState"></param>
        public void RequestState(NoteState newState)
        {
            switch (State)
            {
                case NoteState.Inactive:
                    if (newState == NoteState.Active )
                        updateState();
                    return;

                case NoteState.Active:
                    if (newState == NoteState.Judged)
                        updateState();
                    return;

                default:
                    return;
            }

            void updateState()
            {
                State = newState;
                OnStateChanged?.Invoke(this, newState);
            }
        }

        public enum NoteState
        {
            Inactive,
            Active,
            Judged
        }
    }
}
