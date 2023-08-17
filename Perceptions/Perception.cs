// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Composer;
using XanaduProject.Singletons;

namespace XanaduProject.Perceptions
{
    public abstract partial class Perception : CharacterBody2D
    {
        public const int BASE_VELOCITY = 700;
        protected int Gravity;

        /// <summary>
        /// Returns the current state of the core
        /// </summary>
        public bool IsAlive { get; private set; } = true;

        protected AudioSource AudioSource = null!;

        [Export]
        protected Area2D NoteReceptor { get; set; } = null!;
        [Export]
        protected Polygon2D Body { get; private set; } = null!;
        [Export]
        protected Area2D Nucleus { get; private set; } = null!;

        protected Perception()
        {
            var fetchGravity = ProjectSettings.GetSetting("physics/2d/default_gravity");
            Gravity = fetchGravity.AsInt32();

            Velocity = new Vector2(BASE_VELOCITY, 0);
        }

        public override void _Ready()
        {
            base._Ready();

            AddChild(new NoteProcessor(NoteReceptor));

            AudioSource = SingletonSource.GetAudioSource();
            AudioSource.RequestPlay = true;

            GetNode<Area2D>("Shell").AreaEntered += _ =>
                SetPhysicsProcess(false);

            Nucleus.BodyEntered += _ =>
            {
                IsAlive = false;
                SetPhysicsProcess(false);
            };
        }
    }
}
