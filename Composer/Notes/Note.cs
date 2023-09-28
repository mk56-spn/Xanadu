// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.Composer.Selectables;
using XanaduProject.DataStructure;

namespace XanaduProject.Composer.Notes
{
    [SuperNode(typeof(Dependent))]
    public partial class Note : Node2D, IComposable
    {
        public Selectable Selectable => new SelectableNote();

        public override partial void _Notification(int what);

        private const double note_activation_preempt = 1;
        /// <summary>
        /// The current state this node is in;
        /// </summary>
        public NoteState State { private set; get; } = NoteState.Inactive;

        /// <summary>
        /// Called when the state is changed
        /// </summary>
        public event EventHandler<NoteState>? OnStateChanged;

        /// <summary>
        /// Called when the note's judgement has been processed
        /// </summary>
        public event Action<Judgement>? OnNoteJudged;

        [Export]
        private AnimationPlayer animation { get; set; } = null!;

        [Export]
        private Label judgementText { get; set; } = null!;

        [Export]
        public float PositionInTrack { get; set; }

        [Dependency] private TrackHandler trackHandler => DependOn<TrackHandler>();

        private Arc arc = new Arc();

        public void OnResolved()
        {
            AddChild(arc);
            // Hacky way of ensuring text is always centered during animation;
            judgementText.CustomMinimumSize = new Vector2(300, 0);

            trackHandler.OnSongCommence += () =>
                GetTree().CreateTimer(PositionInTrack - note_activation_preempt + trackHandler.SecondsPerBeat * 4, false, true).Timeout += () =>
                {
                    arc.Activate();
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
            Judgement judgement = JudgementInfo.GetJudgement(Math.Abs(millisecondDeviation));

            judgementText.Text = $"{JudgementInfo.GetJudgmentText(judgement).ToUpper()}\n{(millisecondDeviation < 0 ? "late" : "early" )}";

            OnNoteJudged?.Invoke(judgement);
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

        private partial class Arc : Node2D
        {
            private SceneTreeTimer? progressTween;

            public override void _Ready()
            {
                base._Ready();
                SetProcess(false);
            }

            public override void _Process(double delta)
            {
                base._Process(delta);

                if (progressTween == null) return;
                QueueRedraw();
            }

            public override void _Draw()
            {
                base._Draw();

                if (progressTween == null) return;

                double arcMultiplier = progressTween.TimeLeft / note_activation_preempt;
                DrawArc(Vector2.Zero,
                    70,
                    0,
                    (float)(2 * MathF.PI * (1 - arcMultiplier)),
                    60,
                    Colors.Orange,
                    4,
                    true);
            }

            public void Activate()
            {
                SetProcess(true);
                progressTween = GetTree().CreateTimer(note_activation_preempt, false, true);
                progressTween.Timeout += () =>
                    CreateTween().TweenProperty(this, "modulate", Modulate with { A = 0 }, note_activation_preempt / 2 );
            }
        }
    }
}
