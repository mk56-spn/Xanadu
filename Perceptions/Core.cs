using System;
using Godot;
using XanaduProject.Singletons;

namespace XanaduProject.Perceptions;

public partial class Core : Perception
{
    private const int JumpVelocity = -1900;

    public bool IsAlive { get; private set; } = true;

    private Tween? rotationTween;
    private Area2D nucleus = null!;
    private Polygon2D body = null!;

    private AudioSource audioSource = null!;

    public override void _Ready()
    {
        base._Ready();

        body = GetNode<Polygon2D>("Body");
        nucleus = GetNode<Area2D>("Nucleus");

        GetNode<Area2D>("Shell").AreaShapeEntered += (_, _, _, _) => SetPhysicsProcess(false);
        nucleus.BodyEntered += _ =>
        {
            IsAlive = false;
            SetPhysicsProcess(false);
        };

        audioSource = GetNode<AudioSource>("/root/GlobalAudio");

        audioSource.RequestPlay = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        nucleus_collision();

        if (IsOnFloor())
            ground_movement();
        else
            air_movement(delta);

        MoveAndSlide();

        if (!(Math.Abs(Position.X - audioSource.TrackPosition * 700) > 25) || !audioSource.Playing) return;

        GD.Print($"A de-sync of {Math.Abs(TimeSpan.FromSeconds(Position.X / 700 - audioSource.TrackPosition).TotalMilliseconds)} milliseconds has occured");

        //Forces the player into position if it de-syncs more than the acceptable amount from the song,
        //rather brutish but functional.
        Position = new Vector2((float)audioSource.TrackPosition * 700, Position.Y);
    }

    private void ground_movement()
    {
        grounded_rotation();

        if (Input.IsActionPressed("main"))
            Velocity = new Vector2(Velocity.X, JumpVelocity);
    }

    private void grounded_rotation()
    {
        float targetRotation = Mathf.Snapped(body.RotationDegrees, 90);

        if (rotationTween != null || !(Math.Abs(body.RotationDegrees - targetRotation) > 0.01)) return;

        rotationTween = CreateTween();
        rotationTween.TweenProperty(body, "rotation_degrees", targetRotation, 0.1);
    }

    private void air_movement(double delta)
    {
        rotationTween?.Kill();
        rotationTween = null;

        body.Rotate(Mathf.DegToRad(360 * (float)delta));

        Velocity = new Vector2(Velocity.X, Mathf.Min(1500, Velocity.Y + Gravity * (float)delta));
    }

    private void nucleus_collision() =>
        nucleus.Modulate = nucleus.HasOverlappingBodies() ? Colors.Red : Colors.Green;
}