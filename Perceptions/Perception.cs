// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.Perceptions.Components;
using XanaduProject.Screens;

namespace XanaduProject.Perceptions
{
    [SuperNode(typeof(Dependent))]
    public abstract partial class Perception : CharacterBody2D
    {
        public override partial void _Notification(int what);

        public bool Movable;

        private const int base_velocity = 700;
        protected int Gravity;

        // Emits an event when this perception is rendered dead.
        public event Action? OnDeath;

        private RhythmHandle[] handles { get;  set; } = Array.Empty<RhythmHandle>();

        [Export]
        protected Polygon2D Body { get; private set; } = null!;
        [Export]
        protected Area2D Nucleus { get; private set; } = null!;

        [Dependency] private TrackHandler trackHandler => DependOn<TrackHandler>();

        private HBoxContainer handleContainer = new HBoxContainer();

        protected Perception()
        {
            var fetchGravity = ProjectSettings.GetSetting("physics/2d/default_gravity");
            Gravity = fetchGravity.AsInt32();

            Velocity = new Vector2(base_velocity, 0);

            ProcessMode = ProcessModeEnum.Pausable;
        }

        public void OnResolved()
        {
            handleContainer.GrowHorizontal = Control.GrowDirection.Both;
            AddChild(handleContainer);

            Stage stage = GetParent<Stage>();

            foreach (var line in stage.Info.GetLines())
            {
                if (line.active) continue;
                handleContainer.AddChild(RhythmHandle.CreateHandle(line.instance, stage.NoteLinks));
            }

            handles = handleContainer.GetChildren().OfType<RhythmHandle>().ToArray();
            handleContainer.Position = Position with { Y = -15 };
        }

        public override void _Ready()
        {
            base._Ready();

            GetNode<Area2D>("%Shell").AreaEntered += _ => OnDeath?.Invoke();
            Nucleus.BodyEntered += _ => { OnDeath?.Invoke(); };
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!Movable) return;
            if (!(Math.Abs(Position.X - trackHandler.TrackPosition * base_velocity) > 25) || !trackHandler.Playing) return;

            GD.Print(
                $"A de-sync of {Math.Abs(TimeSpan.FromSeconds(Position.X / base_velocity - trackHandler.TrackPosition).TotalMilliseconds)} milliseconds has occured");

            //Forces the player into position if it de-syncs more than the acceptable amount from the song,
            //rather brutish but functional.
            Position = new Vector2((float)trackHandler.TrackPosition * base_velocity, Position.Y);
        }
    }
}
