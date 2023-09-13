// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.Composer;
using XanaduProject.Perceptions.Components;
using XanaduProject.Screens;

namespace XanaduProject.Perceptions
{
    [SuperNode(typeof(Dependent))]
    public abstract partial class Perception : CharacterBody2D
    {
        public override partial void _Notification(int what);

        public const int BASE_VELOCITY = 700;
        protected int Gravity;

        /// <summary>
        /// Returns the current state of the core
        /// </summary>
        public bool IsAlive { get; private set; } = true;

        [Export]
        protected Area2D NoteReceptor { get; set; } = null!;
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

            Velocity = new Vector2(BASE_VELOCITY, 0);
        }

        public void OnResolved()
        {
            handleContainer.GrowHorizontal = Control.GrowDirection.Both;
            AddChild(handleContainer);

            foreach (var line in GetParent<Stage>().Info.GetLines())
            {
                if (!line.active) continue;
                handleContainer.AddChild(createHandle(line.instance));
            }

            handleContainer.Position = Position with { Y = -15 };
        }

        public override void _Ready()
        {
            base._Ready();

            AddChild(new HitNoteProcessor(NoteReceptor));

            GetNode<Area2D>("%Shell").AreaEntered += _ =>
                GetTree().Paused = true;

            Nucleus.BodyEntered += _ =>
            {
                IsAlive = false;
                GetTree().Paused = true;
            };
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            if (!(Math.Abs(Position.X - trackHandler.TrackPosition * 700) > 25) || !trackHandler.Playing) return;

            GD.Print(
                $"A de-sync of {Math.Abs(TimeSpan.FromSeconds(Position.X / BASE_VELOCITY - trackHandler.TrackPosition).TotalMilliseconds)} milliseconds has occured");

            //Forces the player into position if it de-syncs more than the acceptable amount from the song,
            //rather brutish but functional.
            Position = new Vector2((float)trackHandler.TrackPosition * BASE_VELOCITY, Position.Y);
        }

        private RhythmHandle createHandle(RhythmInstance instance)
        {
            RhythmHandle handle = ResourceLoader.Load<PackedScene>("res://Perceptions/Components/RhythmHandle.tscn")
                .Instantiate<RhythmHandle>();

            handle.Instance = instance;
            return handle;
        }
    }
}
